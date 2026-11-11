"use client";
import { useRef, useState } from "react";
import { Upload, Loader2 } from "lucide-react";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription, DialogFooter } from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { internalApi, type DraftField, type DraftTerm, type TemplateDefinition } from "@/services/internal-api";
import { internalStyles as ui } from "./internal-theme";

export default function TemplateUpload({ onImported, disabled }: { onImported: (template: TemplateDefinition) => void; disabled?: boolean }) {
  const [open, setOpen] = useState(false), [busy, setBusy] = useState(false), [error, setError] = useState("");
  const [file, setFile] = useState<{ fileName: string; base64: string } | null>(null);
  const [name, setName] = useState(""), [content, setContent] = useState(""), [warning, setWarning] = useState("");
  const [terms, setTerms] = useState<DraftTerm[]>([]), [fields, setFields] = useState<DraftField[]>([]);
  const cursors = useRef<Record<string,{start:number;end:number}>>({});
  const textareas = useRef<Record<string,HTMLTextAreaElement | null>>({});
  const [activeTerm,setActiveTerm] = useState(0);
  const coreKeys = ["CUSTOMER_COMPANY","CUSTOMER_CONTACT_NAME","CUSTOMER_CONTACT_PHONE","CUSTOMER_ADDRESS","CUSTOMER_TAX_CODE","PROVIDER_LEGAL_NAME","PROVIDER_REPRESENTATIVE_NAME","PROVIDER_ADDRESS","PROVIDER_TAX_CODE","TOTAL_AMOUNT","SUBTOTAL","VAT","DISCOUNT","PAYMENT_SCHEDULE","CONTRACT_NAME","EFFECTIVE_DATE","EXPIRE_DATE"];
  const [checked, setChecked] = useState(false);
  const validation: string[] = [];
  const keys = [...coreKeys,"CUSTOMER_EMAIL",...fields.map(f=>f.key)];
  fields.forEach((f,i)=> { if(!/^[A-Z][A-Z0-9_]{0,99}$/.test(f.key) || !f.label.trim()) validation.push('Trường '+(i+1)+': nhập khóa viết hoa và nhãn.'); if(coreKeys.includes(f.key) || fields.filter(x=>x.key===f.key).length>1) validation.push('Khóa trùng hoặc trùng trường lõi: '+f.key); if(f.dataType==='SELECT' && (!f.options?.length || f.options.some(o=>!o.trim()) || new Set(f.options).size!==f.options.length)) validation.push(f.label+': danh sách lựa chọn không hợp lệ.'); if(f.min!=null && f.max!=null && f.min>f.max) validation.push(f.label+': giới hạn nhỏ nhất vượt lớn nhất.'); });
  terms.forEach((t,i)=> {if(!t.title.trim() || !t.content.trim()) validation.push('Điều khoản '+(i+1)+': thiếu tiêu đề hoặc nội dung.'); for(const text of [t.title,t.content]) {for(const m of text.matchAll(/\{\{([^{}]*)\}\}/g)) if(!keys.includes(m[1])) validation.push('Placeholder chưa ánh xạ: '+m[0]); if(/\{\{|\}\}/.test(text.replace(/\{\{[^{}]*\}\}/g,''))) validation.push('Điều khoản '+(i+1)+': sai cú pháp {{KEY}}.');}});
  function insert(key:string) { const term=terms[activeTerm]; if(!term || !key) return; const selection=cursors.current[term.key] ?? {start:term.content.length,end:term.content.length}; const token='{{'+key+'}}'; setTerms(ts=>ts.map((t,i)=>i===activeTerm?{...t,content:t.content.slice(0,selection.start)+token+t.content.slice(selection.end)}:t)); const pos=selection.start+token.length; cursors.current[term.key]={start:pos,end:pos};setChecked(false);requestAnimationFrame(()=>{const el=textareas.current[term.key];el?.focus();el?.setSelectionRange(pos,pos);}); }
  function move(i:number,delta:number) {setTerms(ts=> {const next=[...ts]; [next[i],next[i+delta]]=[next[i+delta],next[i]];return next;});setActiveTerm(i+delta);setChecked(false);}
  function split(i:number) {const term=terms[i];const pos=cursors.current[term.key]?.start ?? term.content.length; if(pos<=0 || pos>=term.content.length) {setError('Đặt con trỏ bên trong nội dung để tách điều khoản.');return;}setTerms(ts=>ts.flatMap((t,j)=>j===i?[{...t,content:t.content.slice(0,pos)},{...t,key:crypto.randomUUID(),title:t.title+' (tiếp)',content:t.content.slice(pos)}]:[t]));setActiveTerm(i+1);setChecked(false);}
  async function extract(selected?: File) {
    if (!selected) return;
    setTerms([]); setFields([]); setActiveTerm(0); setFile(null); setContent(""); setName(""); setChecked(false); setError(""); setWarning("");
    if (!/\.(docx|pdf)$/i.test(selected.name) || selected.size === 0 || selected.size > 10 * 1024 * 1024) { setError("Chọn DOCX/PDF có nội dung, tối đa 10 MB. File .doc cần lưu lại thành .docx."); return; }
    setBusy(true);
    try {
      const base64 = await new Promise<string>((resolve, reject) => { const reader = new FileReader(); reader.onload = () => resolve(String(reader.result).split(",")[1]); reader.onerror = () => reject(new Error("Không đọc được file.")); reader.readAsDataURL(selected); });
      const source = { fileName: selected.name, base64 };
      const result = await internalApi<{ name: string; content: string; warning?: string }>("/contract-templates/extract", "POST", source);
      setFile(source); setName(result.name); setContent(result.content); setTerms(Array.from({length: Math.max(1,Math.ceil(result.content.length/18000))}, (_,i) => ({key:`TERM_${i+1}`,title:`Điều khoản ${i+1}`,content:result.content.slice(i*18000,(i+1)*18000),editable:true}))); setWarning(result.warning || "Hãy đối chiếu với file gốc; bố cục, ảnh và bảng không được giữ nguyên.");
    } catch (e) { setError((e as Error).message); } finally { setBusy(false); }
  }
  async function save() {
    if (validation.length || !file || !checked || !name.trim() || !terms.some(t => t.content.trim())) return;
    setBusy(true); setError("");
    try { const template = await internalApi<TemplateDefinition>("/contract-templates/import", "POST", { ...file, name, content: terms.map(t => t.content).join("\n"), terms, fields }); onImported(template); setOpen(false); }
    catch (e) { setError((e as Error).message); } finally { setBusy(false); }
  }
  return <>
    <Button type="button" variant="outline" disabled={disabled} onClick={() => { setOpen(true); setError(""); setFile(null); setContent(""); setName(""); setWarning(""); setChecked(false); }}><Upload className="size-4"/>Tải lên hợp đồng mẫu</Button>
    <Dialog open={open} onOpenChange={value => { if (!busy) setOpen(value); }}><DialogContent className="sm:max-w-3xl max-h-[90vh] overflow-y-auto" onInteractOutside={e => e.preventDefault()}>
      <DialogHeader><DialogTitle>Tải lên hợp đồng mẫu</DialogTitle><DialogDescription>Chọn Word (.docx) hoặc PDF, tối đa 10 MB / 100 trang PDF. Kiểm tra nội dung trước khi lưu thành mẫu dùng chung trong công ty.</DialogDescription></DialogHeader>
      <label className="grid gap-2 text-sm font-medium">File hợp đồng<Input type="file" accept=".docx,.pdf" disabled={busy} onChange={e => void extract(e.target.files?.[0])}/></label>
      {busy && <p role="status" className="flex items-center gap-2 text-sm"><Loader2 className="size-4 animate-spin"/>Đang xử lý…</p>}
      {error && <p role="alert" className={ui.error}>{error}</p>}
      {file && <>
        <p className="rounded-lg bg-amber-50 p-3 text-sm text-amber-900">{warning} PDF scan cần tự bổ sung chữ ở ô dưới; chưa có nhận dạng chữ tự động.</p>
        <label className="grid gap-2 text-sm font-medium">Tên mẫu<Input value={name} maxLength={200} disabled={busy} onChange={e => setName(e.target.value)}/></label>
        <p className="text-sm">Chia và chỉnh sửa điều khoản. Chọn vị trí trong nội dung rồi chèn trường; dữ liệu lõi được lấy từ hợp đồng.</p>
        <select aria-label="Chèn trường vào điều khoản" className={ui.field} value="" onChange={e => insert(e.target.value)}><option value="">Chèn trường tại con trỏ…</option>{[...coreKeys,...fields.map(f => f.key)].map(key => <option key={key}>{key}</option>)}</select>
        {terms.map((term,i) => <div key={term.key} className="grid gap-2 border rounded-lg p-3"><Input aria-label={"Tiêu đề điều khoản " + (i+1)} value={term.title} onFocus={() => setActiveTerm(i)} onChange={e => {setTerms(ts => ts.map((t,j) => i===j ? {...t,title:e.target.value} : t));setChecked(false);}}/><Textarea aria-label={"Nội dung điều khoản " + (i+1)} ref={el => {textareas.current[term.key]=el;}} onSelect={e => {cursors.current[term.key]={start:e.currentTarget.selectionStart,end:e.currentTarget.selectionEnd};}} onBlur={e => {cursors.current[term.key]={start:e.currentTarget.selectionStart,end:e.currentTarget.selectionEnd};}} rows={8} maxLength={20000} value={term.content} onFocus={() => setActiveTerm(i)} onChange={e => {setTerms(ts => ts.map((t,j) => i===j ? {...t,content:e.target.value} : t));setChecked(false);}}/><div className="flex gap-2"><Button type="button" variant="outline" disabled={i===0} onClick={()=>move(i,-1)}>Đưa lên</Button><Button type="button" variant="outline" disabled={i===terms.length-1} onClick={()=>move(i,1)}>Đưa xuống</Button><Button type="button" variant="outline" onClick={()=>split(i)}>Tách tại con trỏ</Button></div><label className="text-sm"><input type="checkbox" checked={term.editable} onChange={e => {setTerms(ts => ts.map((t,j) => i===j ? {...t,editable:e.target.checked} : t));setChecked(false);}}/> Cho phép thương lượng</label><Button type="button" variant="outline" onClick={() => {setTerms(ts => ts.filter((_,j) => i!==j));setActiveTerm(0);setChecked(false);}}>Xóa điều khoản</Button></div>)}
        <Button type="button" variant="outline" onClick={() => {setTerms(ts => [...ts,{key:crypto.randomUUID(),title:"Điều khoản bổ sung",content:"",editable:true}]);setChecked(false);}}>Thêm điều khoản</Button>
        <h3>Trường mở rộng</h3>{fields.map((f,i) => <div className="grid gap-2 border p-3 rounded-lg" key={i}><Input aria-label="Khóa trường" placeholder="KEY_VIET_HOA" value={f.key} onChange={e => {setFields(fs => fs.map((x,j) => i===j ? {...x,key:e.target.value} : x));setChecked(false);}}/><Input aria-label="Nhãn trường" placeholder="Tên hiển thị" value={f.label} onChange={e => {setFields(fs => fs.map((x,j) => i===j ? {...x,label:e.target.value} : x));setChecked(false);}}/><select aria-label="Kiểu trường" className={ui.field} value={f.dataType} onChange={e => {setFields(fs => fs.map((x,j) => i===j ? {...x,dataType:e.target.value as DraftField['dataType']} : x));setChecked(false);}}><option value="TEXT">Văn bản</option><option value="NUMBER">Số</option><option value="DATE">Ngày</option><option value="SELECT">Danh sách</option></select>{f.dataType === "SELECT" && <Input placeholder="Các lựa chọn, cách nhau bằng dấu ;" value={f.options?.join(";") ?? ""} onChange={e => {setFields(fs => fs.map((x,j) => i===j ? {...x,options:e.target.value.split(";")} : x));setChecked(false);}}/>}{f.dataType === "NUMBER" && <div className="flex gap-2">{(['min','max'] as const).map(key => <Input key={key} aria-label={key} type="number" placeholder={key === 'min' ? 'Nhỏ nhất' : 'Lớn nhất'} value={f[key] ?? ''} onChange={e => {setFields(fs => fs.map((x,j) => i===j ? {...x,[key]:e.target.value === '' ? null : e.target.valueAsNumber} : x));setChecked(false);}}/>)}</div>}<label><input type="checkbox" checked={f.required} onChange={e => {setFields(fs => fs.map((x,j) => i===j ? {...x,required:e.target.checked} : x));setChecked(false);}}/> Bắt buộc khi gửi khách</label><Button type="button" variant="outline" onClick={() => {setFields(fs => fs.filter((_,j) => i!==j));setChecked(false);}}>Xóa trường</Button></div>)}
        <Button type="button" variant="outline" onClick={() => {setFields(fs => [...fs,{key:"",label:"",required:false,dataType:"TEXT"}]);setChecked(false);}}>Thêm trường mở rộng</Button>
        <label className="flex items-start gap-2 text-sm"><input type="checkbox" className="mt-1" checked={checked} disabled={busy} onChange={e => setChecked(e.target.checked)}/>Tôi đã kiểm tra nội dung so với file gốc. Mẫu này chưa được coi là mẫu đã phê duyệt.</label>
      </>}
      {file && validation.length>0 && <ul role="alert" className={ui.error}>{[...new Set(validation)].map(m=><li key={m}>{m}</li>)}</ul>}
      <DialogFooter><Button type="button" variant="outline" disabled={busy} onClick={() => setOpen(false)}>Hủy</Button><Button type="button" disabled={busy || !file || !checked || !name.trim() || !terms.length || validation.length>0} onClick={() => void save()}>Lưu mẫu và sử dụng</Button></DialogFooter>
    </DialogContent></Dialog>
  </>;
}

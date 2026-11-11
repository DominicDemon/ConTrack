"use client";
import WordDocumentLayout, { type DocumentBlock } from "./word-document-layout";
import WordPdfViewer from "./word-pdf-viewer";
import WordTableEditor, { createTable, type AddedTable } from "./word-table-editor";
import WordContentEditor, { type AddedContent } from "./word-content-editor";
import { useEffect, useRef, useState } from "react";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription } from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Panel, PanelGroup, PanelResizeHandle } from "react-resizable-panels";
import styles from "./template-workspace.module.css";
import { internalApi, type TemplateDefinition } from "@/services/internal-api";

type Block = DocumentBlock;
type Extraction = { name: string; documentBase64: string; blocks: Block[]; sourceToken: string };
export function wordBlob(base64: string, type: string) {
  return new Blob([Uint8Array.from(atob(base64), c => c.charCodeAt(0))], { type });
}
const documentTypes = ["Hợp đồng", "Phụ lục", "Biên bản bàn giao", "Biên lai", "Đề nghị thanh toán"];
export default function TemplateUpload({ onImported, disabled }: { onImported: (template: TemplateDefinition) => void; disabled?: boolean }) {
  const [mobile, setMobile] = useState(false);
  const [tab, setTab] = useState("edit");
  const [previewCurrent, setPreviewCurrent] = useState(false);
  useEffect(() => {
    const query = window.matchMedia("(max-width: 850px)");
    const update = () => setMobile(query.matches);
    update();
    query.addEventListener("change", update);
    return () => query.removeEventListener("change", update);
  }, []);
  const [open, setOpen] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [message, setMessage] = useState("");
  const [source, setSource] = useState<{ fileName: string; base64: string } | null>(null);
  const [extraction, setExtraction] = useState<Extraction | null>(null);
  const [name, setName] = useState("");
  const [documentType, setDocumentType] = useState(1);
  const [blocks, setBlocks] = useState<Block[]>([]);
  const [tables, setTables] = useState<AddedTable[]>([]);
  const [additions, setAdditions] = useState<AddedContent[]>([]);
  const imageInput = useRef<HTMLInputElement>(null);
  const [tableRows, setTableRows] = useState(2);
  const [tableColumns, setTableColumns] = useState(3);

  const [preview, setPreview] = useState<{ url: string; token: string } | null>(null);
  const [confirmed, setConfirmed] = useState(false);
  const [active, setActive] = useState("");
  const inputs = useRef<Record<string, HTMLTextAreaElement | null>>({});
  useEffect(() => () => { if (preview) URL.revokeObjectURL(preview.url); }, [preview]);
  function invalidate() { setPreviewCurrent(false); setConfirmed(false); setError(""); }
  async function extract(file?: File) {
    if (!file) return;
    invalidate(); setPreview(null); setSource(null); setExtraction(null); setMessage(""); setTables([]); setAdditions([]);
    if (!/\.(doc|docx)$/i.test(file.name) || file.size === 0 || file.size > 10 * 1024 * 1024) {
      setError("Chọn Word .doc hoặc .docx có nội dung, tối đa 10 MB."); return;
    }
    setBusy(true);
    try {
      const base64 = await new Promise<string>((resolve, reject) => {
        const reader = new FileReader();
        reader.onload = () => resolve(String(reader.result).split(",")[1]);
        reader.onerror = () => reject(new Error("Không đọc được file."));
        reader.readAsDataURL(file);
      });
      const original = { fileName: file.name, base64 };
      const result = await internalApi<Extraction>("/contract-templates/word/extract", "POST", original);
      setSource(original); setExtraction(result); setName(result.name); setBlocks(result.blocks);
      setActive("");

      const type = /ĐNTT/i.test(file.name) ? 5 : /BienBan|Biên bản/i.test(file.name) ? 3 : /PLHD|PLHĐ/i.test(file.name) ? 2 : 1;
      setDocumentType(type);
      const pdf = await internalApi<{ pdfBase64: string; previewToken: string }>("/contract-templates/word/preview", "POST", {
        ...original, documentBase64: result.documentBase64, sourceToken: result.sourceToken,
        name: result.name, documentType: type, blocks: result.blocks.map(({ images, ...block }) => { void images; return block; }), fields: [], sampleValues: {}, manualContent: true, tables: [], additions: [],
      });
      setPreview({ url: URL.createObjectURL(wordBlob(pdf.pdfBase64, "application/pdf")), token: pdf.previewToken });
      setPreviewCurrent(true);
    } catch (e) { setError((e as Error).message); } finally { setBusy(false); }
  }
  function payload() {
    return { ...source!, documentBase64: extraction!.documentBase64, sourceToken: extraction!.sourceToken,
      name, documentType, blocks: blocks.map(({ images, ...block }) => { void images; return block; }), fields: [], tables, additions, sampleValues: {}, manualContent: true };
  }
  async function render() {
    setBusy(true); invalidate();
    try {
      const result = await internalApi<{ pdfBase64: string; previewToken: string }>("/contract-templates/word/preview", "POST", payload());
      setPreview({ url: URL.createObjectURL(wordBlob(result.pdfBase64, "application/pdf")), token: result.previewToken }); setPreviewCurrent(true); setTab("preview");
    } catch (e) { setError((e as Error).message); } finally { setBusy(false); }
  }
  async function activate() {
    if (!preview || !previewCurrent || !confirmed) return;
    setBusy(true); setError("");
    try {
      const template = await internalApi<TemplateDefinition>("/contract-templates/word/activate", "POST", { input: payload(), previewToken: preview.token });
      if (documentType === 1) onImported(template);
      setMessage("Đã kích hoạt mẫu " + name + ".");
      setOpen(false); invalidate();
    } catch (e) { setError((e as Error).message); } finally { setBusy(false); }
  }
  const activeBlock = blocks.find(b => b.id === active);
  const headings = blocks.filter(b => b.tableId == null && /^(Điều\s+\d|Phụ lục|HỢP ĐỒNG|BIÊN BẢN)/i.test(b.text.trim()));
  function addTable() {
    if (!activeBlock || !activeBlock.location.startsWith("Nội dung")) return;
    // Insert after the whole existing table when an original table cell is selected.
    const anchor = activeBlock.tableId == null ? activeBlock : blocks.filter(b => b.tableId === activeBlock.tableId).at(-1)!;
    setTables(old => [...old, {...createTable(anchor.id, tableRows, tableColumns), order:nextOrder()}]); invalidate();
  }
  function nextOrder() { return Math.max(0,...tables.map(t => t.order ?? 0),...additions.map(a => a.order)) + 1; }
  function anchorId() { return activeBlock?.tableId == null ? activeBlock?.id : blocks.filter(b => b.tableId === activeBlock.tableId).at(-1)?.id; }
  function addContent(kind: "clause" | "paragraph") {
    const afterBlockId = anchorId(); if (!afterBlockId) return;
    setAdditions(old => [...old,{id:crypto.randomUUID(),afterBlockId,kind,title:"",text:"",widthCm:12,align:"center",order:nextOrder()}]); invalidate();
  }
  async function addImage(file?: File) {
    const afterBlockId = anchorId(); if (!file || !afterBlockId) return;
    if (!["image/png","image/jpeg"].includes(file.type) || file.size > 5*1024*1024 || !file.size) {setError("Chọn ảnh PNG/JPEG, tối đa 5 MB.");return;}
    if (additions.reduce((size,item) => size + (item.imageBase64?.length ?? 0)*0.75, 0) + file.size > 10*1024*1024) {setError("Tổng ảnh chèn thêm tối đa 10 MB.");return;}
    setBusy(true); setError("");
    try {
      const base64 = await new Promise<string>((resolve,reject) => {const reader=new FileReader(); reader.onload=()=>resolve(String(reader.result).split(",")[1]); reader.onerror=()=>reject(new Error("Không đọc được ảnh.")); reader.readAsDataURL(file);});
      setAdditions(old => [...old,{id:crypto.randomUUID(),afterBlockId,kind:"image",title:"",text:"",imageBase64:base64,imageMime:file.type,widthCm:12,align:"center",order:nextOrder()}]); invalidate();
    } catch(e) {setError((e as Error).message);} finally {setBusy(false); if(imageInput.current) imageInput.current.value="";}
  }
  const canInsert = !busy && !!activeBlock?.location.startsWith("Nội dung") && additions.length < 50;
  function insertedAfter(block: Block) {
    return [...tables.filter(t => t.afterBlockId === block.id).map(t => ({order:t.order ?? 0,id:t.id,table:t,content:null})),
      ...additions.filter(a => a.afterBlockId === block.id).map(a => ({order:a.order,id:a.id,table:null,content:a}))]
      .sort((a,b) => a.order-b.order).map(item => item.table ? <WordTableEditor key={item.id} table={item.table} disabled={busy}
        onChange={value => {setTables(old => old.map(t => t.id === value.id ? value : t)); invalidate();}}
        onDelete={() => {setTables(old => old.filter(t => t.id !== item.id)); invalidate();}}/> : <WordContentEditor key={item.id} value={item.content!} disabled={busy}
        onChange={value => {setAdditions(old => old.map(a => a.id === value.id ? value : a)); invalidate();}}
        onDelete={() => {setAdditions(old => old.filter(a => a.id !== item.id)); invalidate();}}/>);
  }
  const editor = <section className={styles.pane} aria-label="Khung chỉnh mẫu">
    <div className={styles.toolbar}>
      <strong>Chỉnh nội dung</strong>
      <select aria-label="Đi tới mục trong tài liệu" className={styles.select} value="" onChange={e => {
        const target = inputs.current[e.target.value]; target?.focus(); target?.scrollIntoView({ block: "center", behavior: "smooth" });
      }}><option value="">Đi tới mục…</option>{headings.map(b => <option value={b.id} key={b.id}>{b.text.slice(0, 100)}</option>)}</select>
      {extraction && <>
        <span className={styles.selection}>Bấm vào nội dung để sửa trực tiếp. Chọn một đoạn để chèn nội dung bên dưới.</span>
        <div className="flex w-full flex-wrap gap-2">
          <Button type="button" variant="outline" disabled={!canInsert} onClick={() => addContent("paragraph")}>Chèn đoạn</Button>
          <Button type="button" variant="outline" disabled={!canInsert} onClick={() => addContent("clause")}>Chèn điều khoản</Button>
          <Button type="button" variant="outline" disabled={!canInsert} onClick={() => imageInput.current?.click()}>Chèn ảnh</Button>
          <input ref={imageInput} type="file" aria-label="Ảnh chèn thêm" accept="image/png,image/jpeg" hidden disabled={!canInsert} onChange={e => void addImage(e.target.files?.[0])}/>
        </div>
        <div className="flex w-full flex-wrap items-center gap-2">
          <label>Hàng <input aria-label="Số hàng bảng mới" className="w-14 rounded border px-1" type="number" min={1} max={50} value={tableRows} disabled={busy} onChange={e => setTableRows(Math.max(1, Math.min(50, Number(e.target.value) || 1)))}/></label>
          <label>Cột <input aria-label="Số cột bảng mới" className="w-14 rounded border px-1" type="number" min={1} max={12} value={tableColumns} disabled={busy} onChange={e => setTableColumns(Math.max(1, Math.min(12, Number(e.target.value) || 1)))}/></label>
          <Button type="button" variant="outline" disabled={busy || tables.length >= 20 || !activeBlock?.location.startsWith("Nội dung")} onClick={addTable}>Chèn bảng sau vị trí chọn</Button>
        </div>
      </>}
    </div>
    <div className={styles.scroll}>
      {extraction ? <div className={styles.paper}><WordDocumentLayout blocks={blocks}
        afterBlock={insertedAfter} renderBlock={block => <>
        {block.images?.map((image,index) => <div key={index}>
          {/* eslint-disable-next-line @next/next/no-img-element */}
          <img src={`data:${image.mime};base64,${image.base64}`} alt="Ảnh có sẵn trong mẫu Word" style={{maxWidth:"100%",height:"auto"}}/>
        </div>)}
        <textarea aria-label={block.text.trim().slice(0,60) || "Nội dung trống"} data-block-id={block.id}
          className={/^(Điều\s+\d|HỢP ĐỒNG|BIÊN BẢN|CỘNG HOÀ|CỘNG HÒA)/i.test(block.text.trim()) ? styles.heading : ""}
          ref={el => {inputs.current[block.id] = el;}} value={block.text} rows={1} maxLength={20000} disabled={busy}
          onFocus={() => setActive(block.id)}
          onChange={e => {setBlocks(old => old.map(b => b.id === block.id ? {...b,text:e.target.value} : b)); invalidate();}}/>
      </>}/></div> : <p className={styles.empty}>Chọn file Word để mở tài liệu.</p>}
    </div>
  </section>;
  const previewPane = <section className={styles.pane} aria-label="Khung xem trước">
    <div className={styles.toolbar}><strong>Bản in từ Word</strong>
      <Button type="button" variant="outline" disabled={!extraction || busy} onClick={() => void render()}>Cập nhật bản xem trước</Button>
      <p className={styles.selection} role="status">{busy ? "Đang dựng bản xem trước…" : previewCurrent ? "Đã cập nhật theo nội dung bên trái." : preview ? "Nội dung đã thay đổi. Bấm cập nhật để xem bản mới trước khi kích hoạt." : "Bố cục, ảnh và đường viền được lấy từ file Word."}</p>
    </div>
    <div className={styles.scroll}>
      {preview ? <><a href={preview.url} target="_blank" rel="noreferrer">Mở PDF</a><WordPdfViewer key={preview.url} url={preview.url}/></> : <p className={styles.empty}>{busy ? "Đang chuyển Word sang bản xem trước…" : extraction ? "Chưa dựng được bản xem trước. Bấm cập nhật để thử lại." : "Chọn file Word để xem bản in tại đây."}</p>}
    </div>
  </section>;
  return <>
    <Button type="button" variant="outline" disabled={disabled} onClick={() => {setOpen(true); setMessage("");}}>Tải mẫu Word</Button>
    {message && <p role="status">{message}</p>}
    <Dialog open={open} onOpenChange={value => {if (!busy) setOpen(value);}}>
      <DialogContent className={styles.dialog} style={{width:"98vw",maxWidth:"98vw",height:"96dvh",display:"flex",overflow:"hidden"}} onInteractOutside={e => e.preventDefault()}>
        <DialogHeader><DialogTitle>Soạn mẫu Word</DialogTitle><DialogDescription>Bên trái sửa nội dung trực tiếp, thêm điều khoản, bảng và ảnh. Bên phải xem bản in giữ bố cục của file Word.</DialogDescription></DialogHeader>
        <fieldset disabled={busy} className={styles.metadata}>
          <label>File Word<Input type="file" accept=".doc,.docx" onChange={e => void extract(e.target.files?.[0])}/></label>
          <label>Tên mẫu<Input value={name} maxLength={500} onChange={e => {setName(e.target.value); invalidate();}}/></label>
          <label>Loại mẫu<select className={styles.select} value={documentType} onChange={e => {setDocumentType(Number(e.target.value)); invalidate();}}>
            {documentTypes.map((label,index) => <option value={index+1} key={label}>{label}</option>)}
          </select></label>
        </fieldset>
        <div className={styles.tabs}><Button type="button" variant={tab==="edit"?"default":"outline"} onClick={() => setTab("edit")}>Chỉnh mẫu</Button><Button type="button" variant={tab==="preview"?"default":"outline"} onClick={() => setTab("preview")}>Xem trước</Button></div>
        <div className={styles.workspace}>{mobile ? <><div className={styles.mobilePane} hidden={tab!=="edit"}>{editor}</div><div className={styles.mobilePane} hidden={tab!=="preview"}>{previewPane}</div></> :
          <PanelGroup direction="horizontal"><Panel defaultSize={50} minSize={30}>{editor}</Panel><PanelResizeHandle className={styles.handle} aria-label="Điều chỉnh chiều rộng hai khung">⋮</PanelResizeHandle><Panel defaultSize={50} minSize={30}>{previewPane}</Panel></PanelGroup>}
        </div>
        {busy && <p role="status">Đang xử lý tài liệu…</p>}
        {error && <p role="alert" className="text-red-600">{error}</p>}
        <div className={styles.footer}>
          {preview && <label><input type="checkbox" checked={confirmed} disabled={busy || !previewCurrent} onChange={e => setConfirmed(e.target.checked)}/> Tôi đã kiểm tra nội dung và bố cục PDF đối chiếu.</label>}
          <Button type="button" variant="outline" disabled={busy} onClick={() => setOpen(false)}>Đóng</Button>
          <Button type="button" disabled={busy || !preview || !previewCurrent || !confirmed} onClick={() => void activate()}>Kích hoạt mẫu</Button>
        </div>
      </DialogContent>
    </Dialog>
  </>;
}

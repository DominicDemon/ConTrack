"use client";
import { draftIssues, focusIssue } from "../../../Shared/draft-validation";
import { PartyEditor, PaymentEditor } from "./draft-details";

import { useCallback, useEffect, useRef, useState, type ReactNode } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { Panel, PanelGroup, PanelResizeHandle } from "react-resizable-panels";
import { ArrowLeft, ArrowDown, ArrowUp, Building2, FileText, GripVertical, ListChecks, Plus, Save, Send, Trash2, Users, Wallet } from "lucide-react";
import { completePartyFields, internalApi, InternalApiError, estimateTotals, money, dateTime, type DraftContent, type DraftRecord, type TemplateDefinition, type CustomerOption, type CustomerInput, type ProductOption, type DraftItem } from "@/services/internal-api";
import { internalStyles as ui } from "./internal-theme";
import DraftPreview from "./draft-preview";
import WordDraftPreview from "./word-draft-preview";
import TemplateUpload from "./template-upload";
import ReviewPanel from "./review-panel";
import s from "./workspace.module.css";
const blank = (): DraftContent => ({ formatVersion: 2, partyType: "", customerBusinessId: null, paymentMode: "PERCENT", milestones: [], templateId: "", customerId: "", name: "", fields: {}, items: [], terms: [], vatPercent: 0, effectiveDate: null, expireDate: null, templateSnapshot: null, totals: { subtotal: 0, discount: 0, vat: 0, total: 0 }, missingFields: [] });
const newCustomer = (): CustomerInput => ({ name: "", phone: "", email: "", company: "", taxCode: "", address: "" });
function Field({ label, children }: { label: string; children: ReactNode }) { return <label className={s.label}>{label}{children}</label>; }
export default function DraftEditor({ id }: { id?: string }) {
  const router = useRouter(); const [content, setContent] = useState<DraftContent>(blank); const [record, setRecord] = useState<DraftRecord | null>(null);
  const [templates, setTemplates] = useState<TemplateDefinition[]>([]); const [customers, setCustomers] = useState<CustomerOption[]>([]); const [products, setProducts] = useState<ProductOption[]>([]);
  const [busy, setBusy] = useState(false); const [loading, setLoading] = useState(true); const [error, setError] = useState(""); const [success, setSuccess] = useState(""); const [conflict, setConflict] = useState(false);
  const [dirty, setDirty] = useState(false); const dirtyRef = useRef(false); const form = useRef<HTMLFormElement>(null);
  const [mobile, setMobile] = useState(false); const [tab, setTab] = useState("edit"); const [customerForm, setCustomerForm] = useState<CustomerInput | null>(null); const [customerBusy, setCustomerBusy] = useState(false);
  const [customerSearch, setCustomerSearch] = useState(""); const [lookupError, setLookupError] = useState(""); const [loadAttempt, setLoadAttempt] = useState(0);
  const [wordInfo, setWordInfo] = useState("");
  const wordReadVersion = useRef(0);
  const retryLoad = (next: (v: number) => number) => { setLoading(true); setError(""); setLoadAttempt(next); };
  useEffect(() => { const mq = window.matchMedia("(max-width: 850px)"); const update = () => setMobile(mq.matches); update(); mq.addEventListener("change", update); return () => mq.removeEventListener("change", update); }, []);
  useEffect(() => { let active = true;
    void Promise.all([internalApi<TemplateDefinition[]>("/contract-templates"), internalApi<CustomerOption[]>("/customers"), internalApi<ProductOption[]>("/products"), id ? internalApi<DraftRecord>(`/contract-drafts/${id}`) : Promise.resolve(null)]).then(([ts, cs, ps, draft]) => {
      if (!active) return; ts = ts.map(completePartyFields); setTemplates(ts); setCustomers(cs); setProducts(ps); setRecord(draft);
      setContent(draft?.content ?? { ...blank(), templateId: ts[0]?.id ?? "", templateSnapshot: ts[0] ?? null, terms: structuredClone(ts[0]?.terms ?? []) });
      setDirty(false); dirtyRef.current = false;
    }).catch(e => { if (active) setError(e.message); }).finally(() => { if (active) setLoading(false); }); return () => { active = false; };
  }, [id, loadAttempt]);
  useEffect(() => { if (id) return; let active = true; const timer = setTimeout(() => {
    void internalApi<CustomerOption[]>(`/customers?search=${encodeURIComponent(customerSearch)}`).then(cs => { if (active) { setCustomers(old => { const selected = old.find(c => c.id === content.customerId); return selected && !cs.some(c => c.id === selected.id) ? [selected, ...cs] : cs; }); setLookupError(""); } }).catch(e => { if (active) setLookupError(e.message); });
  }, 300); return () => { active = false; clearTimeout(timer); }; }, [customerSearch, id, content.customerId]);
  useEffect(() => {
    const unload = (e: BeforeUnloadEvent) => { if (dirtyRef.current) { e.preventDefault(); e.returnValue = ""; } };
    const click = (e: MouseEvent) => { const target = (e.target as HTMLElement).closest("a[href],button[data-leave-workspace]"); if (target && dirtyRef.current && !window.confirm("Bạn có thay đổi chưa lưu. Rời trang và bỏ các thay đổi này?")) { e.preventDefault(); e.stopPropagation(); } };
    // Navigation API handles browser Back/Forward before a same-document traversal.
    // Full page departures remain covered by beforeunload.
    const navigation = (window as Window & { navigation?: EventTarget }).navigation;
    const traverse = (event: Event) => { if ((event as Event & { navigationType?: string }).navigationType === "traverse" && event.cancelable && dirtyRef.current
      && !window.confirm("Bạn có thay đổi chưa lưu. Rời trang và bỏ các thay đổi này?")) event.preventDefault(); };
    navigation?.addEventListener("navigate", traverse);
    window.addEventListener("beforeunload", unload); document.addEventListener("click", click, true);
    return () => { window.removeEventListener("beforeunload", unload); document.removeEventListener("click", click, true); navigation?.removeEventListener("navigate", traverse); };
  }, []);
  const change = useCallback((update: (old: DraftContent) => DraftContent) => { setContent(update); setDirty(true); dirtyRef.current = true; setSuccess(""); }, []);
  const setField = (key: string, value: string) => change(c => ({ ...c, fields: { ...c.fields, [key]: value } }));
  const sourceTemplate = content.templateSnapshot ?? templates.find(t => t.id === content.templateId);
  const template = sourceTemplate && (!record?.locked ? completePartyFields(sourceTemplate) : sourceTemplate);
  const totals = estimateTotals(content);
  const issues = draftIssues({...content, templateSnapshot: template ?? null});
  const missing = issues.map(i => i.message);
  function selectCustomer(customer: CustomerOption) { change(c => ({ ...c, customerId: customer.id, partyType: "", customerBusinessId: null, fields: c.templateSnapshot?.manualWordContent ? { ...customer.fields, ...c.fields } : { ...c.fields, ...customer.fields } })); }
  async function selectWordTemplate(t: TemplateDefinition) {
    const version = ++wordReadVersion.current;
    change(c => ({...c, ...(t.wordStorageKey ? {name:"",fields:{},items:[],effectiveDate:null,expireDate:null,customerId:"",partyType:"",customerBusinessId:null} : {fields:Object.fromEntries(Object.entries(c.fields).filter(([key]) => t.fields.some(f => f.key === key)))}), templateId:String(t.id), templateSnapshot:t, terms:structuredClone(t.terms)}));
    setWordInfo("");
    if (!t.wordStorageKey) return;
    setWordInfo("Đang đọc thông tin từ bản Word đã lưu…");
    try {
      const details = await internalApi<{name:string;fields:Record<string,string>;effectiveDate:string|null;expireDate:string|null;totalAmount:number|null;items:DraftItem[];notes:string[]}>(`/contract-templates/${encodeURIComponent(String(t.id))}/word-details`);
      if (version !== wordReadVersion.current) return;
      change(c => c.templateId !== String(t.id) ? c : {...c,
        name:c.name.trim() ? c.name : details.name,
        effectiveDate:c.effectiveDate || details.effectiveDate, expireDate:c.expireDate || details.expireDate,
        fields:{...c.fields,...Object.fromEntries(Object.entries(details.fields).filter(([key]) => !c.fields[key]?.trim() && t.fields.some(f => f.key === key)))},
        items:c.items.length ? c.items : details.items});
      setWordInfo(`Đã điền thông tin nhận diện từ Word. ${details.totalAmount != null ? `Tổng tiền ghi trong tài liệu: ${money(details.totalAmount)}. ` : ""}Hãy đối chiếu và chọn khách nhận hợp đồng. ${details.notes.join(" ")}`);
    } catch(e) {if(version === wordReadVersion.current) setWordInfo("Chưa đọc được thông tin tự động: " + (e as Error).message);}
  }
  async function createCustomer() { if (!customerForm) return; setCustomerBusy(true); setError(""); try { const c = await internalApi<CustomerOption>("/customers", "POST", customerForm); setCustomers(old => [c, ...old]); selectCustomer(c); setCustomerForm(null); } catch (e) { setError((e as Error).message); } finally { setCustomerBusy(false); } }
  async function save() { if (!form.current?.reportValidity()) return; setBusy(true); setError(""); setSuccess(""); setConflict(false);
    try { const saved = await internalApi<DraftRecord>(id ? `/contract-drafts/${id}` : "/contract-drafts", id ? "PUT" : "POST", { content, rowVersion: record?.rowVersion, versionRowVersion: record?.versionRowVersion });
      setRecord(saved); setContent(saved.content); setDirty(false); dirtyRef.current = false; setSuccess("Đã lưu bản nháp thành công."); if (!id) router.replace(`/workspace/contracts/drafts/${saved.id}`);
    } catch (e) { setError((e as Error).message); if(e instanceof InternalApiError && e.issues.length) focusIssue(e.issues[0].path); setConflict(e instanceof InternalApiError && e.status === 409); } finally { setBusy(false); }
  }
  async function reload() { if (dirty && !window.confirm("Tải lại sẽ bỏ nội dung chưa lưu của bạn. Tiếp tục?")) return; retryLoad(v => v + 1); setConflict(false); }
  function editItem(index: number, patch: Partial<DraftItem>) { change(c => ({ ...c, items: c.items.map((item, i) => i === index ? { ...item, ...patch } : item) })); }
  function moveTerm(index: number, offset: number) { change(c => { const terms = [...c.terms]; [terms[index], terms[index + offset]] = [terms[index + offset], terms[index]]; return { ...c, terms }; }); }
  const readOnly = !!record && !record.canEdit;
  if (loading) return <div className={s.empty}>Đang chuẩn bị không gian soạn thảo…</div>;
  if (!template || (id && !record)) return <div className={s.empty}><p role="alert">{error || "Chưa có mẫu hợp đồng hoạt động. Liên hệ quản trị để khởi tạo mẫu."}</p><button className={ui.secondaryButton} onClick={() => retryLoad(v => v + 1)}>Thử lại</button><Link href="/workspace/contracts/drafts">Về danh sách nháp</Link></div>;
  const editor = <div className={s.formPane}><form ref={form} onSubmit={e => { e.preventDefault(); void save(); }}><fieldset disabled={busy || readOnly} style={{ border: 0, padding: 0, minWidth: 0 }}>
    <section id="contract-info" tabIndex={-1} className={s.section}><h2><FileText/>Thông tin hợp đồng</h2><div className={s.fields}>
      <Field label="Mẫu hợp đồng *"><select required className={ui.field} value={content.templateId} disabled={!!id} onChange={e => { const t = templates.find(t => String(t.id) === e.target.value)!; if (dirty && !window.confirm("Đổi mẫu sẽ thay nội dung theo mẫu mới. Tiếp tục?")) return; void selectWordTemplate(t); }}>{templates.map(t => <option value={t.id} key={t.id}>{t.name}</option>)}{!templates.some(t => t.id === template.id) && <option value={template.id}>{template.name}</option>}</select></Field>
      {!id && <TemplateUpload disabled={busy} onImported={t => { setTemplates(old => [t, ...old]); void selectWordTemplate(t); }}/>}
      {wordInfo && <p role="status" className={s.muted}>{wordInfo}</p>}
      <p className={s.muted}>{template.manualWordContent ? "Thông tin nhận diện từ Word được điền sẵn vào hồ sơ. Bản in giữ nội dung Word đã lưu; hãy đối chiếu trước khi gửi khách." : template.isImported ? "Mẫu tải lên chưa được phê duyệt. Sửa nội dung file tại phần Nội dung hợp đồng; thông tin khách hàng không tự thay vào văn bản." : "Mẫu minh họa, chưa được công ty phê duyệt. Nội dung trống cần được người soạn bổ sung."}</p>
      <Field label="Tên hợp đồng *"><input required maxLength={1000} className={ui.field} placeholder="Ví dụ: Hợp đồng cung cấp phần mềm…" value={content.name} onChange={e => change(c => ({ ...c, name: e.target.value }))}/></Field>
      <div className={s.columns}><Field label="Ngày hiệu lực"><input type="date" className={ui.field} value={content.effectiveDate ?? ""} onChange={e => change(c => ({ ...c, effectiveDate: e.target.value || null }))}/></Field><Field label="Ngày hết hạn"><input type="date" className={ui.field} min={content.effectiveDate ?? undefined} value={content.expireDate ?? ""} onChange={e => change(c => ({ ...c, expireDate: e.target.value || null }))}/></Field></div>
    </div></section>
    <section id="customer-fields" tabIndex={-1} className={s.section}><h2><Users/>Khách hàng · Bên A</h2><div className={s.fields}>
      {!id && <><input className={ui.field} aria-label="Tìm khách hàng" placeholder="Tìm theo tên hoặc số điện thoại…" value={customerSearch} onChange={e => setCustomerSearch(e.target.value)}/>{lookupError && <p role="alert" className={ui.error}>{lookupError}</p>}
        <Field label="Khách hàng *"><select required className={ui.field} value={content.customerId} onChange={e => { const c = customers.find(x => x.id === e.target.value); if (c) selectCustomer(c); }}><option value="">Chọn khách hàng</option>{customers.map(c => <option value={c.id} key={c.id}>{c.name} · {c.phone}</option>)}</select></Field>
        <button type="button" className={ui.secondaryButton} onClick={() => setCustomerForm(customerForm ? null : newCustomer())}><Plus size={16}/>{customerForm ? "Đóng tạo khách hàng" : "Tạo nhanh khách hàng"}</button>
        {customerForm && <div className={s.lineItem}><strong style={{ fontSize: 14 }}>Khách hàng mới</strong>{([["name", "Tên hiển thị *"], ["phone", "Số điện thoại *"], ["email", "Email"], ["company", "Tên doanh nghiệp (nếu có)"], ["taxCode", "Mã số thuế doanh nghiệp"], ["address", "Địa chỉ"]] as const).map(([key, label]) => <Field key={key} label={label}><input className={ui.field} value={customerForm[key]} onChange={e => setCustomerForm({ ...customerForm, [key]: e.target.value })}/></Field>)}<button disabled={customerBusy} type="button" className={ui.primaryButton} onClick={createCustomer}>{customerBusy ? "Đang tạo…" : "Tạo và chọn khách hàng"}</button></div>}
      </>}
      <PartyEditor content={content} change={change}/>{template.fields.filter(f => f.key.startsWith("CUSTOMER_")).map(f => <Field key={f.key} label={f.label + (f.required && !content.fields[f.key]?.trim() ? " · cần hoàn thiện" : "")}><input maxLength={4000} className={ui.field} value={content.fields[f.key] ?? ""} onChange={e => setField(f.key, e.target.value)}/></Field>)}
    </div></section>
    {<section id="provider-fields" tabIndex={-1} className={s.section}><h2><Building2/>Công ty mình · Bên B</h2><div className={s.fields}>{template.fields.filter(f => f.key.startsWith("PROVIDER_")).map(f => <Field key={f.key} label={f.label + (f.required && !content.fields[f.key]?.trim() ? " · cần hoàn thiện" : "")}><input maxLength={4000} className={ui.field} value={content.fields[f.key] ?? ""} onChange={e => setField(f.key, e.target.value)}/></Field>)}</div></section>}
    <section id="contract-items" tabIndex={-1} className={s.section}><h2><Wallet/>Hạng mục và giá trị</h2>
      {content.items.map((item, index) => <div key={index} className={s.lineItem}><header><strong style={{ fontSize: 12 }}>Hạng mục {index + 1}</strong><button type="button" className={s.iconButton} aria-label={`Xóa hạng mục ${index + 1}`} onClick={() => change(c => ({ ...c, items: c.items.filter((_, i) => i !== index) }))}><Trash2 size={16}/></button></header>
        <Field label="Tên sản phẩm / dịch vụ"><input required maxLength={500} className={ui.field} value={item.name} onChange={e => editItem(index, { name: e.target.value })}/></Field>
        <div className={s.columns}>{([["quantity", "Số lượng", "0.0001"], ["unitPrice", "Đơn giá (VND)", "0.01"], ["discountPercent", "Giảm giá (%)", "0.01"], ["discountAmount", "Hoặc giảm số tiền (VND)", "0.01"]] as const).map(([key, label, step]) => <Field key={key} label={label}><input type="number" min={key === "quantity" ? "0.0001" : "0"} max={key === "discountPercent" ? "100" : undefined} step={step} className={ui.field} value={item[key]} onChange={e => editItem(index, { [key]: e.target.valueAsNumber || 0, ...(key === "discountPercent" ? { discountAmount: 0 } : key === "discountAmount" ? { discountPercent: 0 } : {}) })}/></Field>)}</div>
      </div>)}
      <div className={s.fields}><button type="button" className={ui.secondaryButton} onClick={() => change(c => ({ ...c, items: [...c.items, { productId: null, name: "", quantity: 1, unitPrice: 0, discountPercent: 0, discountAmount: 0 }] }))}><Plus size={16}/>Thêm hạng mục</button>
        {products.length > 0 && <select className={ui.field} aria-label="Thêm sản phẩm từ danh mục" value="" onChange={e => { const p = products.find(p => p.id === e.target.value); if (p) change(c => ({ ...c, items: [...c.items, { productId: p.id, name: p.name, quantity: 1, unitPrice: p.price, discountPercent: 0, discountAmount: 0 }] })); }}><option value="">Hoặc chọn sản phẩm trong danh mục…</option>{products.map(p => <option value={p.id} key={p.id}>{p.name} — {money(p.price)} ₫</option>)}</select>}
        <Field label="VAT chung của hợp đồng (%)"><input type="number" min="0" max="100" step="0.01" className={ui.field} value={content.vatPercent} onChange={e => change(c => ({ ...c, vatPercent: e.target.valueAsNumber || 0 }))}/></Field>
      </div><div className={s.totals}><div><span>Tạm tính</span><span>{money(totals.subtotal)} ₫</span></div><div><span>Giảm giá</span><span>{money(totals.discount)} ₫</span></div><div><span>VAT</span><span>{money(totals.vat)} ₫</span></div><div><span>Tổng giá trị</span><span>{money(totals.total)} ₫</span></div></div>
    </section>
    <section id="contract-terms" tabIndex={-1} className={s.section}><h2><ListChecks/>{template.isImported ? "Nội dung hợp đồng" : "Điều khoản hợp đồng"}</h2>{!template.isImported && <Field label="Căn cứ hợp đồng"><textarea className={`${ui.field} ${s.termContent}`} maxLength={4000} value={content.fields.CONTRACT_LEGAL_BASES ?? ""} onChange={e => setField("CONTRACT_LEGAL_BASES", e.target.value)}/></Field>}
      <div style={{ marginTop: 18 }}>{content.terms.map((term, index) => <div className={s.lineItem} key={term.key}><header><strong style={{ fontSize: 12 }}>Điều {index + 1}{!term.editable ? " · Cố định theo mẫu" : ""}</strong><div><button type="button" className={s.iconButton} disabled={!!content.templateSnapshot?.wordStorageKey || index === 0} aria-label={`Đưa điều ${index + 1} lên`} onClick={() => moveTerm(index, -1)}><ArrowUp size={15}/></button><button type="button" className={s.iconButton} disabled={!!content.templateSnapshot?.wordStorageKey || index === content.terms.length - 1} aria-label={`Đưa điều ${index + 1} xuống`} onClick={() => moveTerm(index, 1)}><ArrowDown size={15}/></button><button type="button" className={s.iconButton} disabled={!term.editable} aria-label={`Xóa điều ${index + 1}`} onClick={() => change(c => ({ ...c, terms: c.terms.filter(t => t.key !== term.key) }))}><Trash2 size={15}/></button></div></header>
        <Field label="Tiêu đề điều khoản"><input required disabled={!term.editable} maxLength={500} className={ui.field} value={term.title} onChange={e => change(c => ({ ...c, terms: c.terms.map(t => t.key === term.key ? { ...t, title: e.target.value } : t) }))}/></Field>
        <Field label="Nội dung"><textarea disabled={!term.editable} maxLength={20000} className={`${ui.field} ${s.termContent}`} placeholder="Nhập nội dung điều khoản…" value={term.content} onChange={e => change(c => ({ ...c, terms: c.terms.map(t => t.key === term.key ? { ...t, content: e.target.value } : t) }))}/></Field>
      </div>)}</div><button type="button" disabled={!!content.templateSnapshot?.wordStorageKey} className={ui.secondaryButton} onClick={() => change(c => ({ ...c, terms: [...c.terms, { key: crypto.randomUUID(), title: "Điều khoản bổ sung", content: "", editable: true }] }))}><Plus size={16}/>Thêm điều khoản</button>
    </section><PaymentEditor content={content} change={change}/><section className={s.section}><h2>Thông tin bổ sung theo mẫu</h2>{template.fields.filter(f => !f.key.startsWith("CUSTOMER_") && !f.key.startsWith("PROVIDER_") && f.key !== "CONTRACT_LEGAL_BASES").map(f => <Field key={f.key} label={f.label + (f.required ? " *" : "")} >{f.dataType === "SELECT" ? <select className={ui.field} value={content.fields[f.key] ?? ""} onChange={e => setField(f.key,e.target.value)}><option value="">Chọn giá trị</option>{f.options?.map(o => <option key={o}>{o}</option>)}</select> : <input className={ui.field} type={f.dataType === "DATE" ? "date" : f.dataType === "NUMBER" ? "number" : "text"} step="any" min={f.min ?? undefined} max={f.max ?? undefined} value={content.fields[f.key] ?? ""} onChange={e => setField(f.key,e.target.value)}/>}</Field>)}</section><section className={ui.notice}><strong>Nháp có thể lưu khi chưa hoàn thiện</strong>{issues.length > 0 && <ul>{issues.map((issue,i) => <li key={i}><button type="button" className={ui.secondaryButton} onClick={() => focusIssue(issue.path)}>{issue.message}</button></li>)}</ul>}<p className={s.muted} style={{ marginTop: 6 }}>{missing.length ? `Cần bổ sung: ${missing.join(", ")}.` : "Đã đủ các trường thông tin của mẫu. Hợp đồng vẫn là bản nháp, chưa được duyệt hoặc ký."}</p></section>
  </fieldset></form></div>;
  return <><div className={s.editorHeader}><Link href="/workspace/contracts/drafts" className={s.iconButton} aria-label="Về danh sách"><ArrowLeft size={20}/></Link><div><h1>{readOnly ? "Xem hợp đồng" : id ? "Chỉnh sửa hợp đồng nháp" : "Tạo hợp đồng nháp"}</h1><p className={s.muted}>{record ? `${record.employeeName} · Cập nhật ${dateTime(record.updatedAt)}` : "Bắt đầu từ mẫu, hoàn thiện theo nhu cầu"}</p></div><div className={s.actions}><span className={`${s.badge} ${dirty ? s.warning : ""}`}>{dirty ? "Chưa lưu thay đổi" : record ? "Đã lưu" : "Nháp mới"}</span>{!readOnly && <button disabled={busy || customerBusy} className={ui.primaryButton} onClick={save}><Save size={17}/>{busy ? "Đang lưu…" : "Lưu nháp"}</button>}</div></div>
    {(error || success) && <div className={s.messages}>{error && <div role="alert" className={ui.error}>{error}{conflict && <button className={ui.secondaryButton} style={{ marginLeft: 12 }} onClick={reload}>Tải lại bản mới</button>}</div>}{success && <div role="status" className={ui.notice}>{success}</div>}</div>}
    {!record && <div className={s.messages}><section className={ui.card} style={{ display: "flex", flexWrap: "wrap", alignItems: "center", gap: 12 }} aria-label="Gửi khách xem"><button type="button" className={ui.primaryButton} disabled aria-describedby="new-draft-send-guidance"><Send size={16}/>Gửi khách xem</button><p id="new-draft-send-guidance" className={s.muted}>Lưu nháp trước, sau đó hoàn thiện các mục còn thiếu để gửi khách xem.</p></section></div>}
    {record && <div className={s.messages}><ReviewPanel record={record} dirty={dirty} onChanged={() => retryLoad(v => v + 1)}/></div>}
    <div className={s.mobileTabs}><button type="button" className={tab === "edit" ? ui.primaryButton : ui.secondaryButton} onClick={() => setTab("edit")}>Soạn thảo</button><button type="button" className={tab === "preview" ? ui.primaryButton : ui.secondaryButton} onClick={() => setTab("preview")}>Xem trước</button></div>
    <div className={s.editorBody}>{mobile ? <><div hidden={tab !== "edit"}>{editor}</div><div hidden={tab !== "preview"}>{content.templateSnapshot?.wordStorageKey ? <WordDraftPreview content={content} draftId={record?.id}/> : <DraftPreview content={content} code={record?.code}/>}</div></> : <PanelGroup direction="horizontal"><Panel defaultSize={48} minSize={30}>{editor}</Panel><PanelResizeHandle aria-label="Điều chỉnh chiều rộng vùng soạn thảo" style={{ width: 10, display: "grid", placeItems: "center", background: "var(--secondary)", color: "var(--primary)" }}><GripVertical size={14}/></PanelResizeHandle><Panel defaultSize={52} minSize={30}>{content.templateSnapshot?.wordStorageKey ? <WordDraftPreview content={content} draftId={record?.id}/> : <DraftPreview content={content} code={record?.code}/>}</Panel></PanelGroup>}</div>
  </>;
}

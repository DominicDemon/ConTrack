"use client";
import { isGroupedWordSection, wordDisplaySections } from "../../../Shared/word-display-sections";
import { sendIssues, focusIssue } from "../../../Shared/draft-validation";
import { onNotificationRefresh } from "../../../Shared/notification-navigation";
import { PartyEditor, PaymentEditor } from "./draft-details";
import { keepInView } from "./word-clause-form";
import NumberRule from "./number-rule";

import { Fragment, useCallback, useEffect, useRef, useState, type ReactNode } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { Panel, PanelGroup, PanelResizeHandle } from "react-resizable-panels";
import { ArrowLeft, ArrowDown, ArrowUp, Building2, FileText, GripVertical, ListChecks, Plus, RefreshCw, Save, Send, Trash2, Users, Wallet } from "lucide-react";
import { completePartyFields, internalApi, InternalApiError, estimateTotals, money, dateTime, type DraftContent, type DraftRecord, type TemplateDefinition, type CustomerOption, type CustomerLookupResult, type CustomerInput, type ProductOption, type DraftItem } from "@/services/internal-api";
import { internalStyles as ui } from "./internal-theme";
import DraftPreview from "./draft-preview";
import WordDraftPreview from "./word-draft-preview";
import TemplateUpload from "./template-upload";
import ReviewPanel from "./review-panel";
import ChecklistPanel from "./checklist-panel";
import ApprovalPanel from "./approval-panel";
import { useInternalUser } from "./internal-shell";
import s from "./workspace.module.css";
const blank = (): DraftContent => ({ formatVersion: 2, partyType: "", customerBusinessId: null, paymentMode: "PERCENT", milestones: [], templateId: "", customerId: "", name: "", fields: {}, items: [], terms: [], vatPercent: 0, effectiveDate: null, expireDate: null, templateSnapshot: null, totals: { subtotal: 0, discount: 0, vat: 0, total: 0 }, missingFields: [] });
const newCustomer = (): CustomerInput => ({ name: "", phone: "", email: "", company: "", taxCode: "", address: "" });
/** Same list as InternalDraftPolicy.EnglishFieldKeys: what reads differently in English; numbers and codes are shared. */
const ENGLISH_FIELDS = ["CUSTOMER_COMPANY", "CUSTOMER_ADDRESS", "CUSTOMER_CONTACT_TITLE", "CUSTOMER_BANK_NAME", "PROVIDER_LEGAL_NAME", "PROVIDER_ADDRESS", "PROVIDER_REPRESENTATIVE_TITLE", "PROVIDER_BANK_NAME"];
function Field({ label, children }: { label: string; children: ReactNode }) { return <label className={s.label}>{label}{children}</label>; }
export default function DraftEditor({ id }: { id?: string }) {
  const user = useInternalUser(); const router = useRouter(); const [content, setContent] = useState<DraftContent>(blank); const [record, setRecord] = useState<DraftRecord | null>(null);
  const [templates, setTemplates] = useState<TemplateDefinition[]>([]); const [customers, setCustomers] = useState<CustomerLookupResult[]>([]); const [products, setProducts] = useState<ProductOption[]>([]);
  const [busy, setBusy] = useState(false); const [loading, setLoading] = useState(true); const [error, setError] = useState(""); const [success, setSuccess] = useState(""); const [conflict, setConflict] = useState(false);
  const [dirty, setDirty] = useState(false); const dirtyRef = useRef(false); const form = useRef<HTMLFormElement>(null);
  const [mobile, setMobile] = useState(false); const [tab, setTab] = useState("edit"); const [customerForm, setCustomerForm] = useState<CustomerInput | null>(null); const [customerBusy, setCustomerBusy] = useState(false);
  const [customerSearch, setCustomerSearch] = useState(""); const [searching, setSearching] = useState(false); const [lookupError, setLookupError] = useState(""); const [loadAttempt, setLoadAttempt] = useState(0);
  const [locate, setLocate] = useState<{location:string|null;request:number}|null>(null);
  const [wordInfo, setWordInfo] = useState("");
  /** Result of refilling Bên A from the customer record (shown in the Bên A section). */
  const [customerNotice, setCustomerNotice] = useState("");
  const refilled = useRef("");
  const receipt = useRef<string|null|undefined>(undefined);
  const [previewHost,setPreviewHost] = useState<HTMLDivElement|null>(null);
  const wordReadVersion = useRef(0);
  const retryLoad = (next: (v: number) => number) => { setLoading(true); setError(""); setLoadAttempt(next); };
  useEffect(() => { const mq = window.matchMedia("(max-width: 850px)"); const update = () => setMobile(mq.matches); update(); mq.addEventListener("change", update); return () => mq.removeEventListener("change", update); }, []);
  useEffect(() => { let active = true;
    void Promise.all([internalApi<TemplateDefinition[]>("/contract-templates"), internalApi<ProductOption[]>("/products"), id ? internalApi<DraftRecord>(`/contract-drafts/${id}`) : Promise.resolve(null)]).then(([ts, ps, draft]) => {
      if (!active) return; ts = ts.map(completePartyFields); setTemplates(ts); setProducts(ps); setRecord(draft);
      setContent(draft?.content ?? { ...blank(), templateId: ts[0]?.id ?? "", templateSnapshot: ts[0] ?? null, terms: structuredClone(ts[0]?.terms ?? []) });
      receipt.current = undefined; setDirty(false); dirtyRef.current = false;
    }).catch(e => { if (active) setError(e.message); }).finally(() => { if (active) setLoading(false); }); return () => { active = false; };
  }, [id, loadAttempt]);
  useEffect(() => {
    let generation = 0;
    const unsubscribe = onNotificationRefresh(() => {
      if (!id || !record || busy || loading || dirtyRef.current) return;
      const request = ++generation;
      void internalApi<DraftRecord>(`/contract-drafts/${id}`).then(latest => {
        if (request !== generation || dirtyRef.current) return;
        setRecord(latest);
        if (latest.rowVersion !== record.rowVersion || latest.versionRowVersion !== record.versionRowVersion) {
          receipt.current = undefined;
          setContent(latest.content);
        }
      }).catch(e => { if (request === generation) setError(e.message); });
    });
    return () => { generation++; unsubscribe(); };
  }, [id, record, busy, loading]);
  useEffect(() => {
    const search = customerSearch.trim();
    if (id || !search) return;
    let active = true;
    const timer = setTimeout(() => {
      void internalApi<CustomerLookupResult[]>(`/customers/lookup?search=${encodeURIComponent(search)}`)
        .then(results => { if (active) setCustomers(results); })
        .catch(error => { if (active) setLookupError(error.message); })
        .finally(() => { if (active) setSearching(false); });
    }, 300);
    return () => { active = false; clearTimeout(timer); };
  }, [customerSearch, id]);
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
  const change = useCallback((update: (old: DraftContent) => DraftContent) => {
    if (receipt.current !== undefined) receipt.current = null;
    setContent(old => {
      const next = update(old);
      return old.customerId !== next.customerId || old.customerBusinessId !== next.customerBusinessId || old.partyType !== next.partyType
        ? { ...next, numberingBusinessId: null } : next;
    });
    setDirty(true); dirtyRef.current = true; setSuccess("");
  }, []);
  const setField = (key: string, value: string) => change(c => ({ ...c, fields: { ...c.fields, [key]: value } }));
  const bilingual = content.languageMode === "VI_EN";
  const setTerm = (key: string, patch: Partial<DraftContent["terms"][number]>) => change(c => ({ ...c, terms: c.terms.map(t => t.key === key ? { ...t, ...patch } : t) }));
  // A party field, plus its English wording for a bilingual contract (names, addresses, positions, banks).
  const partyField = (f: TemplateDefinition["fields"][number]) => <Fragment key={f.key}>
    <Field label={f.label + (f.required && !content.fields[f.key]?.trim() ? " · cần hoàn thiện" : "")}><input maxLength={4000} className={ui.field} value={content.fields[f.key] ?? ""} onChange={e => setField(f.key, e.target.value)}/></Field>
    {bilingual && ENGLISH_FIELDS.includes(f.key) && <Field label={`${f.label} (tiếng Anh)`}><input maxLength={4000} className={ui.field} lang="en" value={content.fieldsEn?.[f.key] ?? ""} onChange={e => { const value = e.target.value; change(c => ({ ...c, fieldsEn: { ...c.fieldsEn, [f.key]: value } })); }}/></Field>}
  </Fragment>;
  const sourceTemplate = content.templateSnapshot ?? templates.find(t => t.id === content.templateId);
  const template = sourceTemplate && (!record?.locked ? completePartyFields(sourceTemplate) : sourceTemplate);
  const totals = estimateTotals(content);
  const issues = sendIssues({...content, templateSnapshot: template ?? null});
  const missing = issues.map(i => i.message);
  function selectCustomer(customer: CustomerOption, businessId: string | null = null) {
    change(current => ({
      ...current, customerId: customer.id, partyType: businessId ? "BUSINESS" : "PERSON", customerBusinessId: businessId,
      fields: { ...Object.fromEntries(Object.entries(current.fields).filter(([key]) => !key.startsWith("CUSTOMER_"))), ...customer.fields },
      fieldsEn: current.fieldsEn && Object.fromEntries(Object.entries(current.fieldsEn).filter(([key]) => !key.startsWith("CUSTOMER_")))
    }));
    setCustomerSearch("");
    setCustomers([]);
    setLookupError("");
    setSearching(false);
  }
  /**
   * Bên A of a saved, editable version refilled from the customer record (the person or the legal entity of the draft).
   * A new version copies Bên A from the previous one, which may be outdated or wrongly read from the Word file.
   */
  async function refillCustomer(auto: boolean) {
    if (!content.customerId) return;
    try {
      const profile = await internalApi<CustomerLookupResult>(`/customers/${encodeURIComponent(content.customerId)}/profile`
        + (content.customerBusinessId ? `?businessId=${encodeURIComponent(content.customerBusinessId)}` : ""));
      if (!Object.entries(profile.customer.fields).some(([key, value]) => (content.fields[key] ?? "") !== (value ?? ""))) {
        if (!auto) setCustomerNotice("Thông tin Bên A đã khớp với hồ sơ khách hàng.");
        return;
      }
      selectCustomer(profile.customer, profile.businessId);
      setCustomerNotice((auto ? "Đã điền lại" : "Đã lấy lại") + " thông tin Bên A theo hồ sơ khách hàng (cả trong nội dung Word). Kiểm tra rồi bấm Lưu nháp.");
    } catch (e) { if (!auto) setCustomerNotice("Chưa lấy được thông tin khách hàng: " + (e as Error).message); }
  }
  // Opening an editable version (e.g. the one created after the customer asked for changes) refills Bên A once.
  useEffect(() => {
    if (!record?.canEdit || !(user.canWrite || user.canCheck) || dirtyRef.current) return;
    const key = `${record.id}:${record.versionNo}`;
    if (refilled.current === key) return;
    refilled.current = key;
    void refillCustomer(true);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [record]);
  async function selectWordTemplate(t: TemplateDefinition) {
    receipt.current = undefined;
    const version = ++wordReadVersion.current;
    change(c => ({...c, ...(t.wordStorageKey ? {name:"",fields:Object.fromEntries(Object.entries(c.fields).filter(([key]) => c.customerId && key.startsWith("CUSTOMER_"))),items:[],effectiveDate:null,expireDate:null} : {fields:Object.fromEntries(Object.entries(c.fields).filter(([key]) => t.fields.some(f => f.key === key)))}), templateId:String(t.id), templateSnapshot:t, terms:structuredClone(t.terms)}));
    setWordInfo("");
    if (!t.wordStorageKey) return;
    setWordInfo("Đang đọc thông tin từ bản Word đã lưu…");
    try {
      const details = await internalApi<{name:string;fields:Record<string,string>;effectiveDate:string|null;expireDate:string|null;totalAmount:number|null;items:DraftItem[];notes:string[]}>(`/contract-templates/${encodeURIComponent(String(t.id))}/word-details`);
      if (version !== wordReadVersion.current) return;
      change(c => c.templateId !== String(t.id) ? c : {...c,
        name:c.name.trim() ? c.name : details.name,
        effectiveDate:c.effectiveDate || details.effectiveDate, expireDate:c.expireDate || details.expireDate,
        fields:{...c.fields,...Object.fromEntries(Object.entries(details.fields).filter(([key]) => !(c.customerId && key.startsWith("CUSTOMER_")) && !c.fields[key]?.trim() && t.fields.some(f => f.key === key)))},
        items:c.items.length ? c.items : details.items});
      setWordInfo(`Đã điền thông tin nhận diện từ Word. ${details.totalAmount != null ? `Tổng tiền ghi trong tài liệu: ${money(details.totalAmount)}. ` : ""}Hãy đối chiếu và chọn khách nhận hợp đồng. ${details.notes.join(" ")}`);
    } catch(e) {if(version === wordReadVersion.current) setWordInfo("Chưa đọc được thông tin tự động: " + (e as Error).message);}
  }
  async function createCustomer() {
    if (!customerForm) return;
    setCustomerBusy(true);
    setError("");
    try {
      const customer = await internalApi<CustomerOption>("/customers", "POST", customerForm);
      setCustomerForm(null);
      selectCustomer(customer);
      if (customerForm.company.trim()) {
        const results = await internalApi<CustomerLookupResult[]>(`/customers/lookup?search=${encodeURIComponent(customerForm.taxCode)}`);
        const business = results.find(result => result.customer.id === customer.id && result.businessId);
        if (business) selectCustomer(business.customer, business.businessId);
      }
    } catch (error) {
      setError((error as Error).message);
    } finally {
      setCustomerBusy(false);
    }
  }
  async function save() { if (!form.current?.reportValidity()) return;
    if (!content.customerId) { setError("Tìm và chọn khách hàng trước khi lưu nháp."); focusIssue("partyType"); return; } setBusy(true); setError(""); setSuccess(""); setConflict(false);
    try { if (receipt.current === null) throw new Error("Cập nhật bản xem trước Word trước khi lưu nháp.");
      const saved = await internalApi<DraftRecord>(id ? `/contract-drafts/${id}` : "/contract-drafts", id ? "PUT" : "POST", { content, wordPreviewToken:receipt.current, rowVersion: record?.rowVersion, versionRowVersion: record?.versionRowVersion });
      receipt.current = undefined; setRecord(saved); setContent(saved.content); setDirty(false); dirtyRef.current = false; setSuccess("Đã lưu bản nháp thành công."); if (!id) router.replace(`/workspace/contracts/drafts/${saved.id}`);
    } catch (e) { setError((e as Error).message); if(e instanceof InternalApiError && e.issues.length) focusIssue(e.issues[0].path); setConflict(e instanceof InternalApiError && e.status === 409); } finally { setBusy(false); }
  }
  async function reload() { if (dirty && !window.confirm("Tải lại sẽ bỏ nội dung chưa lưu của bạn. Tiếp tục?")) return; retryLoad(v => v + 1); setConflict(false); }
  function editItem(index: number, patch: Partial<DraftItem>) { change(c => ({ ...c, items: c.items.map((item, i) => i === index ? { ...item, ...patch } : item) })); }
  function moveTerm(index: number, offset: number) { keepInView(`contract-term-${content.terms[index].key}`, offset < 0 ? `Đưa điều ${index + offset + 1} lên` : `Đưa điều ${index + offset + 1} xuống`); change(c => { const terms = [...c.terms]; [terms[index], terms[index + offset]] = [terms[index + offset], terms[index]]; return { ...c, terms }; }); }
  const readOnly = !user.canWrite && !user.canCheck || !!record && !record.canEdit;
  if (loading) return <div className={s.empty}>Đang chuẩn bị không gian soạn thảo…</div>;
  if (!template || (id && !record)) return <div className={s.empty}><p role="alert">{error || "Chưa có mẫu hợp đồng hoạt động. Liên hệ quản trị để khởi tạo mẫu."}</p><button className={ui.secondaryButton} onClick={() => retryLoad(v => v + 1)}>Thử lại</button><Link href="/workspace/contracts/drafts">Về danh sách nháp</Link></div>;
  const canEditTerms = user.canCheck || (user.canWrite && (!record || String(record.employeeId) === String(user.id)));
  const showWordSections = !!template.wordStorageKey || content.terms.some(isGroupedWordSection);
  const inlineWord = !!template.wordStorageKey && canEditTerms && !readOnly;
  const editor = <div className={s.formPane}><form ref={form} onSubmit={e => { e.preventDefault(); void save(); }}><fieldset disabled={busy || readOnly} style={{ border: 0, padding: 0, minWidth: 0 }}>
    <section id="contract-info" tabIndex={-1} className={s.section}><h2><FileText/>Thông tin hợp đồng</h2><div className={s.fields}>
      <Field label="Mẫu hợp đồng *"><select required className={ui.field} value={content.templateId} disabled={!!id} onChange={e => { const t = templates.find(t => String(t.id) === e.target.value)!; if (dirty && !window.confirm("Đổi mẫu sẽ thay nội dung theo mẫu mới. Tiếp tục?")) return; void selectWordTemplate(t); }}>{templates.map(t => <option value={t.id} key={t.id}>{t.name}</option>)}{!templates.some(t => t.id === template.id) && <option value={template.id}>{template.name}</option>}</select></Field>
      {!id && user.canManageTemplates && <TemplateUpload disabled={busy} onImported={t => { setTemplates(old => [t, ...old]); if (!dirty || window.confirm("Dùng mẫu vừa tải và bỏ thay đổi đang soạn?")) void selectWordTemplate(t); }}/>}
      {wordInfo && <p role="status" className={s.muted}>{wordInfo}</p>}
      <p className={s.muted}>{template.manualWordContent ? "Đối chiếu thông tin đã điền từ Word. Bạn có thể sửa văn bản, bảng và ảnh trong phần Nội dung hợp đồng." : template.isImported ? "Mẫu tải lên chưa được phê duyệt. Sửa nội dung file tại phần Nội dung hợp đồng; thông tin khách hàng không tự thay vào văn bản." : "Mẫu minh họa, chưa được công ty phê duyệt. Nội dung trống cần được người soạn bổ sung."}</p>
      <Field label="Ngôn ngữ hợp đồng"><select className={ui.field} value={bilingual ? "VI_EN" : "VI"} disabled={!!template.wordStorageKey} title={template.wordStorageKey ? "Mẫu Word giữ nguyên nội dung của file" : undefined}
        onChange={e => { const mode = e.target.value === "VI_EN" ? "VI_EN" : null; change(c => ({ ...c, languageMode: mode })); }}>
        <option value="VI">Tiếng Việt</option><option value="VI_EN">Song ngữ Anh – Việt (tiếng Anh trước, tiếng Việt in nghiêng)</option></select></Field>
      <Field label="Tên hợp đồng *"><input required maxLength={1000} className={ui.field} placeholder="Ví dụ: Hợp đồng cung cấp phần mềm…" value={content.name} onChange={e => change(c => ({ ...c, name: e.target.value }))}/></Field>
      <Field label="Số hợp đồng"><p>{content.contractNumber || record?.code || "Cấp khi lưu hợp đồng mới"}</p>{template.wordStorageKey && <span className={s.muted} style={{ fontWeight: 400 }}>Hệ thống tự sinh và tự điền vào dòng “Số: …” đầu văn bản; không cần nhập tay.</span>}</Field>
      <Field label="Phương thức thanh toán"><select className={ui.field} value={content.paymentMethod ?? ""} onChange={e => change(c => ({ ...c, paymentMethod: (e.target.value || null) as DraftContent["paymentMethod"] }))}>
        <option value="">Chọn phương thức</option>
        <option value="BANK_TRANSFER">Chuyển khoản</option>
        <option value="CASH">Tiền mặt</option>
        <option value="CASH_OR_TRANSFER">Tiền mặt hoặc chuyển khoản</option>
      </select></Field>
      {bilingual && <Field label="Tên hợp đồng tiếng Anh *"><input maxLength={1000} className={ui.field} lang="en" placeholder="Ví dụ: Supply and installation contract" value={content.nameEn ?? ""} onChange={e => { const value = e.target.value; change(c => ({ ...c, nameEn: value })); }}/></Field>}
      <div className={s.columns}><Field label="Ngày hiệu lực"><input type="date" className={ui.field} value={content.effectiveDate ?? ""} onChange={e => change(c => ({ ...c, effectiveDate: e.target.value || null }))}/></Field><Field label="Ngày hết hạn *"><input required aria-label="Ngày hết hạn" type="date" className={ui.field} min={content.effectiveDate ?? undefined} value={content.expireDate ?? ""} onChange={e => change(c => ({ ...c, expireDate: e.target.value || null }))}/></Field></div>
    </div></section>
    <section id="customer-fields" tabIndex={-1} className={s.section}><h2><Users/>Khách hàng · Bên A</h2><div className={s.fields}>
      {!id && <><Field label="Tìm khách hàng"><input type="search" className={ui.field} placeholder="Nhập email, mã số thuế hoặc số điện thoại…" value={customerSearch}
          onChange={event => {
            setCustomerSearch(event.target.value);
            setCustomers([]);
            setLookupError("");
            setSearching(Boolean(event.target.value.trim()));
          }}/></Field>
        {content.customerId && <p role="status">Đã chọn {content.partyType === "BUSINESS" ? content.fields.CUSTOMER_COMPANY : content.fields.CUSTOMER_CONTACT_NAME}</p>}
        {!customerSearch.trim() && !content.customerId && <p className={s.muted}>Nhập email, mã số thuế hoặc số điện thoại để tìm khách có sẵn.</p>}
        {lookupError ? <p role="alert" className={ui.error}>{lookupError}</p> : searching ?
          <p role="status">Đang tìm khách hàng…</p> : customerSearch.trim() && (customers.length ?
          <ul className={s.fields} aria-label="Kết quả tìm khách hàng">{customers.map(result => <li key={result.businessId ? `business-${result.businessId}` : `customer-${result.customer.id}`}>
            <button type="button" className={ui.secondaryButton} onClick={() => selectCustomer(result.customer, result.businessId)}>
              {result.businessName || result.customer.name} · {result.businessId ? "Doanh nghiệp" : "Cá nhân"}
              {["CUSTOMER_TAX_CODE", "CUSTOMER_CONTACT_PHONE", "CUSTOMER_EMAIL"].map(key => result.customer.fields[key]).filter(Boolean).map((value, index) => <span key={index}> · {value}</span>)}
            </button>
          </li>)}</ul> : <p role="status">Không tìm thấy khách hàng phù hợp.</p>)}
        <button type="button" className={ui.secondaryButton} onClick={() => setCustomerForm(customerForm ? null : newCustomer())}><Plus size={16}/>{customerForm ? "Đóng tạo khách hàng" : "Tạo nhanh khách hàng"}</button>
        {customerForm && <div className={s.lineItem}><strong style={{ fontSize: 14 }}>Khách hàng mới</strong>{([["name", "Tên hiển thị *"], ["phone", "Số điện thoại *"], ["email", "Email"], ["company", "Tên doanh nghiệp (nếu có)"], ["taxCode", "Mã số thuế doanh nghiệp"], ["address", "Địa chỉ"]] as const).map(([key, label]) => <Field key={key} label={label}><input className={ui.field} value={customerForm[key]} onChange={e => setCustomerForm({ ...customerForm, [key]: e.target.value })}/></Field>)}<button disabled={customerBusy} type="button" className={ui.primaryButton} onClick={createCustomer}>{customerBusy ? "Đang tạo…" : "Tạo và chọn khách hàng"}</button></div>}
      </>}
      {id && record?.canEdit && !!content.customerId && <button type="button" className={ui.secondaryButton} onClick={() => void refillCustomer(false)}><RefreshCw size={16}/>Lấy lại thông tin khách hàng</button>}
      {customerNotice && <p role="status" className={ui.notice}>{customerNotice}</p>}
      <PartyEditor content={content} change={change}/>{template.fields.filter(f => f.key.startsWith("CUSTOMER_")).map(partyField)}
    </div></section>
    <section id="provider-fields" tabIndex={-1} className={s.section}><details open={!template.wordStorageKey || undefined}><summary><Building2/> Công ty mình · Bên B</summary><div className={s.fields}>{template.fields.filter(f => f.key.startsWith("PROVIDER_")).map(partyField)}</div></details></section>
    {!id && <section className={s.section}><h2>Số hợp đồng</h2><p>Chờ cấp số khi lưu nháp</p>
      <NumberRule owner="0" selected={!content.numberingBusinessId} choose={() => change(c => ({...c, numberingBusinessId:null}))}/>
      {content.partyType === "BUSINESS" && content.customerBusinessId && <NumberRule key={content.customerBusinessId}
        owner={content.customerBusinessId} customerId={content.customerId} selected={content.numberingBusinessId === content.customerBusinessId}
        choose={() => change(c => c.customerBusinessId === content.customerBusinessId && c.customerId === content.customerId ? {...c, numberingBusinessId:c.customerBusinessId} : c)}/>}
    </section>}
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
    <section id="contract-terms" tabIndex={-1} className={s.section}><h2><ListChecks/>{showWordSections ? "Nội dung hợp đồng" : "Điều khoản hợp đồng"}</h2>
      {template.wordStorageKey && <TemplateUpload key={String(template.id) + ":" + template.wordStorageKey} draft={record ?? undefined} locate={locate} onImported={() => {}}
        inline={{readOnly:!inlineWord,content,saved:record?.content,templateId:String(template.id),previewHost,receipt,changed:() => {setDirty(true); dirtyRef.current=true;setSuccess("");}}}/>}
      <fieldset hidden={!!template.wordStorageKey} disabled={!canEditTerms || showWordSections} style={{border:0,padding:0}}>
      {!template.isImported && <Field label="Căn cứ hợp đồng"><textarea className={`${ui.field} ${s.termContent}`} maxLength={4000} value={content.fields.CONTRACT_LEGAL_BASES ?? ""} onChange={e => setField("CONTRACT_LEGAL_BASES", e.target.value)}/></Field>}
      <div style={{ marginTop: 18 }}>{showWordSections
        ? wordDisplaySections(content.terms).map(section => (
          <div className={s.lineItem} id={`contract-term-${section.key}`} tabIndex={-1} key={section.key}>
            <h3>{section.title}</h3>
            {section.content && <p style={{ whiteSpace: "pre-wrap", overflowWrap: "anywhere" }}>{section.content}</p>}
          </div>
        ))
        : content.terms.map((term, index) => <div className={s.lineItem} id={`contract-term-${term.key}`} tabIndex={-1} key={term.key}><header><strong style={{ fontSize: 12 }}>Điều {index + 1}{!term.editable ? " · Cố định theo mẫu" : ""}</strong><div><button type="button" className={s.iconButton} disabled={!!content.templateSnapshot?.wordStorageKey || index === 0} aria-label={`Đưa điều ${index + 1} lên`} onClick={() => moveTerm(index, -1)}><ArrowUp size={15}/></button><button type="button" className={s.iconButton} disabled={!!content.templateSnapshot?.wordStorageKey || index === content.terms.length - 1} aria-label={`Đưa điều ${index + 1} xuống`} onClick={() => moveTerm(index, 1)}><ArrowDown size={15}/></button><button type="button" className={s.iconButton} disabled={!canEditTerms} aria-label={`Xóa điều ${index + 1}`} onClick={() => change(c => ({ ...c, terms: c.terms.filter(t => t.key !== term.key) }))}><Trash2 size={15}/></button></div></header>
        <Field label="Tiêu đề điều khoản"><input required disabled={!canEditTerms} maxLength={500} className={ui.field} value={term.title} onChange={e => change(c => ({ ...c, terms: c.terms.map(t => t.key === term.key ? { ...t, title: e.target.value } : t) }))}/></Field>
        <Field label="Nội dung"><textarea disabled={!canEditTerms} maxLength={20000} className={`${ui.field} ${s.termContent}`} placeholder="Nhập nội dung điều khoản…" value={term.content} onChange={e => change(c => ({ ...c, terms: c.terms.map(t => t.key === term.key ? { ...t, content: e.target.value } : t) }))}/></Field>
      </div>)}</div>{!showWordSections && <select aria-label="Thêm mục" className={ui.field} value="" onChange={e => {
        const title = e.target.value;
        if (title) change(c => ({ ...c, terms: [...c.terms, { key: crypto.randomUUID(), title, content: "", editable: true }] }));
      }}>
        <option value="">Thêm mục</option>
        <option>Điều khoản bổ sung</option>
        <option>Định nghĩa</option>
        <option>Phụ lục</option>
      </select>}
    </fieldset></section><PaymentEditor content={content} change={change}/><section className={s.section}><h2>Thông tin bổ sung theo mẫu</h2>{template.fields.filter(f => !f.key.startsWith("CUSTOMER_") && !f.key.startsWith("PROVIDER_") && f.key !== "CONTRACT_LEGAL_BASES").map(f => <Field key={f.key} label={f.label + (f.required ? " *" : "")} >{f.dataType === "SELECT" ? <select className={ui.field} value={content.fields[f.key] ?? ""} onChange={e => setField(f.key,e.target.value)}><option value="">Chọn giá trị</option>{f.options?.map(o => <option key={o}>{o}</option>)}</select> : <input className={ui.field} type={f.dataType === "DATE" ? "date" : f.dataType === "NUMBER" ? "number" : "text"} step="any" min={f.min ?? undefined} max={f.max ?? undefined} value={content.fields[f.key] ?? ""} onChange={e => setField(f.key,e.target.value)}/>}</Field>)}</section><section className={ui.notice}><strong>Nháp có thể lưu khi chưa hoàn thiện</strong>{issues.length > 0 && <ul>{issues.map((issue,i) => <li key={i}><button type="button" className={ui.secondaryButton} onClick={() => focusIssue(issue.path)}>{issue.message}</button></li>)}</ul>}<p className={s.muted} style={{ marginTop: 6 }}>{missing.length ? `Cần bổ sung: ${missing.join(", ")}.` : "Đã đủ các trường thông tin của mẫu. Hợp đồng vẫn là bản nháp, chưa được duyệt hoặc ký."}</p></section>
  </fieldset></form></div>;
  return <><div className={s.editorHeader}><Link href="/workspace/contracts/drafts" className={s.iconButton} aria-label="Về danh sách"><ArrowLeft size={20}/></Link><div><h1>{readOnly ? "Xem hợp đồng" : id ? "Chỉnh sửa hợp đồng nháp" : "Tạo hợp đồng nháp"}</h1><p className={s.muted}>{record ? `${record.employeeName} · Cập nhật ${dateTime(record.updatedAt)}` : "Bắt đầu từ mẫu, hoàn thiện theo nhu cầu"}</p></div><div className={s.actions}><span className={`${s.badge} ${dirty ? s.warning : ""}`}>{dirty ? "Chưa lưu thay đổi" : record ? "Đã lưu" : "Nháp mới"}</span>{!readOnly && <button disabled={busy || customerBusy} className={ui.primaryButton} onClick={save}><Save size={17}/>{busy ? "Đang lưu…" : "Lưu nháp"}</button>}</div></div>
    {(error || success) && <div className={s.messages}>{error && <div role="alert" className={ui.error}>{error}{conflict && <button className={ui.secondaryButton} style={{ marginLeft: 12 }} onClick={reload}>Tải lại bản mới</button>}</div>}{success && <div role="status" className={ui.notice}>{success}</div>}</div>}
    {!record && <div className={s.messages}><section className={ui.card} style={{ display: "flex", flexWrap: "wrap", alignItems: "center", gap: 12 }} aria-label="Gửi khách xem"><button type="button" className={ui.primaryButton} disabled aria-describedby="new-draft-send-guidance"><Send size={16}/>Gửi khách xem</button><p id="new-draft-send-guidance" className={s.muted}>Lưu nháp trước, sau đó hoàn thiện các mục còn thiếu để gửi khách xem.</p></section></div>}
    {record && <div className={s.messages}><ChecklistPanel record={record} dirty={dirty} onChanged={() => retryLoad(v => v + 1)} onLocate={location => { setTab("edit"); if (template.wordStorageKey && canEditTerms && !readOnly && !dirty) setLocate({location,request:Date.now()}); else requestAnimationFrame(() => { const target = document.getElementById(`contract-term-${location}`) ?? document.getElementById("contract-terms"); target?.scrollIntoView({behavior:"smooth"}); target?.focus({preventScroll:true}); }); }}/><ApprovalPanel record={record} dirty={dirty} onChanged={() => retryLoad(v => v + 1)}/><ReviewPanel onLocate={path => { setTab("edit"); requestAnimationFrame(() => focusIssue(path)); }} record={record} dirty={dirty} onChanged={() => retryLoad(v => v + 1)}/></div>}
    <div className={s.mobileTabs}><button type="button" className={tab === "edit" ? ui.primaryButton : ui.secondaryButton} onClick={() => setTab("edit")}>Soạn thảo</button><button type="button" className={tab === "preview" ? ui.primaryButton : ui.secondaryButton} onClick={() => setTab("preview")}>Xem trước</button></div>
    <div className={s.editorBody} style={mobile ? {height:"calc(100dvh - 140px)",minHeight:480}:undefined}><PanelGroup direction="horizontal">
      <Panel defaultSize={48} minSize={30} style={mobile ? {display:tab==="edit"?"block":"none",flexGrow:1}:undefined}>{editor}</Panel>
      {!mobile && <PanelResizeHandle aria-label="Điều chỉnh chiều rộng vùng soạn thảo" style={{width:10,display:"grid",placeItems:"center"}}><GripVertical size={14}/></PanelResizeHandle>}
      <Panel defaultSize={52} minSize={30} style={mobile ? {display:tab==="preview"?"block":"none",flexGrow:1}:undefined}>
        {inlineWord && <div ref={setPreviewHost} style={{height:"calc(100% - 80px)",overflow:"auto"}}/>}
        {content.templateSnapshot?.wordStorageKey ? <WordDraftPreview downloadOnly={inlineWord} content={content} draftId={record?.id} dirty={dirty}/> : <DraftPreview content={content} code={record?.code}/>}
      </Panel></PanelGroup></div>
  </>;
}

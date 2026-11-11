"use client";
import { useEffect, useRef, useState } from "react";
import { money, internalApi, type BusinessOption, type CustomerOption, type DraftContent, type DraftMilestone } from "@/services/internal-api";
import { internalStyles as ui } from "./internal-theme";
import s from "./workspace.module.css";
import { paymentSummary, convertPayments, roundMoney } from "../../../Shared/draft-validation";
type Props = { content: DraftContent; change: (fn: (old: DraftContent) => DraftContent) => void };
export function PartyEditor({ content, change }: Props) {
  const currentCustomer = useRef(content.customerId); currentCustomer.current = content.customerId;
  const [retry, setRetry] = useState(0);
  const [businesses, setBusinesses] = useState<BusinessOption[]>([]), [error, setError] = useState("");
  const [form, setForm] = useState({ name: "", taxCode: "", address: "", representative: "" }), [adding, setAdding] = useState(false), [busy, setBusy] = useState(false);
  useEffect(() => { let active = true; setBusinesses([]); setAdding(false); setError(""); if (!content.customerId) return; internalApi<BusinessOption[]>(`/customers/${content.customerId}/businesses`).then(x => { if(active) { setBusinesses(x); setError(""); } }).catch(e => { if(active) setError(e.message); }); return () => { active = false; }; }, [content.customerId,retry]);
  function select(b: BusinessOption) {
    if (!window.confirm("Thay thông tin bên khách bằng pháp nhân đã chọn? Nội dung điều khoản viết cố định cần được kiểm tra lại.")) return;
    change(c => ({ ...c, partyType: "BUSINESS", customerBusinessId: b.id, fields: { ...c.fields, CUSTOMER_COMPANY: b.name, CUSTOMER_TAX_CODE: b.taxCode, CUSTOMER_ADDRESS: b.address, CUSTOMER_CONTACT_NAME: b.representative } }));
  }
  async function chooseType(partyType: string) {
    if (!window.confirm("Đổi loại bên giao dịch và cập nhật thông tin khách?")) return;
    if (partyType === "PERSON") {
      try { const customers = await internalApi<CustomerOption[]>(`/customers?search=${encodeURIComponent(content.fields.CUSTOMER_CONTACT_PHONE || "")}`); const customer = customers.find(c => c.id === content.customerId);
        change(c => c.customerId !== content.customerId ? c : ({ ...c, partyType, customerBusinessId: null, fields: { ...c.fields, ...(customer?.fields ?? {}), CUSTOMER_TAX_CODE: "" } }));
      } catch(e) { setError((e as Error).message); }
    } else change(c => ({ ...c, partyType, customerBusinessId: null }));
  }
  async function create() { const customerId=content.customerId; setBusy(true); setError(""); try { const b = await internalApi<BusinessOption>(`/customers/${content.customerId}/businesses`, "POST", form); if(currentCustomer.current!==customerId) return; setBusinesses(bs => [...bs,b]); setAdding(false); select(b); } catch(e) { if(currentCustomer.current===customerId) setError((e as Error).message); } finally { setBusy(false); } }
  return <div className={s.lineItem}><label className={s.label}>Bên giao dịch<select aria-label="Loại bên giao dịch" className={ui.field} disabled={!content.customerId || busy} value={content.partyType ?? ""} onChange={e => void chooseType(e.target.value)}><option value="">Chọn cá nhân hoặc doanh nghiệp</option><option value="PERSON">Cá nhân</option><option value="BUSINESS">Doanh nghiệp</option></select></label>
    {content.partyType === "BUSINESS" && <><label className={s.label}>Pháp nhân<select className={ui.field} value={content.customerBusinessId ?? ""} onChange={e => { const b=businesses.find(x => x.id===e.target.value); if(b) select(b); }}><option value="">Chọn pháp nhân</option>{businesses.map(b => <option key={b.id} value={b.id}>{b.name} — {b.taxCode}</option>)}</select></label><button type="button" className={ui.secondaryButton} onClick={() => setAdding(!adding)}>Thêm pháp nhân</button>
      {adding && <div className={s.fields}>{([['name','Tên pháp lý'],['taxCode','Mã số thuế'],['address','Địa chỉ'],['representative','Người đại diện']] as const).map(([key,label]) => <label className={s.label} key={key}>{label}<input className={ui.field} value={form[key]} onChange={e => setForm({...form,[key]:e.target.value})}/></label>)}<button type="button" className={ui.primaryButton} disabled={busy} onClick={() => void create()}>Lưu pháp nhân</button></div>}</>}
    {error && <div role="alert" className={ui.error}>{error}<button type="button" className={ui.secondaryButton} onClick={()=>setRetry(n=>n+1)}>Tải lại pháp nhân</button></div>}
  </div>;
}
export function PaymentEditor({ content, change }: Props) {
  const rows = content.milestones ?? [];
  const summary = paymentSummary(content);
  function switchMode(mode: "PERCENT" | "AMOUNT") { const converted=convertPayments(content,mode); if(rows.length && !window.confirm(`Đổi sang ${mode === "AMOUNT" ? "số tiền" : "tỷ lệ"} theo tổng ${money(summary.total)} ₫? Kết quả: ${converted.map(m => `${m.title}: ${mode === "AMOUNT" ? money(m.amount)+" ₫" : m.percent+"%"}`).join("; ")}`)) return; change(c=>({...c,paymentMode:mode,milestones:converted})); }
  function allocate(i:number) { const remaining=content.paymentMode === "AMOUNT" ? roundMoney(summary.total-rows.reduce((sum,m,j)=>sum+(j===i?0:m.amount),0)) : roundMoney(100-rows.reduce((sum,m,j)=>sum+(j===i?0:m.percent),0)); if(remaining<0 || (content.paymentMode!=="AMOUNT" && remaining>100)) {window.alert("Các đợt khác đang vượt tổng; hãy giảm trước khi phân bổ.");return;} patch(i,content.paymentMode === "AMOUNT" ? {amount:remaining} : {percent:remaining}); }
  const patch = (i: number, value: Partial<DraftMilestone>) => change(c => ({ ...c, milestones: (c.milestones ?? []).map((m,j) => i===j ? {...m,...value} : m) }));
  const add = (single=false) => change(c => ({ ...c, paymentMode: single ? "PERCENT" : c.paymentMode ?? "PERCENT", milestones: [...(single ? [] : c.milestones ?? []), { key: crypto.randomUUID(), title: single ? "Thanh toán toàn bộ" : `Đợt ${(c.milestones?.length ?? 0)+1}`, termKey: c.terms.find(t => /thanh toán/i.test(t.title))?.key ?? "", percent: single ? 100 : 0, amount: 0, anchor: "SIGNED", offsetDays: 0, dueDate: null, condition: "" }] }));
  return <section id="payment-schedule" tabIndex={-1} className={s.section}><h2>Lịch thanh toán</h2><p className={s.muted}>Tính trên tổng sau VAT. Trước khi gửi khách, các đợt phải cộng đủ tổng hợp đồng.</p>
    <div className={ui.notice} aria-live="polite"><p>Tổng hợp đồng: <strong>{money(summary.total)} ₫</strong></p><p>Đã phân bổ: {money(summary.allocated)} ₫{content.paymentMode !== "AMOUNT" ? ` (${summary.percent}%)` : ""}</p><p>{summary.remaining > 0 ? `Còn thiếu ${money(summary.remaining)} ₫` : summary.remaining < 0 ? `Đang thừa ${money(-summary.remaining)} ₫` : "Đã phân bổ đủ số tiền"}</p></div><label className={s.label}>Cách phân bổ<select className={ui.field} value={content.paymentMode ?? "PERCENT"} onChange={e => switchMode(e.target.value as "PERCENT" | "AMOUNT")}><option value="PERCENT">Theo tỷ lệ (%)</option><option value="AMOUNT">Theo số tiền (VND)</option></select></label>
    {rows.map((m,i) => <div key={m.key} className={s.lineItem}><label className={s.label}>Tên đợt<input className={ui.field} value={m.title} onChange={e => patch(i,{title:e.target.value})}/></label>
      <label className={s.label}>{content.paymentMode === "AMOUNT" ? "Số tiền" : "Tỷ lệ (%)"}<input className={ui.field} type="number" min="0" step="0.01" value={content.paymentMode === "AMOUNT" ? m.amount : m.percent} onChange={e => patch(i,content.paymentMode === "AMOUNT" ? {amount:e.target.valueAsNumber || 0} : {percent:e.target.valueAsNumber || 0})}/></label>
      <label className={s.label}>Điều khoản thanh toán<select className={ui.field} value={m.termKey} onChange={e => patch(i,{termKey:e.target.value})}><option value="">Chọn điều khoản</option>{content.terms.map(t => <option key={t.key} value={t.key}>{t.title}</option>)}</select></label>
      <label className={s.label}>Căn cứ tính hạn<select className={ui.field} value={m.anchor} onChange={e => patch(i,{anchor:e.target.value as DraftMilestone['anchor'],dueDate:null})}><option value="SIGNED">Sau ngày ký đủ</option><option value="HANDOVER">Sau ngày bàn giao</option><option value="DATE">Ngày cụ thể</option></select></label>
      {m.anchor === "DATE" ? <label className={s.label}>Ngày đến hạn<input className={ui.field} type="date" value={m.dueDate ?? ""} onChange={e => patch(i,{dueDate:e.target.value || null})}/></label> : <label className={s.label}>Số ngày lịch sau sự kiện<input className={ui.field} type="number" min="0" step="1" value={m.offsetDays} onChange={e => patch(i,{offsetDays:e.target.valueAsNumber || 0})}/></label>}
      <label className={s.label}>Điều kiện bổ sung<textarea className={ui.field} value={m.condition} onChange={e => patch(i,{condition:e.target.value})}/></label><button type="button" className={ui.secondaryButton} onClick={() => allocate(i)}>Phân bổ phần còn lại vào đợt này</button><button type="button" className={ui.secondaryButton} onClick={() => change(c => ({...c,milestones:(c.milestones ?? []).filter((_,j) => i!==j)}))}>Xóa đợt</button></div>)}
    <button type="button" className={ui.secondaryButton} onClick={() => add()}>Thêm đợt thanh toán</button>{!rows.length && <button type="button" className={ui.secondaryButton} onClick={() => add(true)}>Tạo một đợt 100%</button>}
  </section>;
}

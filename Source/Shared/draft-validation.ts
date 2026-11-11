import type { DraftContent, DraftMilestone } from "../SystemAdmin/services/internal-api";
import { renderContract } from "./contract-renderer";
export type DraftIssue = { code: string; path: string; message: string };
export const roundMoney = (n: number) => Math.round((n + Number.EPSILON) * 100) / 100;
export function paymentSummary(c: DraftContent) {
  const rendered = renderContract(c);
  const allocated = roundMoney(rendered.milestones.reduce((n,m) => n + m.amount,0));
  const percent = roundMoney((c.milestones ?? []).reduce((n,m) => n + m.percent,0));
  return { total: rendered.totals.total, allocated, remaining: roundMoney(rendered.totals.total - allocated), percent, rows: rendered.milestones };
}
export function convertPayments(c: DraftContent, mode: "PERCENT" | "AMOUNT"): DraftMilestone[] {
  const summary = paymentSummary(c);
  if(mode === "AMOUNT") return summary.rows.map(({due, ...m}) => m);
  let allocated = 0;
  return summary.rows.map((m,i) => {
    const percent = summary.total ? roundMoney(m.amount / summary.total * 100) : 0;
    const adjusted = summary.remaining === 0 && summary.total > 0 && i === summary.rows.length - 1 ? roundMoney(100-allocated) : percent;
    allocated = roundMoney(allocated + adjusted);
    const {due,...row}=m; return {...row,percent:adjusted};
  });
}
export function draftIssues(c: DraftContent): DraftIssue[] {
  const issues: DraftIssue[]=[];
  const add=(code:string,path:string,message:string)=>issues.push({code,path,message});
  if(!c.name.trim()) add("required","name","Tên hợp đồng");
  if(!c.customerId) add("required","partyType","Chọn khách hàng");
  if(!c.partyType) add("required","partyType","Chọn cá nhân hoặc doanh nghiệp");
  if(c.partyType === "BUSINESS" && !c.customerBusinessId) add("required","customerBusinessId","Chọn pháp nhân khách hàng");
  if(c.partyType === "BUSINESS" && !c.fields.CUSTOMER_TAX_CODE?.trim()) add("required","fields.CUSTOMER_TAX_CODE","Mã số thuế pháp nhân");
  for(const f of c.templateSnapshot?.fields ?? []) {
    const value=c.fields[f.key]?.trim();
    if(f.required && !value) add("required","fields."+f.key,f.label);
    if(value && f.dataType === "NUMBER" && (!Number.isFinite(Number(value)) || (f.min != null && Number(value)<f.min) || (f.max != null && Number(value)>f.max))) add("invalid_number","fields."+f.key,`${f.label}: số không hợp lệ`);
    if(value && f.dataType === "SELECT" && !f.options?.includes(value)) add("invalid_choice","fields."+f.key,`${f.label}: chọn giá trị trong danh sách`);
  }
  if(!c.items.length) add("required","items","Hạng mục hợp đồng");
  c.items.forEach((m,i)=> {if(!m.name.trim() || m.quantity<=0 || m.unitPrice<0) add("invalid_item",`items.${i}`,`Hạng mục ${i+1}: tên, số lượng hoặc đơn giá không hợp lệ`);});
  if(!c.terms.length || c.terms.some(t=>!t.title.trim() || (t.editable !== false && !t.content.trim()))) add("required","terms","Nội dung điều khoản");
  const rows=c.milestones ?? [], summary=paymentSummary(c);
  if(!rows.length) add("required","milestones","Lịch thanh toán");
  rows.forEach((m,i)=>{
    if(!m.title.trim()) add("required",`milestones.${i}.title`,`Đợt ${i+1}: tên đợt`);
    if(!c.terms.some(t=>t.key===m.termKey)) add("invalid_term",`milestones.${i}.termKey`,`Đợt ${i+1}: chọn điều khoản thanh toán`);
    if(m.anchor==='DATE' && !m.dueDate) add("required",`milestones.${i}.dueDate`,`Đợt ${i+1}: ngày đến hạn`);
    if(m.percent<0 || m.percent>100 || m.amount<0 || m.offsetDays<0 || !Number.isInteger(m.offsetDays)) add("invalid_payment",`milestones.${i}`,`Đợt ${i+1}: dữ liệu không hợp lệ`);
  });
  if(rows.length && (summary.remaining!==0 || (c.paymentMode!=='AMOUNT' && summary.percent!==100))) add("payment_total","milestones","Lịch thanh toán phải đủ tổng sau VAT");
  return issues;
}
export function issueTarget(path:string) { return path.startsWith("milestones") ? "payment-schedule" : path.startsWith("fields.PROVIDER") ? "provider-fields" : path.startsWith("fields.CUSTOMER") || path==='partyType' || path==='customerBusinessId' ? "customer-fields" : path.startsWith("items") ? "contract-items" : path.startsWith("terms") ? "contract-terms" : "contract-info"; }
export function focusIssue(path:string) { const el=document.getElementById(issueTarget(path)); el?.scrollIntoView({behavior:"smooth",block:"start"}); el?.focus({preventScroll:true}); }

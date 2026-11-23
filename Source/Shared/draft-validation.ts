import type { DraftContent, DraftMilestone } from "../SystemAdmin/services/internal-api";
import { renderContract } from "./contract-renderer";
import { ruleFor, needsMilestones } from "./document-rules";
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
  const rule=ruleFor(c.templateSnapshot);
  if(!c.name.trim()) add("required","name",rule.type===1 ? "Tên hợp đồng" : "Tên văn bản");
  if(rule.expireRequired && !c.expireDate) add("required","expireDate",rule.type===1 ? "Vui lòng nhập ngày hết hạn hợp đồng." : `Vui lòng nhập ${rule.expireLabel.toLowerCase()}.`);
  else if(rule.expireShown && c.expireDate && c.effectiveDate && c.expireDate < c.effectiveDate) add("invalid_date","expireDate",`${rule.expireLabel} phải sau hoặc bằng ${rule.effectiveLabel.toLowerCase()}.`);
  if(rule.effectiveRequired && !c.effectiveDate) add("required","effectiveDate",rule.effectiveLabel);
  if(rule.parentMilestone && c.parentMilestoneRequired && !c.parentMilestoneKey) add("required","parentMilestoneKey","Đợt thanh toán của hợp đồng");
  if(!c.customerId) add("required","partyType","Chọn khách hàng");
  if(!c.partyType) add("required","partyType","Chọn cá nhân hoặc doanh nghiệp");
  if(c.partyType === "BUSINESS" && !c.customerBusinessId) add("required","customerBusinessId","Chọn pháp nhân khách hàng");
  if(c.partyType === "BUSINESS" && !c.fields.CUSTOMER_TAX_CODE?.trim()) add("required","fields.CUSTOMER_TAX_CODE","Mã số thuế pháp nhân");
  for(const f of c.templateSnapshot?.fields ?? []) {
    const value=c.fields[f.key]?.trim();
    if(f.required && !value && !(c.templateSnapshot?.wordStorageKey && f.key.startsWith("PROVIDER_"))) add("required","fields."+f.key,f.label);
    if(value && f.dataType === "NUMBER" && (!Number.isFinite(Number(value)) || (f.min != null && Number(value)<f.min) || (f.max != null && Number(value)>f.max))) add("invalid_number","fields."+f.key,`${f.label}: số không hợp lệ`);
    if(value && f.dataType === "SELECT" && !f.options?.includes(value)) add("invalid_choice","fields."+f.key,`${f.label}: chọn giá trị trong danh sách`);
  }
  if(rule.itemsRequired && !c.items.length) add("required","items",rule.itemsLabel);
  if(rule.amountRequired && c.items.length && renderContract(c).totals.total<=0) add("required","items","Số tiền phải lớn hơn 0");
  c.items.forEach((m,i)=> {if(!m.name.trim() || m.quantity<=0 || m.unitPrice<0) add("invalid_item",`items.${i}`,`Hạng mục ${i+1}: tên, số lượng hoặc đơn giá không hợp lệ`);});
  if(!c.terms.length || c.terms.some(t=>!t.title.trim() || (t.editable !== false && !t.content.trim()))) add("required","terms","Nội dung điều khoản");
  if(c.languageMode === "VI_EN" && !c.nameEn?.trim()) add("required","nameEn","Tên hợp đồng tiếng Anh");
  if(c.languageMode === "VI_EN" && c.terms.some(t=>t.editable !== false && (!t.titleEn?.trim() || !t.contentEn?.trim()))) add("required","terms","Điều khoản tiếng Anh");
  // A handover record, receipt or payment request has no schedule of its own.
  if(!rule.milestones) return issues;
  const rows=c.milestones ?? [], summary=paymentSummary(c);
  if(!rows.length && needsMilestones(rule,c.items.length)) add("required","milestones","Lịch thanh toán");
  rows.forEach((m,i)=>{
    if(!m.title.trim()) add("required",`milestones.${i}.title`,`Đợt ${i+1}: tên đợt`);
    if(!c.terms.some(t=>t.key===m.termKey)) add("invalid_term",`milestones.${i}.termKey`,`Đợt ${i+1}: chọn điều khoản thanh toán`);
    if(m.anchor==='DATE' && !m.dueDate) add("required",`milestones.${i}.dueDate`,`Đợt ${i+1}: ngày đến hạn`);
    if(m.percent<0 || m.percent>100 || m.amount<0 || m.offsetDays<0 || !Number.isInteger(m.offsetDays)) add("invalid_payment",`milestones.${i}`,`Đợt ${i+1}: dữ liệu không hợp lệ`);
  });
  if(rows.length && (summary.remaining!==0 || (c.paymentMode!=='AMOUNT' && summary.percent!==100))) add("payment_total","milestones","Lịch thanh toán phải đủ tổng sau VAT");
  return issues;
}
export const DEFAULT_PROVIDER_FIELDS: Record<string, string> = {
  PROVIDER_LEGAL_NAME: "Công ty Cổ phần Công nghệ ConTrack",
  PROVIDER_REPRESENTATIVE_NAME: "Đại diện Ban Giám Đốc",
  PROVIDER_REPRESENTATIVE_TITLE: "Giám đốc",
  PROVIDER_ADDRESS: "Tầng 12, Tòa nhà Keangnam, Nam Từ Liêm, Hà Nội",
  PROVIDER_PHONE: "02431234567",
  PROVIDER_TAX_CODE: "0101234567",
  PROVIDER_BANK_NAME: "Ngân hàng TMCP Ngoại thương Việt Nam (Vietcombank)",
  PROVIDER_BANK_ACCOUNT_NUMBER: "0011001234567"
};

export const requiredPartyFields = [
    {key:"CUSTOMER_CONTACT_NAME",label:"Người đại diện khách hàng",required:true},
    {key:"CUSTOMER_COMPANY",label:"Tên bên khách hàng",required:true},
    {key:"CUSTOMER_CONTACT_PHONE",label:"SĐT khách hàng",required:true},
    {key:"CUSTOMER_ADDRESS",label:"Địa chỉ khách hàng",required:true},
    {key:"PROVIDER_LEGAL_NAME",label:"Tên pháp lý công ty",required:true},
    {key:"PROVIDER_REPRESENTATIVE_NAME",label:"Người đại diện công ty",required:true},
    {key:"PROVIDER_ADDRESS",label:"Địa chỉ công ty",required:true},
    {key:"PROVIDER_TAX_CODE",label:"Mã số thuế công ty",required:true},
  ];
export function sendIssues(c: DraftContent): DraftIssue[] {
  const issues = draftIssues(c);
  // The template's own required flags decide, party fields included; a party field the template lacks keeps its default.
  const own = c.templateSnapshot?.fields ?? [];
  for (const f of [...own, ...requiredPartyFields.filter(p => !own.some(x => x.key === p.key))]) {
    // Word templates carry Bên B in the file itself.
    if (c.templateSnapshot?.wordStorageKey && f.key.startsWith("PROVIDER_")) continue;
    const value = c.fields[f.key]?.trim() || (f.key.startsWith("PROVIDER_") ? DEFAULT_PROVIDER_FIELDS[f.key] : "");
    if (f.required && !value && !issues.some(i => i.path === "fields."+f.key))
      issues.push({code:"required",path:"fields."+f.key,message:f.label});
  }
  for (const t of c.terms) {
    if ((!c.templateSnapshot?.wordStorageKey || t.editable !== false || /^Điều\s*\d+/iu.test(t.title)) && !t.content.trim())
      issues.push({code:"required",path:"terms",message:`Thiếu nội dung điều khoản “${t.title}”.`});
  }
  return issues;
}
export function issueTarget(path:string) { return path.startsWith("milestones") ? "payment-schedule" : path.startsWith("fields.PROVIDER") ? "provider-fields" : path.startsWith("fields.CUSTOMER") || path==='partyType' || path==='customerBusinessId' ? "customer-fields" : path.startsWith("items") ? "contract-items" : path.startsWith("terms") ? "contract-terms" : "contract-info"; }
export function focusIssue(path:string) { const el=document.getElementById(issueTarget(path)); const details = el?.querySelector("details"); if(details) details.open = true; el?.scrollIntoView({behavior:"smooth",block:"start"}); el?.focus({preventScroll:true}); }

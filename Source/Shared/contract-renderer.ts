import type { DraftContent } from "../SystemAdmin/services/internal-api";

const money = (n: number) => new Intl.NumberFormat("vi-VN", { maximumFractionDigits: 2 }).format(n);
export function renderContract(content: DraftContent) {
  const round = (n: number) => Math.round((n + Number.EPSILON) * 100) / 100;
  let subtotal = 0, discount = 0;
  for (const item of content.items) { const line = round(item.quantity * item.unitPrice); subtotal += line; discount += item.discountPercent > 0 ? round(line * item.discountPercent / 100) : item.discountAmount; }
  const vat = round((subtotal - discount) * content.vatPercent / 100);
  const totals = { subtotal: round(subtotal), discount: round(discount), vat, total: round(subtotal - discount + vat) };
  let allocated = 0;
  const rows = content.milestones ?? [];
  const milestones = rows.map((m, i) => {
    let amount = content.paymentMode === "AMOUNT" ? m.amount : round(totals.total * m.percent / 100);
    if (content.paymentMode !== "AMOUNT" && rows.reduce((sum, r) => sum + r.percent, 0) === 100 && i === rows.length - 1) amount = round(totals.total - allocated);
    allocated = round(allocated + amount);
    const due = m.anchor === "DATE" ? m.dueDate || "Chưa chọn ngày" : `${m.offsetDays} ngày lịch sau ${m.anchor === "SIGNED" ? "ngày ký đủ" : "ngày bàn giao"}`;
    return { ...m, amount, due };
  });
  const fields: Record<string, string> = { ...content.fields, CONTRACT_NAME: content.name, EFFECTIVE_DATE: content.effectiveDate ?? "", EXPIRE_DATE: content.expireDate ?? "",
    SUBTOTAL: money(totals.subtotal), DISCOUNT: money(totals.discount), VAT: money(totals.vat), TOTAL_AMOUNT: money(totals.total),
    PAYMENT_SCHEDULE: milestones.map(m => `${m.title}: ${money(m.amount)} VND — ${m.due}${m.condition ? "; " + m.condition : ""}`).join("\n") };
  const resolve = (text: string) => text.replace(/\{\{([^{}]*)\}\}/g, (_, key: string) => fields[key]?.trim() || `[Chưa điền: ${key}]`);
  return { totals, milestones, terms: content.terms.map(t => ({ ...t, title: (content.formatVersion ?? 1) < 2 ? t.title : resolve(t.title), content: (content.formatVersion ?? 1) < 2 ? t.content : resolve(t.content) })) };
}

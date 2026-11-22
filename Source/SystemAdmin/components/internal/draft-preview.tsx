import { paymentMethodLabel, renderContract } from "../../../Shared/contract-renderer";
import { DEFAULT_PROVIDER_FIELDS } from "../../../Shared/draft-validation";
import type { ReactNode } from "react";
import { type DraftContent, money } from "@/services/internal-api";
import s from "./workspace.module.css";
export default function DraftPreview({ content, code }: { content: DraftContent; code?: string }) {
  // Normalize only the display copy; stored snapshots and signing hashes stay unchanged.
  content = JSON.parse(JSON.stringify(content).normalize("NFC")) as DraftContent;
  const rendered = renderContract(content); const totals = rendered.totals;
  // Bilingual (VI_EN), as in the company's printed contracts: English first, Vietnamese in italics.
  const bi = content.languageMode === "VI_EN";
  const L = (vi: string, en: string) => bi ? `${en} / ${vi}` : vi;
  const customerCompany = content.fields.CUSTOMER_COMPANY?.trim();
  const customerContact = content.fields.CUSTOMER_CONTACT_NAME?.trim();
  const effectiveCompany = customerCompany || customerContact || "";
  const effectiveContact = customerContact || customerCompany || "";
  const field = (key: string, label: string) => {
    let val = content.fields[key]?.trim();
    if (!val) {
      if (key === "CUSTOMER_COMPANY") val = effectiveCompany;
      else if (key === "CUSTOMER_CONTACT_NAME") val = effectiveContact;
      else if (key.startsWith("PROVIDER_")) val = DEFAULT_PROVIDER_FIELDS[key] ?? "";
    }
    return val || <span className={s.missing}>Chưa điền: {label}</span>;
  };
  const both = (key: string, vi: ReactNode) => { const en = bi ? content.fieldsEn?.[key]?.trim() : ""; return en ? <>{en}<br/><em>{vi}</em></> : vi; };
  const date = (value: string | null) => value ? value.split("-").reverse().join("/") : <span className={s.missing}>Chưa điền ngày</span>;
  if ((content.formatVersion ?? 1) < 2 && content.templateSnapshot?.isImported) return <div className={s.previewPane}><article className={s.paper}><div className={s.demoStamp}>BẢN NHÁP • MẪU TẢI LÊN — CHƯA XÁC NHẬN PHÊ DUYỆT</div><h2>{content.name}</h2><p>Mã nháp: {code || "Được cấp khi lưu"}</p>{content.terms.map(t => <p key={t.key}>{t.content}</p>)}</article></div>;
  return <div className={s.previewPane}><div className={s.previewLabel}><span>Bản xem trước • A4{bi ? " • Song ngữ Anh – Việt" : ""}</span><span>Chưa ký</span></div><article className={s.paper} aria-label="Bản xem trước hợp đồng">
    <div className={s.demoStamp}>BẢN NHÁP • MẪU MINH HỌA — CHƯA ĐƯỢC CÔNG TY PHÊ DUYỆT</div>
    <p style={{ textAlign: "center", fontWeight: 700, marginTop: 22 }}>CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM</p><p style={{ textAlign: "center" }}>Độc lập – Tự do – Hạnh phúc</p>
    {bi ? <><h2>{content.nameEn?.trim() || <span className={s.missing}>Chưa điền tên hợp đồng tiếng Anh</span>}</h2><h2><em>{content.name || "Tên hợp đồng"}</em></h2></> : <h2>{content.name || "Tên hợp đồng"}</h2>}
    <p style={{ textAlign: "center", fontSize: 11 }}>{L("Số", "No.")}: {content.contractNumber || code || "Được cấp khi lưu nháp"}</p>
    {content.fields.CONTRACT_LEGAL_BASES && <p>{content.fields.CONTRACT_LEGAL_BASES}</p>}
    <h3>{L("BÊN A", "PARTY A")}: {both("CUSTOMER_COMPANY", field("CUSTOMER_COMPANY", "Tên khách hàng"))}</h3>
    <p>{L("Đại diện", "Represented by")}: {field("CUSTOMER_CONTACT_NAME", "Người đại diện khách hàng")}</p>
    {content.fields.CUSTOMER_CONTACT_TITLE && <p>{L("Chức vụ", "Position")}: {both("CUSTOMER_CONTACT_TITLE", content.fields.CUSTOMER_CONTACT_TITLE)}</p>}
    <p>{L("Địa chỉ", "Address")}: {both("CUSTOMER_ADDRESS", field("CUSTOMER_ADDRESS", "Địa chỉ khách hàng"))}</p><p>{L("Điện thoại", "Tel")}: {field("CUSTOMER_CONTACT_PHONE", "SĐT khách hàng")}</p>
    {content.fields.CUSTOMER_EMAIL && <p>Email: {content.fields.CUSTOMER_EMAIL}</p>}{content.fields.CUSTOMER_TAX_CODE && <p>{L("Mã số thuế", "Tax code")}: {content.fields.CUSTOMER_TAX_CODE}</p>}
    {content.fields.CUSTOMER_BANK_ACCOUNT_NUMBER && <p>{L("Tài khoản", "Bank account")}: {content.fields.CUSTOMER_BANK_ACCOUNT_NUMBER} — {both("CUSTOMER_BANK_NAME", content.fields.CUSTOMER_BANK_NAME)}</p>}
    <h3>{L("BÊN B", "PARTY B")}: {both("PROVIDER_LEGAL_NAME", field("PROVIDER_LEGAL_NAME", "Tên pháp lý công ty"))}</h3><p>{L("Đại diện", "Represented by")}: {field("PROVIDER_REPRESENTATIVE_NAME", "Người đại diện công ty")}</p>
    {content.fields.PROVIDER_REPRESENTATIVE_TITLE && <p>{L("Chức vụ", "Position")}: {both("PROVIDER_REPRESENTATIVE_TITLE", content.fields.PROVIDER_REPRESENTATIVE_TITLE)}</p>}
    <p>{L("Địa chỉ", "Address")}: {both("PROVIDER_ADDRESS", field("PROVIDER_ADDRESS", "Địa chỉ công ty"))}</p><p>{L("Mã số thuế", "Tax code")}: {field("PROVIDER_TAX_CODE", "Mã số thuế công ty")}</p>
    {content.fields.PROVIDER_PHONE && <p>{L("Điện thoại", "Tel")}: {content.fields.PROVIDER_PHONE}</p>}
    {content.fields.PROVIDER_BANK_ACCOUNT_NUMBER && <p>{L("Tài khoản", "Bank account")}: {content.fields.PROVIDER_BANK_ACCOUNT_NUMBER} — {both("PROVIDER_BANK_NAME", content.fields.PROVIDER_BANK_NAME)}</p>}
    {bi && <p>Term: from {date(content.effectiveDate)} to {date(content.expireDate)}.</p>}
    <p>{bi ? <em>Thời hạn hiệu lực: từ {date(content.effectiveDate)} đến {date(content.expireDate)}.</em> : <>Thời hạn hiệu lực: từ {date(content.effectiveDate)} đến {date(content.expireDate)}.</>}</p>
    <h3>{L("HẠNG MỤC HỢP ĐỒNG", "CONTRACT ITEMS")}</h3>{content.items.length ? <table><thead><tr><th>STT</th><th>{L("Hạng mục", "Item")}</th><th>{L("Đơn vị tính", "Unit")}</th><th>{L("Số lượng", "Qty")}</th><th>{L("Đơn giá", "Unit price")}</th><th>{L("Thành tiền", "Amount")}*</th></tr></thead><tbody>{content.items.map((item, i) => <tr key={i}><td>{i + 1}</td><td>{item.name || "Chưa điền tên"}</td><td>[THÔNG TIN CẦN BỔ SUNG]</td><td>{money(item.quantity)}</td><td>{money(item.unitPrice)}</td><td>{money(item.quantity * item.unitPrice)}</td></tr>)}</tbody></table> : <p className={s.missing}>Chưa có hạng mục hợp đồng</p>}
    <div className={s.totals}><div><span>{L("Tạm tính", "Subtotal")}</span><span>{money(totals.subtotal)} ₫</span></div><div><span>{L("Giảm giá", "Discount")}</span><span>{money(totals.discount)} ₫</span></div><div><span>VAT ({content.vatPercent}%)</span><span>{money(totals.vat)} ₫</span></div><div><span>{L("Tổng giá trị", "Total")}</span><span>{money(totals.total)} ₫</span></div></div>
    <p style={{ fontSize: 11, color: "#5d6d70" }}>* Thành tiền trước giảm giá và VAT. Giá trị sẽ được kiểm tra lại khi lưu.</p>
    {(content.formatVersion ?? 1) >= 2 && <><h3>{L("LỊCH THANH TOÁN", "PAYMENT SCHEDULE")}</h3>{rendered.milestones.length ? rendered.milestones.map(m => <section key={m.key}><strong>{m.title || "Chưa đặt tên đợt"}: {money(m.amount)} ₫</strong><p>{m.due}</p><p>{m.condition}</p></section>) : <p className={s.missing}>Chưa lập lịch thanh toán</p>}</>}
    <h3>{L("ĐIỀU KHOẢN HỢP ĐỒNG", "TERMS AND CONDITIONS")}</h3>{rendered.terms.map((term, i) => bi ? <section key={term.key}>
        <h3>ARTICLE {i + 1}: {term.titleEn || <span className={s.missing}>Chưa dịch tiêu đề</span>}</h3><h3><em>ĐIỀU {i + 1}: {term.title}</em></h3>
        <p>{term.contentEn || <span className={s.missing}>Chưa soạn nội dung tiếng Anh</span>}</p><p><em>{term.content || <span className={s.missing}>Chưa soạn nội dung điều khoản</span>}</em></p>
      </section> : <section key={term.key}><h3>Điều {i + 1}. {term.title}</h3><p>{term.content || <span className={s.missing}>Chưa soạn nội dung điều khoản</span>}</p></section>)}
    {!content.terms.length && <p className={s.missing}>Chưa có điều khoản</p>}
    <div className={s.signatures}><div><strong>{bi ? "PARTY A / ĐẠI DIỆN BÊN A" : "ĐẠI DIỆN BÊN A"}</strong><p>(Ký, ghi rõ họ tên và đóng dấu nếu có)</p><strong>{content.fields.CUSTOMER_CONTACT_NAME?.trim() || content.fields.CUSTOMER_COMPANY?.trim() || "[THÔNG TIN CẦN BỔ SUNG]"}</strong></div><div><strong>{bi ? "PARTY B / ĐẠI DIỆN BÊN B" : "ĐẠI DIỆN BÊN B"}</strong><p>(Ký, ghi rõ họ tên và đóng dấu nếu có)</p><strong>{content.fields.PROVIDER_REPRESENTATIVE_NAME?.trim() || "[THÔNG TIN CẦN BỔ SUNG]"}</strong></div></div>
  </article></div>;
}

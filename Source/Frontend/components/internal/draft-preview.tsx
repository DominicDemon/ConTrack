import { type DraftContent, estimateTotals, money } from "@/services/internal-api";
import s from "./workspace.module.css";
export default function DraftPreview({ content, code }: { content: DraftContent; code?: string }) {
  const totals = estimateTotals(content);
  const field = (key: string, label: string) => content.fields[key]?.trim() || <span className={s.missing}>Chưa điền: {label}</span>;
  const date = (value: string | null) => value ? value.split("-").reverse().join("/") : <span className={s.missing}>Chưa điền ngày</span>;
  return <div className={s.previewPane}><div className={s.previewLabel}><span>Bản xem trước • A4</span><span>Chưa ký</span></div><article className={s.paper} aria-label="Bản xem trước hợp đồng">
    <div className={s.demoStamp}>BẢN NHÁP • MẪU MINH HỌA — CHƯA ĐƯỢC CÔNG TY PHÊ DUYỆT</div>
    <p style={{ textAlign: "center", fontWeight: 700, marginTop: 22 }}>CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM</p><p style={{ textAlign: "center" }}>Độc lập – Tự do – Hạnh phúc</p>
    <h2>{content.name || "Tên hợp đồng"}</h2><p style={{ textAlign: "center", fontSize: 11 }}>Số: {code || "Được cấp khi lưu nháp"}</p>
    {content.fields.CONTRACT_LEGAL_BASES && <p>{content.fields.CONTRACT_LEGAL_BASES}</p>}
    <h3>BÊN A: {field("CUSTOMER_COMPANY", "Tên khách hàng")}</h3>
    <p>Đại diện: {field("CUSTOMER_CONTACT_NAME", "Người đại diện khách hàng")}</p>
    {content.fields.CUSTOMER_CONTACT_TITLE && <p>Chức vụ: {content.fields.CUSTOMER_CONTACT_TITLE}</p>}
    <p>Địa chỉ: {field("CUSTOMER_ADDRESS", "Địa chỉ khách hàng")}</p><p>Điện thoại: {field("CUSTOMER_CONTACT_PHONE", "SĐT khách hàng")}</p>
    {content.fields.CUSTOMER_EMAIL && <p>Email: {content.fields.CUSTOMER_EMAIL}</p>}{content.fields.CUSTOMER_TAX_CODE && <p>Mã số thuế: {content.fields.CUSTOMER_TAX_CODE}</p>}
    {content.fields.CUSTOMER_BANK_ACCOUNT_NUMBER && <p>Tài khoản: {content.fields.CUSTOMER_BANK_ACCOUNT_NUMBER} — {content.fields.CUSTOMER_BANK_NAME}</p>}
    <h3>BÊN B: {field("PROVIDER_LEGAL_NAME", "Tên pháp lý công ty")}</h3><p>Đại diện: {field("PROVIDER_REPRESENTATIVE_NAME", "Người đại diện công ty")}</p>
    {content.fields.PROVIDER_REPRESENTATIVE_TITLE && <p>Chức vụ: {content.fields.PROVIDER_REPRESENTATIVE_TITLE}</p>}
    <p>Địa chỉ: {field("PROVIDER_ADDRESS", "Địa chỉ công ty")}</p><p>Mã số thuế: {field("PROVIDER_TAX_CODE", "Mã số thuế công ty")}</p>
    {content.fields.PROVIDER_PHONE && <p>Điện thoại: {content.fields.PROVIDER_PHONE}</p>}
    {content.fields.PROVIDER_BANK_ACCOUNT_NUMBER && <p>Tài khoản: {content.fields.PROVIDER_BANK_ACCOUNT_NUMBER} — {content.fields.PROVIDER_BANK_NAME}</p>}
    <p>Thời hạn hiệu lực: từ {date(content.effectiveDate)} đến {date(content.expireDate)}.</p>
    <h3>HẠNG MỤC HỢP ĐỒNG</h3>{content.items.length ? <table><thead><tr><th>Hạng mục</th><th>SL</th><th>Đơn giá</th><th>Thành tiền*</th></tr></thead><tbody>{content.items.map((item, i) => <tr key={i}><td>{item.name || "Chưa điền tên"}</td><td>{money(item.quantity)}</td><td>{money(item.unitPrice)}</td><td>{money(item.quantity * item.unitPrice)}</td></tr>)}</tbody></table> : <p className={s.missing}>Chưa có hạng mục hợp đồng</p>}
    <div className={s.totals}><div><span>Tạm tính</span><span>{money(totals.subtotal)} ₫</span></div><div><span>Giảm giá</span><span>{money(totals.discount)} ₫</span></div><div><span>VAT ({content.vatPercent}%)</span><span>{money(totals.vat)} ₫</span></div><div><span>Tổng giá trị</span><span>{money(totals.total)} ₫</span></div></div>
    <p style={{ fontSize: 11, color: "#5d6d70" }}>* Thành tiền trước giảm giá và VAT. Giá trị sẽ được kiểm tra lại khi lưu.</p>
    <h3>ĐIỀU KHOẢN HỢP ĐỒNG</h3>{content.terms.map((term, i) => <section key={term.key}><h3>Điều {i + 1}. {term.title}</h3><p>{term.content || <span className={s.missing}>Chưa soạn nội dung điều khoản</span>}</p></section>)}
    {!content.terms.length && <p className={s.missing}>Chưa có điều khoản</p>}
    <div className={s.signatures}><div><strong>ĐẠI DIỆN BÊN A</strong><p>(Chưa ký)</p></div><div><strong>ĐẠI DIỆN BÊN B</strong><p>(Chưa ký)</p></div></div>
  </article></div>;
}

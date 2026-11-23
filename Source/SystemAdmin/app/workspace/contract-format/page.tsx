"use client";
import { useEffect, useState } from "react";
import { RotateCcw, Save } from "lucide-react";
import { internalStyles as ui } from "@/components/internal/internal-theme";
import { paperStyle } from "@/components/internal/use-contract-format";
import s from "@/components/internal/workspace.module.css";
import { useInternalUser } from "@/components/internal/internal-shell";
import { internalApi, loadContractFormat, type ContractFormat } from "@/services/internal-api";

/** The layout used before the setting existed (Backend ContractFormat.Default). */
const DEFAULT_FORMAT: ContractFormat = { fontFamily: "Times New Roman", titleFontSize: 17, headingFontSize: 13, bodyFontSize: 13, marginTopMm: 20, marginBottomMm: 20,
  marginLeftMm: 30, marginRightMm: 20, lineSpacing: 1, titleUppercase: true, showNationalMotto: true };
const NUMBERS: { key: keyof ContractFormat; label: string; min: number; max: number; step: number; unit: string }[] = [
  { key: "titleFontSize", label: "Cỡ chữ tiêu đề", min: 10, max: 32, step: 0.5, unit: "pt" },
  { key: "headingFontSize", label: "Cỡ chữ đề mục (Điều…)", min: 9, max: 24, step: 0.5, unit: "pt" },
  { key: "bodyFontSize", label: "Cỡ chữ nội dung", min: 9, max: 20, step: 0.5, unit: "pt" },
  { key: "lineSpacing", label: "Giãn dòng", min: 1, max: 2, step: 0.05, unit: "lần" },
  { key: "marginTopMm", label: "Lề trên", min: 10, max: 50, step: 1, unit: "mm" },
  { key: "marginBottomMm", label: "Lề dưới", min: 10, max: 50, step: 1, unit: "mm" },
  { key: "marginLeftMm", label: "Lề trái", min: 10, max: 50, step: 1, unit: "mm" },
  { key: "marginRightMm", label: "Lề phải", min: 10, max: 50, step: 1, unit: "mm" },
];

export default function ContractFormatPage() {
  // Thiết lập tham số hợp đồng: one print format for every contract the system draws (Word templates keep their own layout).
  const { canManageTemplates } = useInternalUser();
  const [format, setFormat] = useState<ContractFormat | null>(null); const [fonts, setFonts] = useState<string[]>([]);
  const [error, setError] = useState(""); const [notice, setNotice] = useState(""); const [busy, setBusy] = useState(false);
  useEffect(() => { let active = true;
    void loadContractFormat(true).then(r => { if (active) { setFormat(r.format); setFonts(r.fonts); } }).catch(e => { if (active) setError(e.message); });
    return () => { active = false; }; }, []);
  if (!canManageTemplates) return <div className={s.page}><p role="alert" className={ui.error}>Bạn không có quyền thiết lập định dạng hợp đồng.</p></div>;
  const set = (patch: Partial<ContractFormat>) => { setNotice(""); setFormat(old => old && { ...old, ...patch }); };
  const save = () => { if (!format) return; setBusy(true); setError(""); setNotice("");
    void internalApi<ContractFormat>("/contract-format", "PUT", format).then(saved => { setFormat(saved); void loadContractFormat(true); setNotice("Đã lưu định dạng hợp đồng. Bản in mới sẽ dùng định dạng này."); })
      .catch(e => setError(e.message)).finally(() => setBusy(false)); };

  return <div className={s.page}><div className={s.titleRow}><div><span className={s.eyebrow}>Thư viện mẫu</span><h1>Định dạng hợp đồng</h1>
    <p className={s.muted}>Phông chữ, cỡ chữ, lề trang và giãn dòng của mọi hợp đồng do hệ thống soạn. Mẫu tải lên từ file Word giữ định dạng của file đó.</p></div>
    <div style={{ display: "flex", gap: 8 }}><button className={ui.secondaryButton} disabled={busy || !format} onClick={() => set(DEFAULT_FORMAT)}><RotateCcw size={16}/>Về mặc định</button>
      <button className={ui.primaryButton} disabled={busy || !format} onClick={save}><Save size={18}/>{busy ? "Đang lưu…" : "Lưu định dạng"}</button></div></div>
    {error && <p role="alert" className={ui.error} style={{ marginBottom: 16 }}>{error}</p>}
    {notice && <p role="status" className={ui.notice} style={{ marginBottom: 16 }}>{notice}</p>}
    {!format ? <section className={ui.card}><div className={s.empty}>{error ? "" : "Đang tải định dạng…"}</div></section>
    : <div style={{ display: "grid", gap: 16, gridTemplateColumns: "repeat(auto-fit, minmax(min(100%, 360px), 1fr))", alignItems: "start" }}>
      <section className={ui.card}><fieldset disabled={busy} className={s.fields} style={{ border: 0, padding: 0, margin: 0 }}>
        <label className={s.label}>Phông chữ<select className={ui.field} value={format.fontFamily} onChange={e => set({ fontFamily: e.target.value })}>
          {fonts.map(f => <option key={f} value={f}>{f}</option>)}</select></label>
        <div className={s.columns}>{NUMBERS.map(n => <label key={n.key} className={s.label}>{n.label} ({n.unit})
          <input type="number" className={ui.field} min={n.min} max={n.max} step={n.step} value={format[n.key] as number}
            onChange={e => set({ [n.key]: e.target.valueAsNumber || n.min } as Partial<ContractFormat>)}/></label>)}</div>
        <label className="flex items-center gap-2 text-sm"><input type="checkbox" checked={format.titleUppercase} onChange={e => set({ titleUppercase: e.target.checked })}/>Tên hợp đồng viết hoa</label>
        <label className="flex items-center gap-2 text-sm"><input type="checkbox" checked={format.showNationalMotto} onChange={e => set({ showNationalMotto: e.target.checked })}/>In quốc hiệu, tiêu ngữ ở đầu trang</label>
      </fieldset></section>
      <section aria-label="Xem trước định dạng" className={s.previewPane} style={{ height: "auto", borderRadius: 12 }}>
        <article className={s.paper} style={{ ...paperStyle(format), minHeight: 0 }}>
          {format.showNationalMotto && <><p style={{ textAlign: "center", fontWeight: 700 }}>CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM</p><p style={{ textAlign: "center" }}>Độc lập – Tự do – Hạnh phúc</p></>}
          <h2>Hợp đồng cung cấp dịch vụ</h2>
          <p style={{ textAlign: "center" }}>Số: HD-20261123-000001</p>
          <h3>Điều 1. Phạm vi công việc</h3>
          <p>Bên B cung cấp cho Bên A dịch vụ theo danh mục tại Điều 2. Các bên cam kết thực hiện đúng thời hạn và chất lượng đã thỏa thuận trong hợp đồng này.</p>
          <h3>Điều 2. Giá trị hợp đồng</h3>
          <p>Tổng giá trị hợp đồng là 100.000.000 đồng, đã bao gồm thuế giá trị gia tăng.</p>
        </article></section>
    </div>}
  </div>;
}

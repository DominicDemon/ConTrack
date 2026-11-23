import Link from "next/link";
import { Archive, FileScan } from "lucide-react";
import s from "@/components/internal/workspace.module.css";

/** "Tài liệu của tôi": scans of contracts signed with the company, and the customer's own vault. */
export default function DocumentTabs({ active }: { active: "scans" | "vault" }) {
  const tab = (key: "scans" | "vault", href: string, icon: React.ReactNode, label: string, hint: string) =>
    <Link href={href} role="tab" aria-selected={active === key} title={hint}
      style={{ display: "inline-flex", gap: 6, alignItems: "center", padding: "8px 14px", borderBottom: `2px solid ${active === key ? "var(--primary)" : "transparent"}`,
        fontWeight: active === key ? 650 : 500, color: active === key ? undefined : "var(--muted-foreground)", textDecoration: "none" }}>{icon}{label}</Link>;
  return <div style={{ marginBottom: 14 }}>
    <span className={s.eyebrow}>Tài liệu của tôi</span>
    <div role="tablist" style={{ display: "flex", gap: 4, borderBottom: "1px solid var(--border)", marginTop: 6, flexWrap: "wrap" }}>
      {tab("scans", "/portal/scans", <FileScan size={16}/>, "Bản scan hợp đồng với công ty", "Bản scan của hợp đồng đã ký với công ty; nhân viên phụ trách cũng xem được")}
      {tab("vault", "/portal/vault", <Archive size={16}/>, "Kho lưu trữ riêng", "Tài liệu bất kỳ của bạn, kể cả với đối tác khác; chỉ bạn xem được")}
    </div>
    <p className={s.muted} style={{ fontSize: 12, marginTop: 6 }}>{active === "scans"
      ? "Bản scan gắn với hợp đồng đã ký với công ty, nhân viên phụ trách hợp đồng cũng xem được."
      : "Tài liệu riêng của bạn, kể cả hợp đồng với đối tác khác; chỉ bạn mở được bằng key của mình."}</p>
  </div>;
}

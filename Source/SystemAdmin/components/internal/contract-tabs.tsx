"use client";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { useInternalUser } from "./internal-shell";
import { internalStyles as ui } from "./internal-theme";
import s from "./workspace.module.css";

/** The parts of Quản lý hợp đồng: every contract in any status, then the screens with their own actions (signing, payments, timeline). */
const TABS = [
  { href: "/workspace/contracts/drafts", label: "Tất cả hợp đồng", permissions: ["draft.write", "contract.check", "contract.approve", "contract.read", "legal.manage"] },
  { href: "/workspace/contracts/company-signing", label: "Chờ công ty ký", permissions: ["contract.approve", "legal.manage"] },
  { href: "/workspace/contracts/signed", label: "Đã ký", permissions: ["draft.write", "contract.check", "contract.approve"] },
  { href: "/workspace/contracts/signing", label: "Theo dõi ký", permissions: ["draft.manage.all", "contract.approve", "contract.read", "legal.manage"] },
];

export default function ContractTabs() {
  const user = useInternalUser(); const path = usePathname();
  const tabs = TABS.filter(t => t.permissions.some(p => user.permissions.includes(p)));
  if (tabs.length < 2) return null;
  return <nav aria-label="Quản lý hợp đồng" className={s.contractTabs}>
    {tabs.map(t => { const current = path === t.href || (t.href.endsWith("/drafts") ? false : path.startsWith(t.href + "/"));
      return <Link key={t.href} href={t.href} aria-current={current ? "page" : undefined} className={current ? ui.primaryButton : ui.secondaryButton}>{t.label}</Link>; })}
  </nav>;
}

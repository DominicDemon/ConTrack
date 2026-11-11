"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { useEffect, useState } from "react";
import { FileSignature, KeyRound, ScanLine, Search } from "lucide-react";
import { ENCRYPTION_KEY_PERMISSIONS, SCANNED_PERMISSIONS, SIGNING_PERMISSIONS, scanAccessApi } from "@/services/scans/auth";
import s from "./workspace.module.css";

const LINKS = [
  { label: "Bản ký giấy", href: "/workspace/signed-contracts", icon: FileSignature, permissions: [SIGNING_PERMISSIONS.view], exclude: "/workspace/signed-contracts/search" },
  { label: "Hợp đồng giấy scan", href: "/workspace/scanned-contracts", icon: ScanLine, permissions: [SCANNED_PERMISSIONS.view] },
  // Search finds both paper-signing evidence and scanned old contracts.
  { label: "Tìm hợp đồng", href: "/workspace/signed-contracts/search", icon: Search, permissions: [SIGNING_PERMISSIONS.view, SCANNED_PERMISSIONS.view] },
  { label: "Key mở tệp", href: "/workspace/encryption-keys", icon: KeyRound, permissions: [ENCRYPTION_KEY_PERMISSIONS.manage] },
];

/** Links to the paper-scan screens the signed-in employee may open. */
export function ScanNavLinks() {
  const pathname = usePathname() ?? "";
  const [permissions, setPermissions] = useState<string[]>([]);

  useEffect(() => {
    let cancelled = false;
    scanAccessApi.me().then(
      (me) => !cancelled && setPermissions(me.permissions),
      () => undefined,
    );
    return () => {
      cancelled = true;
    };
  }, []);

  return (
    <>
      {LINKS.filter((link) => link.permissions.some((permission) => permissions.includes(permission))).map((link) => {
        const Icon = link.icon;
        const active = pathname.startsWith(link.href) && !(link.exclude && pathname.startsWith(link.exclude));
        return (
          <Link key={link.href} href={link.href} className={active ? s.navActive : s.navItem}>
            <Icon size={19} />
            {link.label}
          </Link>
        );
      })}
    </>
  );
}

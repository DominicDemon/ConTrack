"use client";

import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from "react";
import { getScanErrorMessage } from "@/services/scans/http";
import { scanAccessApi, type ScanUser } from "@/services/scans/auth";

interface ScanAccessValue {
  user: ScanUser;
  can: (permission: string) => boolean;
}

const ScanAccessContext = createContext<ScanAccessValue | null>(null);

export function useScanAccess(): ScanAccessValue {
  const value = useContext(ScanAccessContext);
  if (!value) throw new Error("useScanAccess must be used inside ScanAccessProvider.");
  return value;
}

/** Loads what the signed-in employee may do on the paper-scan screens; sign-in itself is the workspace shell's. */
export function ScanAccessProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<ScanUser | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    scanAccessApi.me().then(
      (me) => !cancelled && setUser(me),
      (loadError: unknown) => !cancelled && setError(getScanErrorMessage(loadError, "Không tải được quyền của bạn.")),
    );
    return () => {
      cancelled = true;
    };
  }, []);

  const value = useMemo<ScanAccessValue | null>(
    () => (user ? { user, can: (permission) => user.permissions.includes(permission) } : null),
    [user],
  );

  if (!value) return <p className="p-6 text-sm text-muted-foreground">{error ?? "Đang tải…"}</p>;
  return <ScanAccessContext.Provider value={value}>{children}</ScanAccessContext.Provider>;
}

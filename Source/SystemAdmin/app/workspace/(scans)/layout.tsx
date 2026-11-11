import type { ReactNode } from "react";
import { FileKeyProvider } from "@/components/internal/file-key";
import { ScanAccessProvider } from "@/components/internal/scan-access";

/** The paper-scan screens, inside the workspace shell. */
export default function ScansLayout({ children }: { children: ReactNode }) {
  return (
    <ScanAccessProvider>
      <FileKeyProvider>
        <div className="space-y-6 p-2 lg:p-6">{children}</div>
      </FileKeyProvider>
    </ScanAccessProvider>
  );
}

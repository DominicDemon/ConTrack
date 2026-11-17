"use client";

import { useRef, useState } from "react";
import { Download, Eye, Loader2 } from "lucide-react";
import { blobErrorMessage, FileKeyCancelled, useFileKey } from "@/components/internal/file-key";
import { Button } from "@/components/ui/button";
import { toast } from "@/components/ui/sonner";

const BY_EXTENSION: Record<string, string> = { pdf: "application/pdf", png: "image/png", jpg: "image/jpeg", jpeg: "image/jpeg", gif: "image/gif", webp: "image/webp" };
/** Types a browser tab shows by itself: a scanned PDF or a photo of the paper. */
const VIEWABLE = /^(application\/pdf|image\/(png|jpeg|gif|webp))$/;

/** Download and view buttons for a scan file, which may be encrypted: the employee's 12-character key is asked when needed. */
export function ScanFileButtons({ load, fileName, contentType }: {
  load: (key: string | undefined) => Promise<Blob>;
  fileName: string;
  contentType?: string | null;
}) {
  const { ensureKey, withFileKey } = useFileKey();
  const [opening, setOpening] = useState(false);
  // The opened file is kept for this page, so viewing after a download needs neither the key nor the server again.
  const opened = useRef<Blob | null>(null);
  const type = (contentType && contentType !== "application/octet-stream" ? contentType : null)
    ?? BY_EXTENSION[fileName.split(".").pop()?.toLowerCase() ?? ""] ?? "application/octet-stream";
  const viewable = VIEWABLE.test(type);

  const open = async (download: boolean) => {
    let preview: Window | null = null;
    setOpening(true);
    try {
      // The key is asked first, so the new tab is still opened from the click that gave it.
      if (viewable && !download) {
        if (!opened.current) await ensureKey();
        preview = window.open("about:blank", "_blank");
      }
      const blob = opened.current ?? await withFileKey(load, {
        beforeAsk: () => preview?.close(),
        afterAsk: () => {
          if (viewable && !download) preview = window.open("about:blank", "_blank");
        },
      });
      opened.current = blob;
      const url = URL.createObjectURL(new Blob([blob], { type }));
      if (preview) {
        preview.opener = null;
        preview.location.replace(url);
      } else {
        const link = document.createElement("a");
        link.href = url;
        link.download = fileName;
        link.click();
      }
      window.setTimeout(() => URL.revokeObjectURL(url), 60_000);
    } catch (openError) {
      preview?.close();
      if (!(openError instanceof FileKeyCancelled)) toast.error(await blobErrorMessage(openError, "Không mở được tệp."));
    } finally {
      setOpening(false);
    }
  };

  return (
    <>
      <Button variant="outline" size="sm" disabled={opening} onClick={() => void open(true)}>
        <Download className="size-4" />
        Tải về
      </Button>
      {viewable && (
        <Button variant="outline" size="sm" disabled={opening} onClick={() => void open(false)}>
          {opening ? <Loader2 className="size-4 animate-spin" /> : <Eye className="size-4" />}
          Xem
        </Button>
      )}
    </>
  );
}

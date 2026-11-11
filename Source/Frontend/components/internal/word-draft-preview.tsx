"use client";
import WordPdfViewer from "./word-pdf-viewer";
import { useEffect, useRef, useState } from "react";
import { Button } from "@/components/ui/button";
import { internalApi, type DraftContent } from "@/services/internal-api";
import { wordBlob } from "./template-upload";

export default function WordDraftPreview({ content, draftId }: { content: DraftContent; draftId?: string }) {
  const [url, setUrl] = useState("");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const run = useRef(0);
  const signature = JSON.stringify([draftId, content]);
  useEffect(() => () => { if (url) URL.revokeObjectURL(url); }, [url]);
  async function render(pdf: boolean) {
    // Empty form IDs are not valid BIGINT strings. Zero means not selected for preview only.
    const previewContent = { ...content, templateId: String(content.templateId || "0"), customerId: String(content.customerId || "0"),
      templateSnapshot: null, customerBusinessId: content.customerBusinessId ? String(content.customerBusinessId) : null,
      items: content.items.map(item => ({...item, productId: item.productId ? String(item.productId) : null})) };
    return internalApi<{ base64: string; contentType: string }>("/contract-drafts/word-preview", "POST",
      { draftId: draftId ? String(draftId) : null, content: previewContent, pdf });
  }
  const shown = useRef("");
  // Real-time print: the PDF is rebuilt with the draft data shortly after the last change; an older result is discarded.
  useEffect(() => {
    const current = ++run.current;
    const timer = setTimeout(async () => {
      setBusy(true);
      try {
        const result = await render(true);
        if (current !== run.current) return;
        setError("");
        // An unchanged print keeps the open viewer (and its scroll position) instead of reloading it.
        if (result.base64 !== shown.current) { shown.current = result.base64; setUrl(URL.createObjectURL(wordBlob(result.base64, result.contentType))); }
      }
      catch (e) { if (current === run.current) setError((e as Error).message); }
      finally { if (current === run.current) setBusy(false); }
    }, 400);
    return () => clearTimeout(timer);
    // eslint-disable-next-line react-hooks/exhaustive-deps -- render() reads exactly content and draftId
  }, [signature]);
  async function download() {
    setError("");
    try {
      const result = await render(false);
      const objectUrl = URL.createObjectURL(wordBlob(result.base64, result.contentType));
      const anchor = document.createElement("a"); anchor.href = objectUrl; anchor.download = "hop-dong.docx"; anchor.click();
      setTimeout(() => URL.revokeObjectURL(objectUrl), 1000);
    } catch (e) { setError((e as Error).message); }
  }
  return <div className="p-4 space-y-3">
    <div className="flex items-center gap-2"><Button type="button" variant="outline" onClick={() => void download()}>Tải DOCX</Button>
      <p role="status" className="text-sm">{busy ? "Đang cập nhật bản in…" : url ? "Bản in đã cập nhật theo nội dung đang soạn." : ""}</p></div>
    {error && <p role="alert" className="text-red-700">{error}</p>}
    {url && <WordPdfViewer key={url} url={url}/>}
  </div>;
}

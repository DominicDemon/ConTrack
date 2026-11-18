"use client";
import WordPdfViewer from "./word-pdf-viewer";
import { useEffect, useState } from "react";
import { Button } from "@/components/ui/button";
import { internalApi, type DraftContent } from "@/services/internal-api";
import { wordBlob } from "./template-upload";

export default function WordDraftPreview({ content, draftId, dirty = false, downloadOnly = false }: { downloadOnly?:boolean; content: DraftContent; draftId?: string; dirty?: boolean }) {
  const [url, setUrl] = useState("");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const [renderedContent, setRenderedContent] = useState("");
  const needsSave = !!draftId && dirty;
  const signature = content.templateSnapshot?.manualWordContent
    ? JSON.stringify([draftId,content.templateId,content.templateSnapshot.wordSha256, content.contractNumber, content.paymentMethod, content.items, content.vatPercent, content.milestones]) : JSON.stringify(content);
  useEffect(() => () => { if (url) URL.revokeObjectURL(url); }, [url]);
  async function render(pdf: boolean) {
    setBusy(true); setError("");
    try {
      // Empty form IDs are not valid BIGINT strings. Zero means not selected for preview only.
      const previewContent = { ...content, templateId: String(content.templateId || "0"), customerId: String(content.customerId || "0"),
        templateSnapshot: null, customerBusinessId: content.customerBusinessId ? String(content.customerBusinessId) : null,
        items: content.items.map(item => ({...item, productId: item.productId ? String(item.productId) : null})) };
      const storedTemplate = !draftId && content.templateSnapshot?.manualWordContent;
      const result = await internalApi<{ base64: string; contentType: string }>(storedTemplate
        ? `/contract-templates/${encodeURIComponent(String(content.templateId))}/word-preview` : "/contract-drafts/word-preview", "POST",
        storedTemplate ? { pdf } : { draftId: draftId ? String(draftId) : null, content: previewContent, pdf });
      const objectUrl = URL.createObjectURL(wordBlob(result.base64, result.contentType));
      if (pdf) { setUrl(objectUrl); setRenderedContent(signature); }
      else {
        const anchor = document.createElement("a"); anchor.href = objectUrl; anchor.download = "hop-dong.docx"; anchor.click();
        setTimeout(() => URL.revokeObjectURL(objectUrl), 1000);
      }
    } catch (e) { setError((e as Error).message); } finally { setBusy(false); }
  }
  return <div className="p-4 space-y-3">
    <div className="flex gap-2">{!downloadOnly && <Button type="button" disabled={busy || needsSave} onClick={() => void render(true)}>Xem trước Word</Button>}
      <Button type="button" variant="outline" disabled={busy || needsSave || (downloadOnly && dirty)} onClick={() => void render(false)}>Tải DOCX</Button></div>
    {needsSave && <p>Lưu thay đổi để xem bản Word với số liệu mới.</p>}
    {busy && <p role="status">Đang dựng tài liệu…</p>}
    {error && <p role="alert" className="text-red-700">{error}</p>}
    {!needsSave && url && renderedContent !== signature && <p>Nội dung đã thay đổi. Bấm xem trước để cập nhật.</p>}
    {!needsSave && url && renderedContent === signature && <WordPdfViewer key={url} url={url}/>}
  </div>;
}

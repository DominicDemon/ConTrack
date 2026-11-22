"use client";
import { useEffect, useRef, useState } from "react";

export default function WordPdfViewer({ url }: { url: string }) {
  const container = useRef<HTMLDivElement>(null);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(true);
  useEffect(() => {
    let cancelled = false;
    let dispose: (() => void) | undefined;
    const host = container.current;
    host?.replaceChildren();

    void (async () => {
      // Legacy build: the modern build of pdf.js 6.5 mis-maps the glyphs of LibreOffice subset fonts in Chrome
      // (Vietnamese text shows as `H ! " Đ#NG`); the same file renders correctly with the legacy build.
      const pdfjs = await import("pdfjs-dist/legacy/build/pdf.mjs");
      const worker = new Worker(new URL("pdfjs-dist/legacy/build/pdf.worker.min.mjs", import.meta.url), { type: "module" });
      const pdfWorker = pdfjs.PDFWorker.create({ port: worker });
      const task = pdfjs.getDocument({ url, worker: pdfWorker });
      dispose = () => { void task.destroy(); worker.terminate(); };
      if (cancelled) { dispose(); return; }
      const document = await task.promise;
      for (let number = 1; number <= document.numPages; number++) {
        if (cancelled) break;
        const page = await document.getPage(number);
        const viewport = page.getViewport({ scale: 1.5 });
        const canvas = window.document.createElement("canvas");
        canvas.width = viewport.width; canvas.height = viewport.height;
        canvas.style.width = "100%"; canvas.style.height = "auto"; canvas.style.marginBottom = "12px";
        canvas.setAttribute("role", "img"); canvas.setAttribute("aria-label", "Trang PDF " + number);
        if (!cancelled) host?.append(canvas);
        await page.render({ canvas, viewport }).promise;
      }
      if (!cancelled) setLoading(false);
    })().catch(e => { if (!cancelled) { setError("Không hiển thị được PDF. Dùng liên kết mở PDF để xem. " + (e instanceof Error ? e.message : "")); setLoading(false); } });
    return () => { cancelled = true; dispose?.(); };
  }, [url]);
  return <div>
    {loading && <p role="status">Đang hiển thị PDF…</p>}
    {error && <p role="alert">{error}</p>}
    <div ref={container} className="max-h-[65vh] overflow-auto bg-gray-200 p-2" aria-label="Bản xem trước PDF"/>
  </div>;
}

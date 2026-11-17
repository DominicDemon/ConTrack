"use client";
import { useEffect, useState } from "react";
import { customerApi } from "@/services/customer-api";
import WordPdfViewer from "@/components/internal/word-pdf-viewer";

export default function ReviewWord({ id }: { id: string }) {
  const [url, setUrl] = useState("");
  const [error, setError] = useState("");
  useEffect(() => {
    let active = true; let created = "";
    customerApi<{base64:string;contentType:string}>(`/reviews/${id}/word`).then(data => {
      if (!active) return;
      created = URL.createObjectURL(new Blob([Uint8Array.from(atob(data.base64), c => c.charCodeAt(0))], {type:data.contentType}));
      setUrl(created);
    }).catch(e => { if (active) setError(e.message); });
    return () => {active = false; if(created) URL.revokeObjectURL(created);};
  }, [id]);
  return <section aria-label="Bản hợp đồng đã duyệt">{error ? <p role="alert">{error}</p> : url ? <WordPdfViewer url={url}/> : <p>Đang dựng bản hợp đồng…</p>}</section>;
}

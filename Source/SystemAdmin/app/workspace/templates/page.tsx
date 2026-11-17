"use client";
import { useEffect, useState } from "react";
import { useInternalUser } from "@/components/internal/internal-shell";
import { TemplateChecklist } from "@/components/internal/checklist-panel";
import TemplateUpload from "@/components/internal/template-upload";
import { internalApi, type TemplateDefinition } from "@/services/internal-api";

export default function TemplatesPage() {
  const user = useInternalUser();
  const [templates, setTemplates] = useState<TemplateDefinition[]>([]);
  const [error, setError] = useState("");
  useEffect(() => { internalApi<TemplateDefinition[]>("/contract-templates").then(setTemplates).catch(e => setError(e.message)); }, []);
  if (!user.canManageTemplates) return <p>Bạn không có quyền quản lý mẫu.</p>;
  return <main style={{padding:24}}><h1>Mẫu hợp đồng</h1><TemplateUpload onImported={t => setTemplates(old => [t,...old])}/>{error && <p role="alert">{error}</p>}<ul>{templates.map(t => <li key={t.id}>{t.name}<TemplateChecklist id={t.id}/></li>)}</ul></main>;
}

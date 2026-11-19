"use client";

import { useEffect, useState } from "react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { FileText, Pencil, Search } from "lucide-react";
import { useInternalUser } from "@/components/internal/internal-shell";
import TemplateUpload from "@/components/internal/template-upload";
import { internalApi, type TemplateDefinition } from "@/services/internal-api";
import styles from "./templates.module.css";

function searchText(value: string) {
  return value.toLowerCase().normalize("NFD").replace(/\p{M}/gu, "").replaceAll("đ", "d").trim();
}

export default function TemplatesPage() {
  const { canManageTemplates } = useInternalUser();
  const [templates, setTemplates] = useState<TemplateDefinition[]>([]);
  const [query, setQuery] = useState("");
  const [source, setSource] = useState("");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [attempt, setAttempt] = useState(0);
  const [editing, setEditing] = useState<TemplateDefinition | null>(null);
  const [name, setName] = useState("");
  const [saving, setSaving] = useState(false);
  const [renameError, setRenameError] = useState("");

  async function saveName() {
    if (!editing || saving || !name.trim()) return;
    setSaving(true);
    setRenameError("");
    try {
      const result = await internalApi<{ name: string }>(`/contract-templates/${editing.id}/name`, "PATCH", { name });
      setTemplates(old => old.map(template => template.id === editing.id ? { ...template, name: result.name } : template));
      setEditing(null);
    } catch (error) {
      setRenameError((error as Error).message);
    } finally {
      setSaving(false);
    }
  }

  useEffect(() => {
    if (!canManageTemplates) return;
    let active = true;
    internalApi<TemplateDefinition[]>("/contract-templates")
      .then(result => { if (active) setTemplates(result); })
      .catch(err => { if (active) setError(err.message); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [attempt, canManageTemplates]);

  if (!canManageTemplates) return <p className={styles.page}>Bạn không có quyền quản lý mẫu.</p>;

  const needle = searchText(query);
  const matches = templates.filter(template =>
    searchText(template.name).includes(needle) &&
    (!source || (source === "word") === Boolean(template.wordStorageKey)));

  return <main className={styles.page}>
    <header className={styles.header}>
      <div><h1>Mẫu hợp đồng</h1><p>Tải lên, tìm kiếm và đổi tên mẫu hợp đồng.</p></div>
      <TemplateUpload onImported={template => setTemplates(old => [template, ...old.filter(item => item.id !== template.id)])}/>
    </header>
    <section className={styles.library} aria-label="Danh sách mẫu hợp đồng">
      <div className={styles.toolbar}>
        <label className={styles.search}><Search size={18} aria-hidden="true"/>
          <input type="search" aria-label="Tìm theo tên mẫu" placeholder="Tìm theo tên mẫu…" value={query} onChange={event => setQuery(event.target.value)}/>
        </label>
        <select aria-label="Lọc theo nguồn mẫu" value={source} onChange={event => setSource(event.target.value)}>
          <option value="">Tất cả nguồn</option>
          <option value="word">Tệp Word</option>
          <option value="system">Mẫu hệ thống</option>
        </select>
        {!loading && !error && <span role="status">{matches.length} / {templates.length} mẫu</span>}
      </div>
      {loading ? <p role="status" className={styles.state}>Đang tải mẫu hợp đồng…</p> : error ?
        <div className={styles.state}><p role="alert">{error}</p><button type="button" onClick={() => { setError(""); setLoading(true); setAttempt(n => n + 1); }}>Thử lại</button></div> :
        matches.length === 0 ? <p className={styles.state}>{templates.length ? "Không tìm thấy mẫu phù hợp." : "Chưa có mẫu hợp đồng."}</p> :
        <ul className={styles.list}>
          {matches.map(template => <li key={template.id} className={styles.item}>
            <div className={styles.identity}>
              <div className={styles.icon}><FileText size={20} aria-hidden="true"/></div>
              <div><h2>{template.name}</h2><p>{template.wordStorageKey ? "Tệp Word" : "Mẫu hệ thống"} · ID {template.id}</p></div>
            </div>
            <div className={styles.actions}>
              <Button type="button" variant="outline" size="sm" onClick={() => {
                setName(template.name);
                setRenameError("");
                setEditing(template);
              }}><Pencil size={14} aria-hidden="true"/> Chỉnh sửa</Button>
            </div>
          </li>)}
        </ul>}
    </section>
    <Dialog open={editing !== null} onOpenChange={open => { if (!open && !saving) setEditing(null); }}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Chỉnh sửa tên mẫu</DialogTitle>
          <DialogDescription>ID mẫu {editing?.id}</DialogDescription>
        </DialogHeader>
        <form onSubmit={event => { event.preventDefault(); void saveName(); }} className={styles.renameForm}>
          <label htmlFor="template-name">Tên mẫu</label>
          <Input id="template-name" value={name} onChange={event => setName(event.target.value)} maxLength={500} required disabled={saving}/>
          {renameError && <p role="alert">{renameError}</p>}
          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => setEditing(null)} disabled={saving}>Hủy</Button>
            <Button type="submit" disabled={saving || !name.trim()}>{saving ? "Đang lưu…" : "Lưu"}</Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  </main>;
}

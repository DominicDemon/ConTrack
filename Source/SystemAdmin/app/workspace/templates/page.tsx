"use client";
import { useEffect, useState } from "react";
import { ListPager } from "@/components/scanned-contracts/list-pager";
import Link from "next/link";
import { EyeOff, FilePlus2, Files, FileCheck2, RefreshCw, MoreHorizontal } from "lucide-react";
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuSeparator, DropdownMenuTrigger } from "@/components/ui/dropdown-menu";
import { SummaryCards } from "@/components/ui/custom/summary-cards";
import { internalStyles as ui } from "@/components/internal/internal-theme";
import { TemplateInfoDialog } from "@/components/internal/catalog-dialogs";
import TemplateUpload from "@/components/internal/template-upload";
import s from "@/components/internal/workspace.module.css";
import { useInternalUser } from "@/components/internal/internal-shell";
import { internalApi, dateTime, documentTypeLabel, type TemplateSummary } from "@/services/internal-api";
import { typeOptions, useDocumentCategories } from "@/components/internal/use-document-categories";
import { SortHeader, useListView } from "@/components/internal/list-tools";

export default function TemplatesPage() {
  // Every document template (contract, appendix, handover record…), hidden ones included; Hành chính manages them.
  const { canManageTemplates } = useInternalUser(); const categories = useDocumentCategories();
  const [rows, setRows] = useState<TemplateSummary[]>([]); const [search, setSearch] = useState("");
  const [type, setType] = useState(""); const [status, setStatus] = useState(""); const [parent, setParent] = useState("");
  const [error, setError] = useState(""); const [notice, setNotice] = useState(""); const [busy, setBusy] = useState(true); const [acting, setActing] = useState(false); const [reload, setReload] = useState(0);
  const [upload, setUpload] = useState<{ row: TemplateSummary; replace: boolean } | null>(null);
  const [editing, setEditing] = useState<TemplateSummary | null>(null); const [editingWord, setEditingWord] = useState<TemplateSummary | null>(null);
  useEffect(() => { if (!canManageTemplates) return; let active = true;
    void internalApi<TemplateSummary[]>("/contract-template-library").then(list => { if (active) setRows(list); })
      .catch(e => { if (active) setError(e.message); }).finally(() => { if (active) setBusy(false); });
    return () => { active = false; }; }, [reload, canManageTemplates]);
  const list = useListView(rows, { search, fields: r => [r.name, r.parentName, r.id], filterKey: [type, status, parent],
    filter: r => (!type || r.documentType === Number(type)) && (!status || (status === "ACTIVE") === r.active)
      && (!parent || (parent === "NONE" ? !r.parentTemplateId : r.parentTemplateId === parent || r.id === parent)),
    sorts: { name: r => r.name, type: r => documentTypeLabel(r.documentType), parent: r => r.parentName, used: r => r.documentCount,
      status: r => r.active, updated: r => r.updatedAt ?? r.createdAt }, initial: { key: "updated", dir: "desc" } });

  if (!canManageTemplates) return <div className={s.page}><p role="alert" className={ui.error}>Bạn không có quyền quản lý mẫu văn bản.</p></div>;

  const contracts = rows.filter(r => r.documentType === 1);
  const hidden = rows.filter(r => !r.active).length;
  const filtered = !!(search || type || status || parent);
  const replace = (row: TemplateSummary) => setRows(list => list.map(r => r.id === row.id ? row : r));
  const refresh = () => { setBusy(true); setError(""); setReload(n => n + 1); };
  const saved = (message: string) => { setNotice(message); refresh(); };
  const toggle = (row: TemplateSummary) => {
    if (!confirm(row.active
      ? `Ẩn mẫu ${row.name}? Mẫu sẽ không còn để chọn khi tạo văn bản mới; ${row.documentCount} văn bản đã tạo vẫn mở và in bình thường.`
      : `Khôi phục mẫu ${row.name}? Mẫu sẽ lại có trong danh sách chọn khi tạo văn bản.`)) return;
    setActing(true); setError(""); setNotice("");
    const request = row.active
      ? internalApi<void>(`/contract-templates/${row.id}`, "DELETE").then(() => ({ ...row, active: false }))
      : internalApi<TemplateSummary>(`/contract-templates/${row.id}/restore`, "POST");
    void request.then(r => { replace(r); setNotice(r.active ? `Đã khôi phục ${r.name}.` : `Đã ẩn ${r.name}.`); }).catch(e => setError(e.message)).finally(() => setActing(false)); };

  return <div className={s.page}><div className={s.titleRow}><div><span className={s.eyebrow}>Thư viện mẫu</span><h1>Quản lý văn bản</h1><p className={s.muted}>Mọi mẫu hợp đồng, phụ lục, biên bản bàn giao, biên lai và đề nghị thanh toán. Gắn mẫu con vào mẫu hợp đồng để tạo văn bản đi kèm hợp đồng.</p></div>
    <div className={s.actions}>
      <TemplateUpload onImported={t => saved(`Đã lưu mẫu ${t.name}.`)}
        trigger={open => <button className={ui.primaryButton} disabled={busy} onClick={open}><FilePlus2 size={18}/>Tạo mẫu văn bản</button>}/>
    </div></div>
    <div style={{ marginBottom: 24 }}><SummaryCards isLoading={busy} items={[
      { title: "Mẫu văn bản", value: rows.length, icon: <Files size={24}/>, iconWrapperClassName: "bg-blue-100 text-blue-700 dark:bg-blue-500/15 dark:text-blue-400" },
      { title: "Đang dùng", value: rows.length - hidden, icon: <FileCheck2 size={24}/>, iconWrapperClassName: "bg-emerald-100 text-emerald-700 dark:bg-emerald-500/15 dark:text-emerald-400" },
      { title: "Đã ẩn", value: hidden, icon: <EyeOff size={24}/>, iconWrapperClassName: "bg-amber-100 text-amber-700 dark:bg-amber-500/15 dark:text-amber-400" },
    ]}/></div>
    {error && <p role="alert" className={ui.error} style={{ marginBottom: 16 }}>{error}</p>}
    {notice && <p role="status" className={ui.notice} style={{ marginBottom: 16 }}>{notice}</p>}
    <section className={ui.card}><div className={s.toolbar}><input aria-label="Tìm mẫu văn bản" className={ui.field} placeholder="Tìm tên mẫu hoặc mẫu hợp đồng cha…" value={search} onChange={e => { setSearch(e.target.value); }}/>
      <select aria-label="Lọc loại văn bản" className={ui.field} style={{ maxWidth: 220 }} value={type} onChange={e => { setType(e.target.value); }}><option value="">Tất cả loại văn bản</option>{typeOptions(categories, null, true).map(c => <option key={c.id} value={c.id}>{c.name}</option>)}</select>
      <select aria-label="Lọc trạng thái" className={ui.field} style={{ maxWidth: 220 }} value={status} onChange={e => { setStatus(e.target.value); }}><option value="">Tất cả trạng thái</option><option value="ACTIVE">Đang dùng</option><option value="HIDDEN">Đã ẩn</option></select>
      <select aria-label="Lọc theo mẫu hợp đồng cha" className={ui.field} style={{ maxWidth: 240 }} value={parent} onChange={e => { setParent(e.target.value); }}><option value="">Mọi mẫu cha</option><option value="NONE">Không gắn mẫu cha</option>{contracts.map(c => <option key={c.id} value={c.id}>{c.name} và mẫu con</option>)}</select>
      <button className={ui.secondaryButton} disabled={busy} onClick={refresh} aria-label="Làm mới"><RefreshCw size={16}/></button></div>
      {busy ? <div className={s.empty}>Đang tải mẫu văn bản…</div> : list.total === 0 ? <div className={s.empty}><Files size={42} strokeWidth={1.3}/><strong>{filtered ? "Không tìm thấy mẫu phù hợp" : "Chưa có mẫu văn bản"}</strong><p>{filtered ? "Thử đổi từ khóa hoặc bộ lọc." : "Tạo mẫu văn bản đầu tiên từ một file Word."}</p></div>
      : <div className={s.tableWrap}><table className={s.table}><thead><tr><SortHeader label="Mẫu văn bản" column="name" sort={list.sort} onSort={list.sortBy}/><SortHeader label="Loại" column="type" sort={list.sort} onSort={list.sortBy}/>
          <SortHeader label="Mẫu hợp đồng cha" column="parent" sort={list.sort} onSort={list.sortBy}/><SortHeader label="Đã dùng" column="used" sort={list.sort} onSort={list.sortBy} firstDir="desc"/>
          <SortHeader label="Trạng thái" column="status" sort={list.sort} onSort={list.sortBy} firstDir="desc"/><SortHeader label="Cập nhật" column="updated" sort={list.sort} onSort={list.sortBy} firstDir="desc"/><th><span className="sr-only">Thao tác</span></th></tr></thead>
        <tbody>{list.rows.map(r => <tr key={r.id}><td><strong>{r.name}</strong><div className={s.muted} style={{ fontSize: 11, marginTop: 4 }}>{r.word ? "Tệp Word" : "Mẫu hệ thống"} · ID {r.id}</div></td>
          <td>{documentTypeLabel(r.documentType)}{r.childCount > 0 && <div className={s.muted} style={{ fontSize: 11, marginTop: 4 }}>{r.childCount} mẫu con</div>}</td>
          <td>{r.parentName ?? <span className={s.muted}>—</span>}</td>
          <td>{r.documentCount ? <span className={s.badge}>{r.documentCount} văn bản</span> : <span className={s.muted}>Chưa dùng</span>}</td>
          <td><span className={`${s.badge} ${r.active ? "" : s.warning}`}>{r.active ? "Đang dùng" : "Đã ẩn"}</span></td>
          <td className={s.muted}>{dateTime(r.updatedAt ?? r.createdAt)}</td>
          <td><div style={{ display: "flex", gap: 8, justifyContent: "flex-end", whiteSpace: "nowrap" }}>
            {r.active ? <>
              <button className={ui.secondaryButton} disabled={acting} onClick={() => setEditingWord(r)}>Sửa nội dung</button>
              <DropdownMenu>
                <DropdownMenuTrigger asChild><button className={ui.secondaryButton} disabled={acting} aria-label={`Thao tác cho ${r.name}`}><MoreHorizontal size={18}/></button></DropdownMenuTrigger>
                <DropdownMenuContent align="end">
                  <DropdownMenuItem onSelect={() => setEditing(r)}>Sửa thông tin mẫu</DropdownMenuItem>
                  <DropdownMenuItem asChild><Link href={`/workspace/templates/${r.id}/format`}>Thiết lập tham số hợp đồng</Link></DropdownMenuItem>
                  {r.word && <DropdownMenuItem onSelect={() => setUpload({ row: r, replace: true })}>Thay file Word</DropdownMenuItem>}
                  {r.documentType === 1 && <DropdownMenuItem onSelect={() => setUpload({ row: r, replace: false })}>Tạo mẫu con</DropdownMenuItem>}
                  <DropdownMenuSeparator/>
                  <DropdownMenuItem onSelect={() => toggle(r)}>Ẩn mẫu</DropdownMenuItem>
                </DropdownMenuContent>
              </DropdownMenu>
            </> : <button className={ui.secondaryButton} disabled={acting} onClick={() => toggle(r)}>Khôi phục</button>}
          </div></td></tr>)}</tbody></table></div>}
      {!busy && <ListPager page={list.page} pageSize={list.pageSize} total={list.total} onPage={list.setPage} label="mẫu văn bản"/>}
    </section>
    {upload && <TemplateUpload key={`${upload.row.id}-${upload.replace}`} startOpen
      replace={upload.replace ? { id: upload.row.id, name: upload.row.name, documentType: upload.row.documentType } : undefined}
      parent={upload.replace ? undefined : { id: upload.row.id, name: upload.row.name }}
      trigger={() => null} onClose={() => setUpload(null)} onImported={t => saved(`Đã lưu mẫu ${t.name}.`)}/>}
    {editingWord && <TemplateUpload key={editingWord.id} editTemplate={editingWord} onClose={() => setEditingWord(null)} onImported={t => saved(`Đã lưu nội dung mẫu ${t.name}.`)}/>}
    {editing && <TemplateInfoDialog template={editing} parents={rows} onClose={() => setEditing(null)}
      onSaved={row => { setEditing(null); replace(row); setNotice(`Đã cập nhật ${row.name}.`); }}/>}
  </div>;
}

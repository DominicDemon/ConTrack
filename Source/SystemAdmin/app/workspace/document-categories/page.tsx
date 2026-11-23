"use client";
import { useEffect, useState } from "react";
import { EyeOff, FileCheck2, FilePlus2, Files, RefreshCw } from "lucide-react";
import { SummaryCards } from "@/components/ui/custom/summary-cards";
import { internalStyles as ui } from "@/components/internal/internal-theme";
import { DocumentCategoryDialog } from "@/components/internal/catalog-dialogs";
import s from "@/components/internal/workspace.module.css";
import { useInternalUser } from "@/components/internal/internal-shell";
import { internalApi, loadDocumentCategories, type DocumentCategory } from "@/services/internal-api";

/** One line describing what a document of this type needs before it is sent. */
function needs(c: DocumentCategory) {
  const r = c.rule, parts: string[] = [];
  if (r.effectiveRequired) parts.push(r.effectiveLabel);
  if (r.expireShown && r.expireRequired) parts.push(r.expireLabel);
  if (r.itemsRequired) parts.push(r.itemsLabel);
  if (r.amountRequired) parts.push("số tiền > 0");
  if (r.milestones && r.milestonesRequired !== false) parts.push(r.milestonesRequired === null ? "lịch thanh toán khi có dòng" : "lịch thanh toán");
  if (r.parentMilestone) parts.push("đợt thanh toán của hợp đồng");
  return parts.length ? parts.join(" · ") : "Chỉ các trường bắt buộc của mẫu";
}

export default function DocumentCategoriesPage() {
  // Document types Hành chính adds beside the five built-in ones; each carries what a document of it needs before it is sent.
  const { canManageTemplates } = useInternalUser();
  const [rows, setRows] = useState<DocumentCategory[]>([]); const [error, setError] = useState(""); const [notice, setNotice] = useState("");
  const [busy, setBusy] = useState(true); const [reload, setReload] = useState(0); const [acting, setActing] = useState(false);
  const [editing, setEditing] = useState<DocumentCategory | "new" | null>(null);
  useEffect(() => { let active = true;
    void loadDocumentCategories(true).then(list => { if (active) setRows(list); })
      .catch(e => { if (active) setError(e.message); }).finally(() => { if (active) setBusy(false); });
    return () => { active = false; }; }, [reload]);

  if (!canManageTemplates) return <div className={s.page}><p role="alert" className={ui.error}>Bạn không có quyền quản lý loại văn bản.</p></div>;
  const refresh = () => { setBusy(true); setError(""); setReload(n => n + 1); };
  const remove = (row: DocumentCategory) => {
    if (!confirm(`Xóa loại ${row.name}? Chỉ xóa được loại chưa có mẫu hay văn bản nào dùng.`)) return;
    setActing(true); setError(""); setNotice("");
    void internalApi<void>(`/document-categories/${row.id}`, "DELETE").then(() => { setNotice(`Đã xóa ${row.name}.`); refresh(); })
      .catch(e => setError(e.message)).finally(() => setActing(false));
  };

  return <div className={s.page}><div className={s.titleRow}><div><span className={s.eyebrow}>Thư viện mẫu</span><h1>Loại văn bản</h1><p className={s.muted}>Mỗi loại quy định tên các ngày, bảng nội dung và những gì phải có trước khi gửi khách. Mẫu văn bản chọn một loại khi tạo.</p></div>
    <div style={{ display: "flex", gap: 8 }}><button className={ui.secondaryButton} disabled={busy} onClick={refresh} aria-label="Làm mới"><RefreshCw size={16}/></button>
      <button className={ui.primaryButton} disabled={busy} onClick={() => setEditing("new")}><FilePlus2 size={18}/>Thêm loại văn bản</button></div></div>
    <div style={{ marginBottom: 24 }}><SummaryCards isLoading={busy} items={[
      { title: "Loại văn bản", value: rows.length, icon: <Files size={24}/>, iconWrapperClassName: "bg-blue-100 text-blue-700 dark:bg-blue-500/15 dark:text-blue-400" },
      { title: "Đang dùng", value: rows.filter(r => r.active).length, icon: <FileCheck2 size={24}/>, iconWrapperClassName: "bg-emerald-100 text-emerald-700 dark:bg-emerald-500/15 dark:text-emerald-400" },
      { title: "Đã ẩn", value: rows.filter(r => !r.active).length, icon: <EyeOff size={24}/>, iconWrapperClassName: "bg-amber-100 text-amber-700 dark:bg-amber-500/15 dark:text-amber-400" },
    ]}/></div>
    {error && <p role="alert" className={ui.error} style={{ marginBottom: 16 }}>{error}</p>}
    {notice && <p role="status" className={ui.notice} style={{ marginBottom: 16 }}>{notice}</p>}
    <section className={ui.card}>
      {busy ? <div className={s.empty}>Đang tải loại văn bản…</div> : rows.length === 0 ? <div className={s.empty}><Files size={42} strokeWidth={1.3}/><strong>Chưa có loại văn bản</strong><p>Chạy script 020-document-categories.sql để tạo các loại có sẵn.</p></div>
      : <div className={s.tableWrap}><table className={s.table}><thead><tr><th>Loại văn bản</th><th>Ngày</th><th>Bắt buộc trước khi gửi khách</th><th>Mẫu</th><th>Trạng thái</th><th><span className="sr-only">Thao tác</span></th></tr></thead>
        <tbody>{rows.map(r => <tr key={r.id}><td><strong>{r.name}</strong><div className={s.muted} style={{ fontSize: 11, marginTop: 4 }}>{r.locked ? "Loại gốc của luồng ký" : r.builtin ? "Có sẵn" : "Tự thêm"}</div></td>
          <td>{r.rule.effectiveLabel}{r.rule.expireShown && <div className={s.muted} style={{ fontSize: 11, marginTop: 4 }}>{r.rule.expireLabel}</div>}</td>
          <td className={s.muted}>{needs(r)}</td>
          <td>{r.templateCount ? <span className={s.badge}>{r.templateCount} mẫu</span> : <span className={s.muted}>Chưa dùng</span>}</td>
          <td><span className={`${s.badge} ${r.active ? "" : s.warning}`}>{r.active ? "Đang dùng" : "Đã ẩn"}</span></td>
          <td><div style={{ display: "flex", gap: 8, justifyContent: "flex-end" }}>
            {!r.locked && <button className={ui.secondaryButton} disabled={acting} onClick={() => setEditing(r)}>Sửa</button>}
            {!r.locked && r.templateCount === 0 && <button className={ui.secondaryButton} disabled={acting} onClick={() => remove(r)}>Xóa</button>}
          </div></td></tr>)}</tbody></table></div>}
    </section>
    {editing && <DocumentCategoryDialog category={editing === "new" ? null : editing} onClose={() => setEditing(null)}
      onSaved={row => { setEditing(null); setNotice(editing === "new" ? `Đã thêm ${row.name}.` : `Đã cập nhật ${row.name}.`); refresh(); }}/>}
  </div>;
}

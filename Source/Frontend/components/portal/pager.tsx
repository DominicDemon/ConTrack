"use client";
import { useState } from "react";
import { ArrowDown, ArrowUp, ChevronLeft, ChevronRight } from "lucide-react";
import { internalStyles as ui } from "@/components/internal/internal-theme";
import s from "@/components/internal/workspace.module.css";

export const PAGE_SIZE = 10;

/**
 * One page of a list shown on the portal. Back to page 1 when `resetOn` changes (a new search or filter), and kept
 * inside the list when items disappear (a document moved to the bin).
 */
export function usePaged<T>(items: T[], resetOn: unknown, size = PAGE_SIZE) {
  // The page belongs to the search it was chosen for: another search starts again at page 1.
  const [chosen, setChosen] = useState<{ page: number; on: unknown }>({ page: 1, on: resetOn });
  const page = Object.is(chosen.on, resetOn) ? chosen.page : 1;
  const setPage = (next: number) => setChosen({ page: next, on: resetOn });
  const pages = Math.max(1, Math.ceil(items.length / size));
  const current = Math.min(page, pages);
  return { page: current, pages, setPage, rows: items.slice((current - 1) * size, current * size), total: items.length, size };
}

export type DateOrder = "newest" | "oldest";

/** Items sorted by a date (ISO text), newest or oldest first; items without a date go last. */
export function byDate<T>(items: T[], date: (item: T) => string | null | undefined, order: DateOrder): T[] {
  const time = (item: T) => { const value = date(item); return value ? new Date(value).getTime() : NaN; };
  return [...items].sort((a, b) => {
    const x = time(a), y = time(b);
    if (Number.isNaN(x) || Number.isNaN(y)) return Number.isNaN(x) ? (Number.isNaN(y) ? 0 : 1) : -1;
    return order === "newest" ? y - x : x - y;
  });
}

/** A date column heading that switches newest first / oldest first; the arrow shows the current order. */
export function DateSortHeader({ label, order, onChange }: { label: string; order: DateOrder; onChange: (order: DateOrder) => void }) {
  const newest = order === "newest";
  return <th aria-sort={newest ? "descending" : "ascending"}>
    <button type="button" onClick={() => onChange(newest ? "oldest" : "newest")} title={newest ? "Đang xếp mới nhất trước. Bấm để xếp cũ nhất trước" : "Đang xếp cũ nhất trước. Bấm để xếp mới nhất trước"}
      style={{ display: "inline-flex", alignItems: "center", gap: 4, border: 0, background: "none", padding: 0, font: "inherit", color: "inherit", cursor: "pointer", whiteSpace: "nowrap" }}>
      {label}{newest ? <ArrowDown size={14} aria-hidden/> : <ArrowUp size={14} aria-hidden/>}
      <span className="sr-only">{newest ? "(mới nhất trước)" : "(cũ nhất trước)"}</span>
    </button>
  </th>;
}

/** "11–20 / 34" with previous and next; hidden when everything fits on one page. */
const pageNumbers = (page: number, pages: number): (number | null)[] => {
  const shown = new Set([1, pages, page - 2, page - 1, page, page + 1, page + 2].filter(value => value >= 1 && value <= pages));
  const sorted = [...shown].sort((a, b) => a - b);
  return sorted.flatMap((value, index) => index > 0 && value - sorted[index - 1] > 1 ? [null, value] : [value]);
};

export default function Pager({ page, pages, total, size, setPage, label }: {
  page: number; pages: number; total: number; size: number; setPage: (page: number) => void; label: string;
}) {
  if (pages <= 1) return null;
  const first = (page - 1) * size + 1, last = Math.min(page * size, total);
  return <nav aria-label={`Phân trang ${label}`} style={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: 12, padding: "12px 4px 2px", flexWrap: "wrap", borderTop: "1px solid var(--border)", marginTop: 12 }}>
    <span className={s.muted} style={{ fontSize: 13 }} aria-live="polite">{first}–{last} / {total} {label}</span>
    <div style={{ display: "flex", alignItems: "center", gap: 4, flexWrap: "wrap" }}>
      <button type="button" className={ui.secondaryButton} disabled={page <= 1} onClick={() => setPage(page - 1)}><ChevronLeft size={15}/>Trước</button>
      {pageNumbers(page, pages).map((value, index) => value === null
        ? <span key={`gap-${index}`} aria-hidden="true" className={s.muted} style={{ padding: "0 4px" }}>…</span>
        : <button key={value} type="button" className={value === page ? ui.primaryButton : ui.secondaryButton} aria-current={value === page ? "page" : undefined} aria-label={`Trang ${value}`} onClick={() => setPage(value)} style={{ minWidth: 36, justifyContent: "center", paddingInline: 8 }}>{value}</button>)}
      <button type="button" className={ui.secondaryButton} disabled={page >= pages} onClick={() => setPage(page + 1)}>Sau<ChevronRight size={15}/></button>
    </div>
  </nav>;
}

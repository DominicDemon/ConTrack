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
export default function Pager({ page, pages, total, size, setPage, label }: {
  page: number; pages: number; total: number; size: number; setPage: (page: number) => void; label: string;
}) {
  if (pages <= 1) return null;
  const first = (page - 1) * size + 1, last = Math.min(page * size, total);
  return <nav aria-label={`Phân trang ${label}`} style={{ display: "flex", alignItems: "center", justifyContent: "flex-end", gap: 8, padding: "12px 4px 2px", flexWrap: "wrap" }}>
    <span className={s.muted} style={{ fontSize: 13 }}>{first}–{last} / {total} {label}</span>
    <button type="button" className={ui.secondaryButton} disabled={page <= 1} onClick={() => setPage(page - 1)} aria-label="Trang trước"><ChevronLeft size={16}/></button>
    <span style={{ fontSize: 13, minWidth: 64, textAlign: "center" }} aria-current="page">Trang {page}/{pages}</span>
    <button type="button" className={ui.secondaryButton} disabled={page >= pages} onClick={() => setPage(page + 1)} aria-label="Trang sau"><ChevronRight size={16}/></button>
  </nav>;
}

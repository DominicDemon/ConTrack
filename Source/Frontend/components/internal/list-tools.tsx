"use client";
/* Same as SystemAdmin/components/internal/list-tools.tsx (the two apps do not share code): change both together. */
import { useState, type ReactNode } from "react";
import { ArrowDown, ArrowUp, ArrowUpDown } from "lucide-react";

/** Lower case without Vietnamese accents, so "hop dong" finds "Hợp đồng". */
export function searchText(value: string | null | undefined) {
  return (value ?? "").toLowerCase().normalize("NFD").replace(/\p{M}/gu, "").replaceAll("đ", "d").trim();
}

export type SortDir = "asc" | "desc";
export type SortState<K extends string> = { key: K; dir: SortDir };
type SortValue = string | number | boolean | null | undefined;

export const PAGE_SIZE = 10;

/**
 * The shared behaviour of every list screen: free-text search over chosen fields, the page's own filters,
 * sorting by a column and 10 rows per page. A new search, filter or sort goes back to page 1.
 */
export function useListView<T, K extends string>(items: T[], options: {
  search: string;
  /** Text of a row the search looks in. */
  fields: (item: T) => (string | null | undefined)[];
  /** The page's filters, already applied to one row. */
  filter?: (item: T) => boolean;
  /** Value of each sortable column. */
  sorts: Record<K, (item: T) => SortValue>;
  initial: SortState<NoInfer<K>>;
  /** Changes whenever a filter changes, so the list goes back to page 1. */
  filterKey?: unknown;
  pageSize?: number;
}) {
  const { search, fields, filter, sorts, initial, filterKey, pageSize = PAGE_SIZE } = options;
  const [sort, setSortState] = useState<SortState<K>>(initial);
  const resetOn = JSON.stringify([search, filterKey ?? null, sort]);
  const [chosen, setChosen] = useState<{ page: number; on: string }>({ page: 1, on: resetOn });
  const term = searchText(search);
  const matched = items.filter(item => (!filter || filter(item)) && (!term || fields(item).some(v => searchText(v).includes(term))));
  const value = sorts[sort.key];
  const sorted = [...matched].sort((a, b) => compare(value(a), value(b)) * (sort.dir === "asc" ? 1 : -1));
  const pages = Math.max(1, Math.ceil(sorted.length / pageSize));
  const page = Math.min(chosen.on === resetOn ? chosen.page : 1, pages);
  return {
    rows: sorted.slice((page - 1) * pageSize, page * pageSize), total: sorted.length, page, pageSize, sort,
    setPage: (next: number) => setChosen({ page: next, on: resetOn }),
    /** Same column: flip the direction; another column: start with its natural order (text A→Z, dates newest first). */
    sortBy: (key: K, firstDir: SortDir = "asc") => setSortState(old => old.key === key ? { key, dir: old.dir === "asc" ? "desc" : "asc" } : { key, dir: firstDir }),
  };
}

/** Empty values last; numbers and dates by value; text by Vietnamese alphabetical order. */
function compare(a: SortValue, b: SortValue) {
  const empty = (v: SortValue) => v === null || v === undefined || v === "";
  if (empty(a) || empty(b)) return empty(a) === empty(b) ? 0 : empty(a) ? 1 : -1;
  if (typeof a === "number" && typeof b === "number") return a - b;
  if (typeof a === "boolean" && typeof b === "boolean") return Number(a) - Number(b);
  return String(a).localeCompare(String(b), "vi", { numeric: true, sensitivity: "base" });
}

/** A table heading that sorts its column; the arrow shows the current order. */
export function SortHeader<K extends string>({ label, column, sort, onSort, firstDir = "asc", align, className }: {
  label: ReactNode; column: K; sort: SortState<K>; onSort: (key: K, firstDir?: SortDir) => void; firstDir?: SortDir; align?: "right" | "center";
  /** Classes of the table the heading sits in (the scan screens use the shadcn table). */
  className?: string;
}) {
  const active = sort.key === column;
  const Icon = !active ? ArrowUpDown : sort.dir === "asc" ? ArrowUp : ArrowDown;
  return <th aria-sort={active ? (sort.dir === "asc" ? "ascending" : "descending") : "none"} className={className} style={align ? { textAlign: align } : undefined}>
    <button type="button" onClick={() => onSort(column, firstDir)} title="Bấm để sắp xếp"
      style={{ display: "inline-flex", alignItems: "center", gap: 4, border: 0, background: "none", padding: 0, font: "inherit", color: "inherit", cursor: "pointer", whiteSpace: "nowrap" }}>
      {label}<Icon size={13} aria-hidden style={{ opacity: active ? 1 : 0.4 }}/>
    </button>
  </th>;
}

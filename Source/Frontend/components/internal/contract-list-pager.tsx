"use client";

import { ChevronLeft, ChevronRight } from "lucide-react";
import { Button } from "@/components/ui/button";

const pageNumbers = (page: number, pages: number): (number | null)[] => {
  const shown = new Set([1, pages, page - 2, page - 1, page, page + 1, page + 2].filter((value) => value >= 1 && value <= pages));
  const sorted = [...shown].sort((a, b) => a - b);
  return sorted.flatMap((value, index) => index > 0 && value - sorted[index - 1] > 1 ? [null, value] : [value]);
};

export function ListPager({ page, pageSize, total, onPage, label = "hợp đồng" }: {
  page: number; pageSize: number; total: number; onPage: (page: number) => void; label?: string;
}) {
  const pages = Math.max(1, Math.ceil(total / pageSize));
  if (total <= pageSize) return null;
  const first = (page - 1) * pageSize + 1;
  const last = Math.min(page * pageSize, total);
  return <nav aria-label={`Phân trang ${label}`} className="mt-4 flex flex-wrap items-center justify-between gap-3 border-t pt-4">
    <p className="text-sm text-muted-foreground" aria-live="polite">{first}–{last} / {total} {label}</p>
    <div className="flex flex-wrap items-center gap-1">
      <Button variant="ghost" size="sm" disabled={page <= 1} onClick={() => onPage(page - 1)}><ChevronLeft aria-hidden="true"/>Trước</Button>
      {pageNumbers(page, pages).map((value, index) => value === null
        ? <span key={`gap-${index}`} aria-hidden="true" className="px-1 text-muted-foreground">…</span>
        : <Button key={value} variant={value === page ? "outline" : "ghost"} size="sm" className="min-w-9" aria-current={value === page ? "page" : undefined} aria-label={`Trang ${value}`} onClick={() => onPage(value)}>{value}</Button>)}
      <Button variant="ghost" size="sm" disabled={page >= pages} onClick={() => onPage(page + 1)}>Sau<ChevronRight aria-hidden="true"/></Button>
    </div>
  </nav>;
}

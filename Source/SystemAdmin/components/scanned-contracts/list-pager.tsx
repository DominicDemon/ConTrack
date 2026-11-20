"use client";

import { ChevronLeft, ChevronRight } from "lucide-react";
import { Button } from "@/components/ui/button";

/** Page numbers to show: the first, the last, and two on each side of the current one; null marks a gap. */
const pageNumbers = (page: number, pages: number): (number | null)[] => {
  const shown = new Set([1, pages, page - 2, page - 1, page, page + 1, page + 2].filter((n) => n >= 1 && n <= pages));
  const sorted = [...shown].sort((a, b) => a - b);
  return sorted.flatMap((n, index) => (index > 0 && n - sorted[index - 1] > 1 ? [null, n] : [n]));
};

/** Previous / page numbers / next under a list, with "showing x–y of total". Hidden when everything fits one page. */
export function ListPager({
  page,
  pageSize,
  total,
  onPage,
  disabled,
  label,
}: {
  page: number;
  pageSize: number;
  total: number;
  onPage: (page: number) => void;
  disabled?: boolean;
  /** What is counted, e.g. "lượt scan". */
  label: string;
}) {
  const pages = Math.max(1, Math.ceil(total / pageSize));
  if (total <= pageSize) return null;
  const first = (page - 1) * pageSize + 1;
  const last = Math.min(page * pageSize, total);

  return (
    <nav aria-label={`Phân trang ${label}`} className="mt-4 flex flex-wrap items-center justify-between gap-3 border-t pt-4">
      <p className="text-sm text-muted-foreground">
        {first}–{last} / {total} {label}
      </p>
      <div className="flex flex-wrap items-center gap-1">
        <Button variant="ghost" size="sm" disabled={disabled || page <= 1} onClick={() => onPage(page - 1)}>
          <ChevronLeft aria-hidden="true" />
          Trước
        </Button>
        {pageNumbers(page, pages).map((n, index) =>
          n === null ? (
            <span key={`gap-${index}`} aria-hidden="true" className="px-1 text-muted-foreground">
              …
            </span>
          ) : (
            <Button
              key={n}
              variant={n === page ? "outline" : "ghost"}
              size="sm"
              className="min-w-9"
              aria-current={n === page ? "page" : undefined}
              aria-label={`Trang ${n}`}
              disabled={disabled}
              onClick={() => n !== page && onPage(n)}
            >
              {n}
            </Button>
          ),
        )}
        <Button variant="ghost" size="sm" disabled={disabled || page >= pages} onClick={() => onPage(page + 1)}>
          Sau
          <ChevronRight aria-hidden="true" />
        </Button>
      </div>
    </nav>
  );
}

export function ListPager({ page, pageSize, total, onPage }: {
  page: number; pageSize: number; total: number; onPage: (page: number) => void;
}) {
  const pages = Math.ceil(total / pageSize);
  if (pages <= 1) return null;
  return <nav aria-label="Phân trang hợp đồng" className="flex flex-wrap items-center gap-3 p-4">
    <button type="button" className="rounded-md border px-3 py-2 disabled:opacity-50" disabled={page <= 1} onClick={() => onPage(page - 1)}>Trang trước</button>
    <span aria-live="polite">Trang {page}/{pages} · {total} hợp đồng</span>
    <button type="button" className="rounded-md border px-3 py-2 disabled:opacity-50" disabled={page >= pages} onClick={() => onPage(page + 1)}>Trang sau</button>
  </nav>;
}

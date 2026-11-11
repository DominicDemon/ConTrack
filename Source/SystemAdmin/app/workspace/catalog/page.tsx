"use client";
import { useEffect, useState } from "react";
import { Package, PackagePlus, RefreshCw, Server } from "lucide-react";
import { SummaryCards } from "@/components/ui/custom/summary-cards";
import { useInternalUser } from "@/components/internal/internal-shell";
import { internalStyles as ui } from "@/components/internal/internal-theme";
import { ProductFormDialog } from "@/components/internal/catalog-dialogs";
import s from "@/components/internal/workspace.module.css";
import { internalApi, money, type CategoryOption, type ProductRow, type ServiceRow } from "@/services/internal-api";

export default function CatalogPage() {
  const user = useInternalUser(); const canManage = user.permissions.includes("product.manage");
  const [products, setProducts] = useState<ProductRow[]>([]); const [services, setServices] = useState<ServiceRow[]>([]); const [categories, setCategories] = useState<CategoryOption[]>([]);
  const [tab, setTab] = useState<"products" | "services">("products"); const [search, setSearch] = useState(""); const [category, setCategory] = useState("");
  const [error, setError] = useState(""); const [notice, setNotice] = useState(""); const [busy, setBusy] = useState(true); const [reload, setReload] = useState(0);
  const [form, setForm] = useState<{ product: ProductRow | null } | null>(null);
  useEffect(() => { let active = true;
    void Promise.all([internalApi<ProductRow[]>("/catalog/products"), internalApi<ServiceRow[]>("/catalog/services"), internalApi<CategoryOption[]>("/catalog/categories")])
      .then(([p, sv, c]) => { if (active) { setProducts(p); setServices(sv); setCategories(c); } })
      .catch(e => { if (active) setError(e.message); }).finally(() => { if (active) setBusy(false); });
    return () => { active = false; }; }, [reload]);
  const term = search.trim().toLowerCase();
  const shownProducts = products.filter(p => (!category || String(p.categoryId ?? "") === category) && (!term || [p.name, p.code ?? ""].some(v => v.toLowerCase().includes(term))));
  const shownServices = services.filter(sv => !term || sv.name.toLowerCase().includes(term));
  const price = (value: number | null) => value === null ? <span className={s.muted}>Chưa có giá</span> : `${money(value)} ₫`;
  const replace = (row: ProductRow) => setProducts(list => list.some(p => p.id === row.id) ? list.map(p => p.id === row.id ? row : p) : [...list, row]);

  return <div className={s.page}><div className={s.titleRow}><div><span className={s.eyebrow}>Danh mục</span><h1>Sản phẩm và dịch vụ</h1><p className={s.muted}>{canManage ? "Thêm và sửa sản phẩm dùng khi soạn hợp đồng." : "Danh mục sản phẩm và dịch vụ của công ty (chỉ xem)."}</p></div>
    {canManage && tab === "products" && <button className={ui.primaryButton} disabled={busy} onClick={() => setForm({ product: null })}><PackagePlus size={18}/>Thêm sản phẩm</button>}</div>
    <div style={{ marginBottom: 24 }}><SummaryCards isLoading={busy} items={[
      { title: "Sản phẩm", value: products.length, icon: <Package size={24}/>, iconWrapperClassName: "bg-blue-100 text-blue-700 dark:bg-blue-500/15 dark:text-blue-400" },
      { title: "Dịch vụ", value: services.length, icon: <Server size={24}/>, iconWrapperClassName: "bg-emerald-100 text-emerald-700 dark:bg-emerald-500/15 dark:text-emerald-400" },
    ]}/></div>
    {error && <p role="alert" className={ui.error} style={{ marginBottom: 16 }}>{error}</p>}
    {notice && <p role="status" className={ui.notice} style={{ marginBottom: 16 }}>{notice}</p>}
    <section className={ui.card}><div className={s.toolbar}>
      <div role="tablist" aria-label="Loại danh mục" style={{ display: "flex", gap: 8 }}>
        <button role="tab" aria-selected={tab === "products"} className={tab === "products" ? ui.primaryButton : ui.secondaryButton} onClick={() => setTab("products")}>Sản phẩm</button>
        <button role="tab" aria-selected={tab === "services"} className={tab === "services" ? ui.primaryButton : ui.secondaryButton} onClick={() => setTab("services")}>Dịch vụ</button></div>
      <input aria-label="Tìm trong danh mục" className={ui.field} placeholder={tab === "products" ? "Tìm tên hoặc mã sản phẩm…" : "Tìm tên dịch vụ…"} value={search} onChange={e => setSearch(e.target.value)}/>
      {tab === "products" && <select aria-label="Lọc danh mục" className={ui.field} style={{ maxWidth: 220 }} value={category} onChange={e => setCategory(e.target.value)}><option value="">Tất cả danh mục</option>{categories.map(c => <option key={c.id} value={c.id}>{c.name}</option>)}</select>}
      <button className={ui.secondaryButton} disabled={busy} onClick={() => { setBusy(true); setError(""); setReload(reload + 1); }} aria-label="Làm mới"><RefreshCw size={16}/></button></div>
      {busy ? <div className={s.empty}>Đang tải danh mục…</div>
      : tab === "products" ? (shownProducts.length === 0 ? <div className={s.empty}><Package size={42} strokeWidth={1.3}/><strong>{search || category ? "Không tìm thấy sản phẩm phù hợp" : "Chưa có sản phẩm"}</strong><p>{search || category ? "Thử đổi từ khóa hoặc bộ lọc." : canManage ? "Thêm sản phẩm đầu tiên." : "Quản lý kinh doanh sẽ thêm sản phẩm."}</p></div>
        : <div className={s.tableWrap}><table className={s.table}><thead><tr><th>Sản phẩm</th><th>Danh mục</th><th>Đơn vị</th><th>Giá đề xuất</th>{canManage && <th><span className="sr-only">Thao tác</span></th>}</tr></thead>
          <tbody>{shownProducts.map(p => <tr key={p.id}><td><strong>{p.name}</strong><div className={s.muted} style={{ fontSize: 11, marginTop: 4 }}>{p.code ?? "Chưa có mã"}{p.description ? ` · ${p.description}` : ""}</div></td>
            <td>{p.categoryName ?? <span className={s.muted}>Không phân loại</span>}</td><td>{p.unit ?? <span className={s.muted}>—</span>}</td><td>{price(p.price)}</td>
            {canManage && <td><div style={{ display: "flex", justifyContent: "flex-end" }}><button className={ui.secondaryButton} onClick={() => setForm({ product: p })}>Sửa</button></div></td>}</tr>)}</tbody></table></div>)
      : (shownServices.length === 0 ? <div className={s.empty}><Server size={42} strokeWidth={1.3}/><strong>{search ? "Không tìm thấy dịch vụ phù hợp" : "Chưa có dịch vụ"}</strong></div>
        : <div className={s.tableWrap}><table className={s.table}><thead><tr><th>Dịch vụ</th><th>Loại</th><th>Đơn vị</th><th>Giá</th><th>Trạng thái</th></tr></thead>
          <tbody>{shownServices.map(sv => <tr key={sv.id}><td><strong>{sv.name}</strong></td><td>{sv.typeName ?? <span className={s.muted}>—</span>}</td><td>{sv.unit ?? <span className={s.muted}>—</span>}</td><td>{price(sv.price)}</td>
            <td><span className={`${s.badge} ${sv.active ? "" : s.warning}`}>{sv.active ? "Đang cung cấp" : "Ngừng cung cấp"}</span></td></tr>)}</tbody></table></div>)}
    </section>
    {form && <ProductFormDialog product={form.product} categories={categories} onClose={() => setForm(null)}
      onSaved={row => { const created = form.product === null; setForm(null); replace(row); setNotice(created ? `Đã thêm ${row.name}.` : `Đã cập nhật ${row.name}.`); }}/>}
  </div>;
}

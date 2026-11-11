"use client";
import { useEffect, useState, type ReactNode } from "react";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription, DialogFooter } from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { internalApi, type BusinessOption, type CategoryOption, type CustomerRow, type DepartmentRow, type ProductRow } from "@/services/internal-api";
import { internalStyles as ui } from "./internal-theme";

const field = "grid gap-2 text-sm font-medium";

/** Shared frame of the catalog dialogs: busy blocks closing, the server's message is shown under the form. */
function FormDialog({ title, description, busy, error, ready, submit, onClose, onSubmit, children }: { title: string; description: string; busy: boolean; error: string; ready: boolean;
  submit: string; onClose: () => void; onSubmit: () => void; children: ReactNode }) {
  return <Dialog open onOpenChange={value => { if (!busy && !value) onClose(); }}><DialogContent className="sm:max-w-xl max-h-[90vh] overflow-y-auto" onInteractOutside={e => e.preventDefault()}>
    <DialogHeader><DialogTitle>{title}</DialogTitle><DialogDescription>{description}</DialogDescription></DialogHeader>
    {children}
    {error && <p role="alert" className={ui.error}>{error}</p>}
    <DialogFooter><Button type="button" variant="outline" disabled={busy} onClick={onClose}>Hủy</Button><Button type="button" disabled={busy || !ready} onClick={onSubmit}>{busy ? "Đang lưu…" : submit}</Button></DialogFooter>
  </DialogContent></Dialog>;
}

function useSave<T>(onSaved: (result: T) => void) {
  const [busy, setBusy] = useState(false); const [error, setError] = useState("");
  async function save(run: () => Promise<T>) { setBusy(true); setError(""); try { onSaved(await run()); } catch (e) { setError((e as Error).message); } finally { setBusy(false); } }
  return { busy, error, save };
}

/** Creates a customer (customer = null) or edits one; a new customer may come with a business (company + tax code). */
export function CustomerFormDialog({ customer, onClose, onSaved }: { customer: CustomerRow | null; onClose: () => void; onSaved: (row: CustomerRow) => void }) {
  const [name, setName] = useState(customer?.name ?? ""); const [phone, setPhone] = useState(customer?.phone ?? ""); const [email, setEmail] = useState(customer?.email ?? "");
  const [address, setAddress] = useState(customer?.address ?? ""); const [tax, setTax] = useState(customer?.taxIdentifier ?? ""); const [company, setCompany] = useState(""); const [companyTax, setCompanyTax] = useState("");
  const [businesses, setBusinesses] = useState<BusinessOption[] | null>(null);
  const { busy, error, save } = useSave(onSaved);
  const creating = customer === null;
  useEffect(() => { if (customer) void internalApi<BusinessOption[]>(`/crm/customers/${customer.id}/businesses`).then(setBusinesses).catch(() => setBusinesses([])); }, [customer]);
  const ready = !!name.trim() && !!phone.trim() && (!company.trim() || !!companyTax.trim());
  return <FormDialog title={creating ? "Thêm khách hàng" : `Sửa ${customer.name}`} busy={busy} error={error} ready={ready} submit={creating ? "Thêm khách hàng" : "Lưu thay đổi"} onClose={onClose}
    description={creating ? "Khách dùng số điện thoại này để đăng nhập cổng khách hàng." : "Đổi số điện thoại hoặc email thì khách phải xác minh lại ở lần đăng nhập kế tiếp."}
    onSubmit={() => void save(() => creating
      ? internalApi<CustomerRow>("/crm/customers", "POST", { name, phone, email, address, company, taxCode: companyTax })
      : internalApi<CustomerRow>(`/crm/customers/${customer.id}`, "PUT", { name, phone, email, address, taxIdentifier: tax }))}>
    <div className="grid gap-4 sm:grid-cols-2">
      <label className={`${field} sm:col-span-2`}>Tên khách hàng *<Input required maxLength={200} value={name} disabled={busy} onChange={e => setName(e.target.value)}/></label>
      <label className={field}>Số điện thoại *<Input required type="tel" maxLength={20} value={phone} disabled={busy} placeholder="VD: 0912345678" onChange={e => setPhone(e.target.value)}/></label>
      <label className={field}>Email<Input type="email" maxLength={255} value={email} disabled={busy} onChange={e => setEmail(e.target.value)}/></label>
      <label className={`${field} sm:col-span-2`}>Địa chỉ<Input maxLength={1000} value={address} disabled={busy} onChange={e => setAddress(e.target.value)}/></label>
      {!creating && <label className={field}>Mã số thuế cá nhân<Input maxLength={20} value={tax} disabled={busy} onChange={e => setTax(e.target.value)}/></label>}
      {creating && <><label className={field}>Tên doanh nghiệp<Input maxLength={1000} value={company} disabled={busy} placeholder="Bỏ trống nếu là cá nhân" onChange={e => setCompany(e.target.value)}/></label>
        <label className={field}>Mã số thuế doanh nghiệp{company.trim() ? " *" : ""}<Input maxLength={20} value={companyTax} disabled={busy} onChange={e => setCompanyTax(e.target.value)}/></label></>}
    </div>
    {!creating && <div className="grid gap-2 text-sm"><strong>Pháp nhân</strong>{businesses === null ? <span className="text-muted-foreground">Đang tải…</span>
      : businesses.length === 0 ? <span className="text-muted-foreground">Chưa có pháp nhân. Có thể thêm khi soạn hợp đồng cho khách này.</span>
      : <ul className="grid gap-1">{businesses.map(b => <li key={b.id}>{b.name} <span className="text-muted-foreground">· MST {b.taxCode}</span></li>)}</ul>}</div>}
  </FormDialog>;
}

export function ProductFormDialog({ product, categories, onClose, onSaved }: { product: ProductRow | null; categories: CategoryOption[]; onClose: () => void; onSaved: (row: ProductRow) => void }) {
  const [code, setCode] = useState(product?.code ?? ""); const [name, setName] = useState(product?.name ?? ""); const [unit, setUnit] = useState(product?.unit ?? "");
  const [categoryId, setCategoryId] = useState(product?.categoryId?.toString() ?? ""); const [price, setPrice] = useState(product?.price?.toString() ?? ""); const [description, setDescription] = useState(product?.description ?? "");
  const { busy, error, save } = useSave(onSaved);
  const creating = product === null;
  const body = () => ({ code, name, unit, categoryId: categoryId ? Number(categoryId) : null, price: price === "" ? null : Number(price), description });
  return <FormDialog title={creating ? "Thêm sản phẩm" : `Sửa ${product.name}`} description="Giá đề xuất được gợi ý khi nhân viên thêm sản phẩm vào hợp đồng nháp." busy={busy} error={error}
    ready={!!name.trim() && (price === "" || Number(price) >= 0)} submit={creating ? "Thêm sản phẩm" : "Lưu thay đổi"} onClose={onClose}
    onSubmit={() => void save(() => creating ? internalApi<ProductRow>("/catalog/products", "POST", body()) : internalApi<ProductRow>(`/catalog/products/${product.id}`, "PUT", body()))}>
    <div className="grid gap-4 sm:grid-cols-2">
      <label className={field}>Mã sản phẩm<Input maxLength={20} value={code} disabled={busy} onChange={e => setCode(e.target.value.toUpperCase())}/></label>
      <label className={field}>Danh mục<select className={ui.field} value={categoryId} disabled={busy} onChange={e => setCategoryId(e.target.value)}><option value="">Không phân loại</option>{categories.map(c => <option key={c.id} value={c.id}>{c.name}</option>)}</select></label>
      <label className={`${field} sm:col-span-2`}>Tên sản phẩm *<Input required maxLength={500} value={name} disabled={busy} onChange={e => setName(e.target.value)}/></label>
      <label className={field}>Đơn vị tính<Input maxLength={100} value={unit} disabled={busy} placeholder="VD: Gói, Tháng" onChange={e => setUnit(e.target.value)}/></label>
      <label className={field}>Giá đề xuất (VND)<Input type="number" min={0} step="any" value={price} disabled={busy} onChange={e => setPrice(e.target.value)}/></label>
      <label className={`${field} sm:col-span-2`}>Mô tả ngắn<textarea className={ui.field} rows={3} maxLength={2000} value={description} disabled={busy} onChange={e => setDescription(e.target.value)}/></label>
    </div>
  </FormDialog>;
}

export function DepartmentFormDialog({ department, onClose, onSaved }: { department: DepartmentRow; onClose: () => void; onSaved: (row: DepartmentRow) => void }) {
  const [name, setName] = useState(department.name); const [description, setDescription] = useState(department.description ?? "");
  const { busy, error, save } = useSave(onSaved);
  return <FormDialog title={`Sửa ${department.name}`} description={`Mã ${department.code} và màn hình của phòng được cố định; quản lý và nhân viên đổi ở trang Nhân viên & phân quyền.`}
    busy={busy} error={error} ready={!!name.trim()} submit="Lưu thay đổi" onClose={onClose}
    onSubmit={() => void save(() => internalApi<DepartmentRow>(`/org/departments/${department.id}`, "PUT", { name, description }))}>
    <label className={field}>Tên phòng ban *<Input required maxLength={500} value={name} disabled={busy} onChange={e => setName(e.target.value)}/></label>
    <label className={field}>Mô tả<textarea className={ui.field} rows={3} maxLength={2000} value={description} disabled={busy} onChange={e => setDescription(e.target.value)}/></label>
  </FormDialog>;
}

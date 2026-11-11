"use client";
import { useState } from "react";
import { Copy } from "lucide-react";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription, DialogFooter } from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { internalApi, type EmployeeAdminRow, type DepartmentOption, type TemporaryPasswordResult } from "@/services/internal-api";
import { internalStyles as ui } from "./internal-theme";
import { screensFor } from "./internal-nav";

/** Creates an account (employee = null) or moves an existing one; department + position decide the employee's screens. */
export function EmployeeFormDialog({ employee, departments, onClose, onSaved }: { employee: EmployeeAdminRow | null; departments: DepartmentOption[]; onClose: () => void;
  onSaved: (result: EmployeeAdminRow | TemporaryPasswordResult) => void }) {
  const [code, setCode] = useState(""); const [username, setUsername] = useState(""); const [name, setName] = useState(""); const [email, setEmail] = useState(""); const [phone, setPhone] = useState("");
  const [departmentId, setDepartmentId] = useState(employee?.departmentId ?? ""); const [manager, setManager] = useState(employee?.isManager ?? false);
  const [busy, setBusy] = useState(false); const [error, setError] = useState("");
  const creating = employee === null;
  const department = departments.find(d => d.id === departmentId);
  const staffOnly = department?.staffPermissions === null; // Ban Giám Đốc: manager only
  const isManager = manager || staffOnly;
  const seatTaken = !!department && isManager && department.managerId !== null && department.managerId !== employee?.id;
  const screens = department ? screensFor((isManager ? department.managerPermissions : department.staffPermissions) ?? []).filter(item => item.href !== "/workspace") : [];
  const ready = !!department && !seatTaken && (!creating || (code.trim() && username.trim() && name.trim()));
  async function save() { setBusy(true); setError("");
    try { onSaved(creating
      ? await internalApi<TemporaryPasswordResult>("/admin/employees", "POST", { code, username, name, email, phone, departmentId, manager: isManager })
      : await internalApi<EmployeeAdminRow>(`/admin/employees/${employee.id}/position`, "PUT", { departmentId, manager: isManager })); }
    catch (e) { setError((e as Error).message); } finally { setBusy(false); } }
  return <Dialog open onOpenChange={value => { if (!busy && !value) onClose(); }}><DialogContent className="sm:max-w-xl max-h-[90vh] overflow-y-auto" onInteractOutside={e => e.preventDefault()}>
    <DialogHeader><DialogTitle>{creating ? "Tạo tài khoản nhân viên" : `Đổi phòng ban của ${employee.name}`}</DialogTitle>
      <DialogDescription>{creating ? "Hệ thống tạo mật khẩu tạm và hiện một lần sau khi lưu. Nhân viên phải đổi mật khẩu ở lần đăng nhập đầu tiên." : "Phòng ban và chức vụ mới có hiệu lực ngay ở thao tác kế tiếp của nhân viên."}</DialogDescription></DialogHeader>
    {creating && <div className="grid gap-4 sm:grid-cols-2">
      <label className="grid gap-2 text-sm font-medium">Mã nhân viên *<Input required maxLength={30} value={code} disabled={busy} placeholder="VD: NV001" onChange={e => setCode(e.target.value.toUpperCase())}/></label>
      <label className="grid gap-2 text-sm font-medium">Tên đăng nhập *<Input required maxLength={100} autoComplete="off" value={username} disabled={busy} placeholder="VD: nguyen.van.a" onChange={e => setUsername(e.target.value.toLowerCase())}/></label>
      <label className="grid gap-2 text-sm font-medium sm:col-span-2">Họ tên *<Input required maxLength={200} value={name} disabled={busy} onChange={e => setName(e.target.value)}/></label>
      <label className="grid gap-2 text-sm font-medium">Email<Input type="email" maxLength={100} value={email} disabled={busy} onChange={e => setEmail(e.target.value)}/></label>
      <label className="grid gap-2 text-sm font-medium">Số điện thoại<Input type="tel" maxLength={20} value={phone} disabled={busy} onChange={e => setPhone(e.target.value)}/></label>
    </div>}
    <label className="grid gap-2 text-sm font-medium">Phòng ban *<select required className={ui.field} value={departmentId} disabled={busy} onChange={e => setDepartmentId(e.target.value)}>
      <option value="">Chọn phòng ban</option>{departments.map(d => <option key={d.id} value={d.id}>{d.name}</option>)}</select></label>
    {department && <fieldset className="grid gap-3"><legend className="mb-2 text-sm font-medium">Chức vụ *</legend>
      {!staffOnly && <label className="flex items-start gap-2 text-sm"><input type="radio" name="position" className="mt-1" checked={!manager} disabled={busy} onChange={() => setManager(false)}/>Nhân viên</label>}
      <label className="flex items-start gap-2 text-sm"><input type="radio" name="position" className="mt-1" checked={isManager} disabled={busy} onChange={() => setManager(true)}/>
        <span>Quản lý phòng<span className="block text-muted-foreground">{department.managerId === null ? "Phòng chưa có quản lý." : department.managerId === employee?.id ? "Đang là quản lý của phòng này." : `Hiện do ${department.managerName ?? "người khác"} quản lý.`}</span></span></label>
    </fieldset>}
    {seatTaken && <p role="alert" className={ui.error}>{department.name} đã có quản lý. Mỗi phòng chỉ có một quản lý: chuyển {department.managerName ?? "người đó"} sang chức vụ nhân viên trước.</p>}
    {department && !seatTaken && <p className="text-sm text-muted-foreground">Được dùng: {screens.length ? screens.map(item => item.label).join(", ") : "chỉ Tổng quan"}.</p>}
    {error && <p role="alert" className={ui.error}>{error}</p>}
    <DialogFooter><Button type="button" variant="outline" disabled={busy} onClick={onClose}>Hủy</Button><Button type="button" disabled={busy || !ready} onClick={() => void save()}>{busy ? "Đang lưu…" : creating ? "Tạo tài khoản" : "Lưu phòng ban"}</Button></DialogFooter>
  </DialogContent></Dialog>;
}

/** Shows an admin-issued temporary password once; it is never retrievable again. */
export function TemporaryPasswordDialog({ result, onClose }: { result: TemporaryPasswordResult; onClose: () => void }) {
  const [copied, setCopied] = useState(false);
  return <Dialog open onOpenChange={value => { if (!value) onClose(); }}><DialogContent className="sm:max-w-md" onInteractOutside={e => e.preventDefault()}>
    <DialogHeader><DialogTitle>Mật khẩu tạm của {result.employee.username}</DialogTitle>
      <DialogDescription>Chỉ hiện một lần. Gửi cho nhân viên qua kênh riêng; nhân viên phải đổi mật khẩu ngay ở lần đăng nhập đầu tiên.</DialogDescription></DialogHeader>
    <div className="flex items-center gap-2"><code className="flex-1 rounded-lg border px-3 py-2 text-base tracking-wider">{result.temporaryPassword}</code>
      <Button type="button" variant="outline" onClick={() => void navigator.clipboard.writeText(result.temporaryPassword).then(() => setCopied(true))}><Copy className="size-4"/>{copied ? "Đã chép" : "Chép"}</Button></div>
    <DialogFooter><Button type="button" onClick={onClose}>Tôi đã lưu mật khẩu</Button></DialogFooter>
  </DialogContent></Dialog>;
}

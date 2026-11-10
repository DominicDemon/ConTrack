"use client";
import { useState } from "react";
import { Copy } from "lucide-react";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription, DialogFooter } from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { internalApi, type EmployeeAdminRow, type RoleOption, type TemporaryPasswordResult } from "@/services/internal-api";
import { internalStyles as ui } from "./internal-theme";

const permissionLabel: Record<string, string> = { "draft.write": "soạn nháp của mình", "draft.manage.all": "quản lý nháp toàn công ty", "employee.manage": "quản lý tài khoản" };

/** Creates an account (employee = null) or changes the roles of an existing one. */
export function EmployeeFormDialog({ employee, roles, onClose, onSaved }: { employee: EmployeeAdminRow | null; roles: RoleOption[]; onClose: () => void;
  onSaved: (result: EmployeeAdminRow | TemporaryPasswordResult) => void }) {
  const [code, setCode] = useState(""); const [username, setUsername] = useState(""); const [name, setName] = useState(""); const [email, setEmail] = useState(""); const [phone, setPhone] = useState("");
  const [roleIds, setRoleIds] = useState<string[]>(employee?.roleIds ?? []); const [busy, setBusy] = useState(false); const [error, setError] = useState("");
  const creating = employee === null;
  const ready = roleIds.length > 0 && (!creating || (code.trim() && username.trim() && name.trim()));
  async function save() { setBusy(true); setError("");
    try { onSaved(creating
      ? await internalApi<TemporaryPasswordResult>("/admin/employees", "POST", { code, username, name, email, phone, roleIds })
      : await internalApi<EmployeeAdminRow>(`/admin/employees/${employee.id}/roles`, "PUT", { roleIds })); }
    catch (e) { setError((e as Error).message); } finally { setBusy(false); } }
  return <Dialog open onOpenChange={value => { if (!busy && !value) onClose(); }}><DialogContent className="sm:max-w-xl max-h-[90vh] overflow-y-auto" onInteractOutside={e => e.preventDefault()}>
    <DialogHeader><DialogTitle>{creating ? "Tạo tài khoản nhân viên" : `Phân quyền cho ${employee.name}`}</DialogTitle>
      <DialogDescription>{creating ? "Hệ thống tạo mật khẩu tạm và hiện một lần sau khi lưu. Nhân viên phải đổi mật khẩu ở lần đăng nhập đầu tiên." : "Vai trò mới có hiệu lực ngay ở thao tác kế tiếp của nhân viên."}</DialogDescription></DialogHeader>
    {creating && <div className="grid gap-4 sm:grid-cols-2">
      <label className="grid gap-2 text-sm font-medium">Mã nhân viên *<Input required maxLength={30} value={code} disabled={busy} placeholder="VD: NV001" onChange={e => setCode(e.target.value.toUpperCase())}/></label>
      <label className="grid gap-2 text-sm font-medium">Tên đăng nhập *<Input required maxLength={100} autoComplete="off" value={username} disabled={busy} placeholder="VD: nguyen.van.a" onChange={e => setUsername(e.target.value.toLowerCase())}/></label>
      <label className="grid gap-2 text-sm font-medium sm:col-span-2">Họ tên *<Input required maxLength={200} value={name} disabled={busy} onChange={e => setName(e.target.value)}/></label>
      <label className="grid gap-2 text-sm font-medium">Email<Input type="email" maxLength={100} value={email} disabled={busy} onChange={e => setEmail(e.target.value)}/></label>
      <label className="grid gap-2 text-sm font-medium">Số điện thoại<Input type="tel" maxLength={20} value={phone} disabled={busy} onChange={e => setPhone(e.target.value)}/></label>
    </div>}
    <fieldset className="grid gap-3"><legend className="mb-2 text-sm font-medium">Vai trò *</legend>
      {roles.map(role => <label key={role.id} className="flex items-start gap-2 text-sm"><input type="checkbox" className="mt-1" checked={roleIds.includes(role.id)} disabled={busy}
        onChange={e => setRoleIds(ids => e.target.checked ? [...ids, role.id] : ids.filter(id => id !== role.id))}/>
        <span><strong>{role.name}</strong><span className="block text-muted-foreground">{role.permissions.length ? "Được " + role.permissions.map(p => permissionLabel[p] ?? p).join(", ") : "Chưa gán quyền nào"}</span></span></label>)}
    </fieldset>
    {error && <p role="alert" className={ui.error}>{error}</p>}
    <DialogFooter><Button type="button" variant="outline" disabled={busy} onClick={onClose}>Hủy</Button><Button type="button" disabled={busy || !ready} onClick={() => void save()}>{busy ? "Đang lưu…" : creating ? "Tạo tài khoản" : "Lưu vai trò"}</Button></DialogFooter>
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

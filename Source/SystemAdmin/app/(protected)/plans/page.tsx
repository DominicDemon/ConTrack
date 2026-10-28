"use client";

import { useState, useEffect, useCallback, type FormEvent, type ReactNode } from "react";
import type { ColumnDef } from "@tanstack/react-table";
import { Package, Plus, Pencil, History, CirclePause, CirclePlay, Info, MoreHorizontal } from "lucide-react";
import { PlanAuditDialog } from "@/components/plan-audit-dialog";
import { Header } from "@/components/ui/custom/header";
import { DataTable } from "@/components/ui/custom/data-table";
import { SelectFilter } from "@/components/ui/custom/select-filter";
import { ConfirmDialog } from "@/components/ui/custom/confirm-dialog";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { Checkbox } from "@/components/ui/checkbox";
import { DropdownMenu, DropdownMenuTrigger, DropdownMenuContent, DropdownMenuItem, DropdownMenuSeparator } from "@/components/ui/dropdown-menu";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription, DialogFooter } from "@/components/ui/dialog";
import { toast } from "@/components/ui/sonner";
import { plansApi } from "@/services/plans-api";
import { getApiErrorMessage } from "@/lib/api-error";
import { currentVersions, formatPlanDuration, planDurationMonths, formatPlanPrice, type BillingCycle, type ServicePlan } from "@/lib/plan-management";

type Editor = { kind: "create" } | { kind: "edit" | "prices"; planId: number };
type Fields = { code: string; name: string; description: string; isPaid: boolean; support: boolean; users: string; contracts: string; pages: string; price: string; cycle: BillingCycle; months: string; days: string };
const EMPTY: Fields = { code: "", name: "", description: "", isPaid: true, support: true, users: "10", contracts: "100", pages: "500", price: "", cycle: "QUARTERLY", months: "3", days: "90" };
const selectClass = "h-10 w-full rounded-md border border-input bg-background px-3 text-sm focus-visible:outline-2 focus-visible:outline-ring";

function Field({ name, label, error, children }: { name: string; label: string; error?: string; children: ReactNode }) {
  return <div className="space-y-2"><Label htmlFor={name}>{label}</Label>{children}{error && <p id={`${name}-error`} role="alert" className="text-xs text-destructive">{error}</p>}</div>;
}
function Status({ plan }: { plan: ServicePlan }) {
  return <Badge variant="outline" className={plan.planStatus === "ACTIVE" ? "border-emerald-500/25 bg-emerald-500/10 text-emerald-700 dark:text-emerald-400" : "border-border text-muted-foreground"}>{plan.planStatus === "ACTIVE" ? "Đang mở bán" : "Ngừng bán"}</Badge>;
}

function Subscribers({ plan }: { plan: ServicePlan }) {
  return (
    <div className="min-w-40 space-y-1">
      <p className="text-sm font-semibold tabular-nums">
        {plan.activeSubscribers.toLocaleString("vi-VN")} <span className="font-normal text-muted-foreground">tổ chức</span>
      </p>
      {plan.planStatus === "INACTIVE" && plan.activeSubscribers > 0 && (
        <p className="max-w-52 text-xs leading-5 text-amber-700 dark:text-amber-400">
          Còn {plan.activeSubscribers.toLocaleString("vi-VN")} khách hàng đang sử dụng.
        </p>
      )}
    </div>
  );
}

export default function PlansPage() {
  const [auditId, setAuditId] = useState<number | null>(null);
  const [plans, setPlans] = useState<ServicePlan[]>([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [loadError, setLoadError] = useState("");
  const reload = useCallback(async () => {
    setLoading(true); setLoadError("");
    try { setPlans(await plansApi.list()); } catch (e) { setLoadError(getApiErrorMessage(e, "Không tải được danh sách gói.")); }
    finally { setLoading(false); }
  }, []);
  useEffect(() => {
    let active = true;
    plansApi.list().then((data) => { if (active) setPlans(data); })
      .catch((e) => { if (active) setLoadError(getApiErrorMessage(e, "Không tải được danh sách gói.")); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, []);
  const [status, setStatus] = useState("ALL");
  const [type, setType] = useState("ALL");
  const [editor, setEditor] = useState<Editor | null>(null);
  const [fields, setFields] = useState<Fields>(EMPTY);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [addingPrice, setAddingPrice] = useState(false);
  const [toggleId, setToggleId] = useState<number | null>(null);
  const selected = editor && "planId" in editor ? plans.find((p) => p.planId === editor.planId) : undefined;
  const togglePlan = plans.find((p) => p.planId === toggleId);
  const isPrices = editor?.kind === "prices";
  const isCreate = editor?.kind === "create";
  const update = <K extends keyof Fields>(key: K, value: Fields[K]) => {
    setFields((prev) => ({ ...prev, [key]: value, ...(key === "months" && Number(value) > 0 ? { days: String(Math.floor(Number(value) / 12) * 365 + (Number(value) % 12) * 30) } : {}) }));
    setErrors((prev) => ({ ...prev, [key]: "" }));
  };
  const open = (next: Editor) => {
    const plan = "planId" in next ? plans.find((p) => p.planId === next.planId) : undefined;
    setFields(plan ? { ...EMPTY, code: plan.planCode, name: plan.planName, description: plan.planDescription, isPaid: plan.isPaid, support: plan.allowsSupportTickets, users: String(plan.maxUsers), contracts: String(plan.maxContracts), pages: String(plan.maxScanPages), cycle: currentVersions(plan)[0]?.billingCycle ?? "TRIAL", price: plan.isPaid ? "" : "0", months: String(currentVersions(plan)[0] ? planDurationMonths(currentVersions(plan)[0]) : 3), days: String(currentVersions(plan)[0]?.durationDays ?? 15) } : { ...EMPTY });
    setErrors({}); setAddingPrice(false); setEditor(next);
  };
  const save = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (saving) return;
    const nextErrors: Record<string, string> = {};
    const positiveInteger = (key: "users" | "contracts" | "pages" | "days" | "months") => {
      if (!/^\d+$/.test(fields[key]) || !Number.isSafeInteger(Number(fields[key])) || Number(fields[key]) <= 0) nextErrors[key] = "Nhập số nguyên dương hợp lệ.";
    };
    if (!isPrices) {
      if (!fields.name.trim()) nextErrors.name = "Vui lòng nhập tên gói.";
      if (isCreate) {
        if (!/^[A-Z0-9_-]+$/.test(fields.code.trim())) nextErrors.code = "Nhập mã gồm chữ in hoa, số, dấu gạch ngang hoặc gạch dưới.";
        else if (plans.some((p) => p.planCode.toUpperCase() === fields.code.trim().toUpperCase())) nextErrors.code = "Mã gói đã tồn tại.";
      }
      positiveInteger("users"); positiveInteger("contracts"); positiveInteger("pages");
    }
    if (isCreate || isPrices) {
      if (fields.isPaid && (!fields.price.trim() || !Number.isFinite(Number(fields.price)) || Number(fields.price) <= 0 || !Number.isInteger(Number(fields.price)) || Number(fields.price) > Number.MAX_SAFE_INTEGER)) nextErrors.price = "Nhập giá VND là số nguyên lớn hơn 0.";
      positiveInteger("days"); if (Number(fields.days) > 730) nextErrors.days = "Tối đa 730 ngày."; if (fields.isPaid) {
        positiveInteger("months");
        if (Number(fields.months) > 24) nextErrors.months = "Thời hạn tối đa 24 tháng.";
      }
    }
    setErrors(nextErrors);
    if (Object.keys(nextErrors).length) {
      document.getElementById(Object.keys(nextErrors)[0])?.focus();
      return;
    }
    const months = fields.isPaid ? Number(fields.months) : null;
    const body = { planCode: fields.code.trim(), planName: fields.name.trim(), planDescription: fields.description.trim(), isPaid: fields.isPaid,
      allowsSupportTickets: fields.support, maxUsers: Number(fields.users), maxContracts: Number(fields.contracts), maxScanPages: Number(fields.pages),
      durationMonths: months, durationDays: Number(fields.days), price: fields.isPaid ? Number(fields.price) : 0, revision: selected?.revision ?? 0 };
    setSaving(true);
    try {
      if (isPrices && selected) await plansApi.price(selected.planId, body.price, body.revision);
      else if (isCreate) await plansApi.create(body);
      else if (selected) await plansApi.update(selected.planId, body);
      setEditor(null); toast.success("Đã lưu thay đổi."); await reload();
    } catch (e) { toast.error(getApiErrorMessage(e, "Không lưu được. Nếu gói đã thay đổi, hãy đóng và tải lại danh sách.")); }
    finally { setSaving(false); }
  };
  const input = (key: "code" | "name" | "users" | "contracts" | "pages" | "price" | "days" | "months", numeric = false) => <Input id={key} value={fields[key]} type={numeric ? "number" : "text"} min={numeric ? 1 : undefined} step={numeric ? 1 : undefined} maxLength={key === "code" ? 50 : key === "name" ? 150 : undefined} max={key === "months" ? 24 : key === "days" ? 730 : undefined} disabled={(key === "code" && !isCreate) || ((key === "months" || key === "days") && isPrices)} aria-invalid={Boolean(errors[key])} aria-describedby={errors[key] ? `${key}-error` : undefined} onChange={(e) => update(key, key === "code" ? e.target.value.toUpperCase() : e.target.value)} />;
  const priceFields = <div className="grid gap-4 sm:grid-cols-2">
    {fields.isPaid ? <><Field name="months" label="Thời hạn gói (tháng)" error={errors.months}>{input("months", true)}<p className="text-xs text-muted-foreground">Nhập từ 1 đến 24 tháng.</p></Field><Field name="days" label="Số ngày sử dụng" error={errors.days}>{input("days", true)}<p className="text-xs text-muted-foreground">Ngày hết hạn được tính theo số ngày đã xác nhận.</p></Field><Field name="price" label="Giá (VND)" error={errors.price}>{input("price", true)}</Field></> : <><Field name="days" label="Thời hạn dùng thử (ngày)" error={errors.days}>{input("days", true)}</Field><div className="space-y-2"><p className="text-sm font-medium">Giá</p><p className="py-2 text-sm text-muted-foreground">0 ₫ · Miễn phí</p></div></>}
  </div>;
  const actions = (plan: ServicePlan) => <div className="flex items-center gap-1 whitespace-nowrap">
    <Button size="sm" variant="ghost" onClick={() => open({ kind: "prices", planId: plan.planId })}><History className="size-4" />Cấu hình giá</Button>
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button size="icon" variant="ghost" className="size-9 shrink-0" aria-label={`Thao tác khác cho gói ${plan.planName}`}>
          <MoreHorizontal className="size-4" aria-hidden="true" />
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="w-52">
        <DropdownMenuItem onSelect={() => open({ kind: "edit", planId: plan.planId })}>
          <Pencil className="size-4" aria-hidden="true" />Chỉnh sửa thông tin
        </DropdownMenuItem>
        <DropdownMenuItem onSelect={() => setAuditId(plan.planId)}><History className="size-4" />Nhật ký thay đổi</DropdownMenuItem>
        <DropdownMenuSeparator />
        <DropdownMenuItem
          className={plan.planStatus === "ACTIVE" ? "text-destructive focus:text-destructive" : undefined}
          onSelect={() => setToggleId(plan.planId)}
        >
          {plan.planStatus === "ACTIVE" ? <CirclePause className="size-4" aria-hidden="true" /> : <CirclePlay className="size-4" aria-hidden="true" />}
          {plan.planStatus === "ACTIVE" ? "Ngừng bán" : "Mở bán"}
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  </div>;
  const columns: ColumnDef<ServicePlan>[] = [
    { id: "search", accessorFn: (p) => `${p.planCode} ${p.planName}`, header: "Gói dịch vụ", cell: ({ row: { original: p } }) => <div className="min-w-36"><p className="font-semibold">{p.planName}</p><p className="mt-1 text-xs text-muted-foreground">{p.planCode}</p></div> },
    { accessorKey: "isPaid", header: "Loại", cell: ({ row }) => <Badge variant="secondary">{row.original.isPaid ? "Trả phí" : "Miễn phí"}</Badge> },
    { id: "price", header: "Giá hiện hành", cell: ({ row }) => { const versions = currentVersions(row.original); return <div className="min-w-32"><p className="font-semibold tabular-nums">{formatPlanPrice(versions[0]?.price ?? 0)}</p>{versions.length > 1 && <p className="text-xs text-muted-foreground">+{versions.length - 1} mức giá khác</p>}</div>; } },
    { id: "cycle", header: "Thời hạn", cell: ({ row }) => { const v = currentVersions(row.original)[0]; return v ? formatPlanDuration(v) : "—"; } },
    { id: "limits", header: "Giới hạn sử dụng", cell: ({ row: { original: p } }) => <div className="min-w-36 text-xs leading-6 text-muted-foreground"><p>{p.maxUsers} người dùng</p><p>{p.maxContracts} hợp đồng · {p.maxScanPages} trang quét</p></div> },
    { accessorKey: "activeSubscribers", header: "Thuê bao hoạt động", cell: ({ row }) => <Subscribers plan={row.original} /> },
    { accessorKey: "planStatus", header: "Trạng thái", cell: ({ row }) => <Status plan={row.original} /> },
    { id: "actions", header: "Thao tác", cell: ({ row }) => actions(row.original) },
  ];
  const visible = plans.filter((p) => (status === "ALL" || p.planStatus === status) && (type === "ALL" || p.isPaid === (type === "PAID")));

  return <>
    <PlanAuditDialog id={auditId} onClose={() => setAuditId(null)} />
    <Header title="Gói dịch vụ" />
    <div className="flex-1 overflow-y-auto p-4 sm:p-6 lg:p-8">
      <div className="mx-auto max-w-[1600px] space-y-6">
        <div className="flex flex-wrap items-start justify-between gap-4"><div><h1 className="flex items-center gap-3 text-2xl font-bold tracking-tight"><Package className="size-6 text-primary" />Gói dịch vụ</h1><p className="mt-2 text-sm text-muted-foreground">Quản lý danh mục gói, giới hạn sử dụng và các phiên bản giá.</p></div><Button onClick={() => open({ kind: "create" })}><Plus className="size-4" />Thêm gói</Button></div>
        <div className="flex items-start gap-2 rounded-lg border border-primary/20 bg-primary/5 p-3 text-sm text-muted-foreground"><Info className="mt-0.5 size-4 shrink-0 text-primary" /><p>Thời hạn tối đa 24 tháng. Thay đổi giá được lưu lịch sử; thuê bao hiện có giữ giá và thời hạn đã chốt.</p></div>
        <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">{[["Tổng số gói", plans.length], ["Thuê bao hoạt động", plans.reduce((sum, plan) => sum + plan.activeSubscribers, 0)], ["Đang mở bán", plans.filter((p) => p.planStatus === "ACTIVE").length], ["Gói trả phí", plans.filter((p) => p.isPaid).length]].map(([label, value]) => <Card key={label}><CardContent className="p-5"><p className="text-sm text-muted-foreground">{label}</p><p className="mt-2 text-2xl font-bold tabular-nums">{value}</p></CardContent></Card>)}</div>
        <div className="flex items-center gap-3"><Button variant="outline" disabled={loading} onClick={() => void reload()}>Tải lại</Button>{loadError && <p role="alert" className="text-sm text-destructive">{loadError}</p>}</div><Card><CardContent className="p-4 sm:p-6"><DataTable isLoading={loading} columns={columns} data={visible} searchKey="search" searchPlaceholder="Tìm theo mã hoặc tên gói..." filterSlot={<><SelectFilter value={status} onChange={setStatus} options={[{ label: "Tất cả trạng thái", value: "ALL" }, { label: "Đang mở bán", value: "ACTIVE" }, { label: "Ngừng bán", value: "INACTIVE" }]} /><SelectFilter value={type} onChange={setType} options={[{ label: "Tất cả loại gói", value: "ALL" }, { label: "Miễn phí", value: "FREE" }, { label: "Trả phí", value: "PAID" }]} /></>} mobileCardRenderer={(row) => { const p = row.original; return <div className="space-y-3"><div className="flex items-center justify-between gap-2"><p className="font-semibold">{p.planName}</p><Status plan={p} /></div><p className="text-xs text-muted-foreground">{p.planCode} · {p.isPaid ? "Trả phí" : "Miễn phí"}</p><div className="space-y-1">{currentVersions(p).map((v) => <p key={v.versionNumber} className="text-sm font-medium">{formatPlanPrice(v.price)} <span className="font-normal text-muted-foreground">/ {formatPlanDuration(v)}</span></p>)}</div><p className="text-xs text-muted-foreground">{p.maxUsers} người dùng · {p.maxContracts} hợp đồng · {p.maxScanPages} trang quét</p><Subscribers plan={p} />{actions(p)}</div>; }} /></CardContent></Card>
      </div>
    </div>
    <Dialog open={editor !== null} onOpenChange={(value) => { if (!value && !saving) setEditor(null); }}>
      <DialogContent className="max-h-[90dvh] overflow-y-auto sm:max-w-2xl">
        <DialogHeader><DialogTitle>{isCreate ? "Thêm gói dịch vụ" : isPrices ? `Cấu hình giá · ${selected?.planName}` : "Chỉnh sửa thông tin gói"}</DialogTitle><DialogDescription>{isPrices ? "Chỉnh giá cho thời hạn đã chọn. Mức giá cũ được lưu trong lịch sử thay đổi." : "Thiết lập thông tin và giới hạn sử dụng cho gói dịch vụ."}</DialogDescription></DialogHeader>
        {selected && selected.activeSubscribers > 0 && <div className="rounded-lg border border-amber-500/25 bg-amber-500/5 p-3 text-sm text-amber-700 dark:text-amber-400">Gói có {selected.activeSubscribers.toLocaleString("vi-VN")} tổ chức đang sử dụng. Cần cân nhắc ảnh hưởng đến khách hàng hiện có khi chỉnh sửa cấu hình.</div>}
        {isPrices && selected ? <>
          <div className="space-y-3"><h3 className="text-sm font-semibold">Giá hiện hành</h3><div className="grid gap-3 sm:grid-cols-2">{currentVersions(selected).map((v) => <div key={v.versionNumber} className="rounded-lg border bg-primary/5 p-4"><p className="text-sm text-muted-foreground">{formatPlanDuration(v)}</p><p className="mt-2 text-xl font-bold">{formatPlanPrice(v.price)}</p></div>)}</div></div>
          {!addingPrice ? <Button variant="outline" onClick={() => setAddingPrice(true)}><Plus className="size-4" />Thêm phiên bản giá</Button> : <form noValidate onSubmit={save} className="space-y-4 rounded-lg border p-4"><h3 className="text-sm font-semibold">Phiên bản giá mới</h3>{priceFields}<p className="text-xs text-muted-foreground">Có hiệu lực ngay. Giá cũ của gói này sẽ được chuyển vào lịch sử; các gói thời hạn khác giữ nguyên.</p><div className="flex justify-end gap-2"><Button type="button" variant="ghost" onClick={() => { setAddingPrice(false); setErrors({}); }}>Hủy</Button><Button type="submit" disabled={saving}>Lưu phiên bản giá</Button></div></form>}
          <div className="space-y-3"><h3 className="text-sm font-semibold">Lịch sử thay đổi giá</h3><div className="space-y-2">{selected.versions.every((v) => v.status === "ACTIVE") && <p className="text-sm text-muted-foreground">Chưa có thay đổi giá.</p>}{selected.versions.filter((v) => v.status !== "ACTIVE").reverse().map((v) => <div key={v.versionNumber} className="flex flex-wrap items-center justify-between gap-2 rounded-lg border p-3 text-sm"><div><p className="font-medium">v{v.versionNumber} · {formatPlanDuration(v)} · {formatPlanPrice(v.price)}</p><p className="mt-1 text-xs text-muted-foreground">{new Date(v.effectiveFrom).toLocaleString("vi-VN")} → {v.effectiveTo ? new Date(v.effectiveTo).toLocaleString("vi-VN") : "Hiện tại"}{v.billingCycle === "TRIAL" && ` · ${v.durationDays} ngày`}</p></div><Badge variant={v.status === "ACTIVE" ? "secondary" : "outline"}>{v.status === "ACTIVE" ? "Hiện hành" : "Đã thay thế"}</Badge></div>)}</div></div>
          <DialogFooter><Button variant="outline" onClick={() => setEditor(null)}>Đóng</Button></DialogFooter>
        </> : <form noValidate onSubmit={save} className="space-y-5">
          <div className="grid gap-4 sm:grid-cols-2"><Field name="code" label="Mã gói" error={errors.code}>{input("code")}</Field><Field name="name" label="Tên gói" error={errors.name}>{input("name")}</Field></div>
          <Field name="description" label="Mô tả"><Textarea id="description" maxLength={500} value={fields.description} onChange={(e) => update("description", e.target.value)} /></Field>
          <div className="grid gap-4 sm:grid-cols-3"><Field name="users" label="Người dùng tối đa" error={errors.users}>{input("users", true)}</Field><Field name="contracts" label="Hợp đồng tối đa" error={errors.contracts}>{input("contracts", true)}</Field><Field name="pages" label="Trang quét tối đa" error={errors.pages}>{input("pages", true)}</Field></div>
          <div className="flex items-center gap-2"><Checkbox id="support" checked={fields.support} onCheckedChange={(value) => update("support", value === true)} /><Label htmlFor="support">Cho phép gửi yêu cầu hỗ trợ khách hàng</Label></div>
          {isCreate && <div className="space-y-4 border-t pt-4"><Field name="plan-type" label="Loại gói"><select id="plan-type" className={selectClass} value={fields.isPaid ? "PAID" : "FREE"} onChange={(e) => { const paid = e.target.value === "PAID"; setFields((prev) => ({ ...prev, isPaid: paid, cycle: paid ? "QUARTERLY" : "TRIAL", price: paid ? "" : "0", days: paid ? "90" : "15" })); setErrors({}); }}><option value="PAID">Trả phí</option><option value="FREE">Miễn phí / Dùng thử</option></select></Field>{priceFields}</div>}
          <DialogFooter><Button type="button" variant="outline" onClick={() => setEditor(null)}>Hủy</Button><Button type="submit" disabled={saving}>{isCreate ? "Thêm gói" : "Lưu thay đổi"}</Button></DialogFooter>
        </form>}
      </DialogContent>
    </Dialog>
    <ConfirmDialog isOpen={Boolean(togglePlan)} onClose={() => setToggleId(null)} title={togglePlan?.planStatus === "ACTIVE" ? "Ngừng bán gói dịch vụ?" : "Mở bán gói dịch vụ?"} description={<span className="block space-y-2"><span className="block">{`Bạn muốn ${togglePlan?.planStatus === "ACTIVE" ? "ngừng bán" : "mở bán"} gói ${togglePlan?.planName ?? ""}? Thay đổi sẽ được lưu vào hệ thống.`}</span>{togglePlan && togglePlan.activeSubscribers > 0 && <span className="block font-medium text-foreground">Gói này còn {togglePlan.activeSubscribers.toLocaleString("vi-VN")} tổ chức đang sử dụng. Trạng thái mở/ngừng bán chỉ điều chỉnh việc đăng ký mới, không hủy thuê bao hiện có.</span>}</span>} confirmText="Xác nhận" isLoading={saving} onConfirm={async () => { if (!togglePlan || saving) return; setSaving(true); try { await plansApi.status(togglePlan.planId, togglePlan.planStatus !== "ACTIVE", togglePlan.revision ?? 0); setToggleId(null); await reload(); toast.success("Đã cập nhật trạng thái gói."); } catch (e) { toast.error(getApiErrorMessage(e, "Không cập nhật được trạng thái. Vui lòng tải lại.")); } finally { setSaving(false); } }} />
  </>;
}

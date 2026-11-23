"use client";
import { useState } from "react";
import { Check, ChevronsUpDown, X } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Command, CommandEmpty, CommandGroup, CommandInput, CommandItem, CommandList } from "@/components/ui/command";
import { Popover, PopoverContent, PopoverTrigger } from "@/components/ui/popover";
import { cn } from "@/lib/utils";
import type { DepartmentStaffDto } from "@/services/ticket-api";

const workload = (s: DepartmentStaffDto) => `${s.activeTicketCount ?? 0} task`;

/**
 * Who handles a support ticket: a drop-down with search by name, phone or employee code, one or several people,
 * each shown with the open tickets already on them ("Nguyễn Văn A — 3 task").
 */
export default function StaffPicker({ staff, selected, onToggle, disabled, activeIds, pastIds }: {
  staff: DepartmentStaffDto[]; selected: number[]; onToggle: (id: number) => void; disabled?: boolean; activeIds: number[]; pastIds: number[];
}) {
  const [open, setOpen] = useState(false);
  const chosen = staff.filter(s => selected.includes(s.employeeId));
  return <div className="space-y-2">
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <Button type="button" variant="outline" role="combobox" aria-expanded={open} disabled={disabled} className="h-10 w-full justify-between bg-background text-sm font-medium">
          <span className="truncate">{chosen.length ? chosen.map(s => s.employeeName).join(", ") : "Chọn hoặc tìm nhân sự…"}</span>
          <ChevronsUpDown className="size-4 shrink-0 opacity-50"/>
        </Button>
      </PopoverTrigger>
      <PopoverContent className="w-[--radix-popover-trigger-width] min-w-[320px] p-0" align="start">
        <Command filter={(value, search) => value.toLowerCase().normalize("NFD").replace(/\p{M}/gu, "").includes(search.toLowerCase().normalize("NFD").replace(/\p{M}/gu, "")) ? 1 : 0}>
          <CommandInput placeholder="Tìm theo tên, số điện thoại hoặc mã NV…"/>
          <CommandList>
            <CommandEmpty>Không tìm thấy nhân sự.</CommandEmpty>
            <CommandGroup>{staff.map(s => {
              const isChecked = selected.includes(s.employeeId);
              return <CommandItem key={s.employeeId} value={`${s.employeeName} ${s.employeePhone ?? ""} ${s.employeeCode ?? ""} ${s.employeeId}`} onSelect={() => onToggle(s.employeeId)}>
                <Check className={cn("size-4", isChecked ? "opacity-100" : "opacity-0")}/>
                <div className="min-w-0 flex-1">
                  <div className="flex items-center justify-between gap-2">
                    <span className="truncate font-medium">{s.isManager ? `${s.employeeName} (Trưởng phòng)` : s.employeeName}</span>
                    <span className={cn("shrink-0 rounded px-1.5 py-0.5 text-[11px] font-semibold", (s.activeTicketCount ?? 0) >= 5
                      ? "bg-amber-100 text-amber-800 dark:bg-amber-950 dark:text-amber-300" : "bg-slate-100 text-slate-700 dark:bg-slate-800 dark:text-slate-300")}>{workload(s)}</span>
                  </div>
                  <p className="truncate text-[11px] text-muted-foreground">{[s.employeePhone, s.employeeCode && `Mã NV: ${s.employeeCode}`].filter(Boolean).join(" · ") || s.employeeEmail || "Nhân viên phòng ban"}
                    {activeIds.includes(s.employeeId) ? " · Đang phụ trách" : pastIds.includes(s.employeeId) ? " · Đã bàn giao đợt trước" : ""}</p>
                </div>
              </CommandItem>;
            })}</CommandGroup>
          </CommandList>
        </Command>
      </PopoverContent>
    </Popover>
    {chosen.length > 0 && <div className="flex flex-wrap gap-1.5">{chosen.map(s =>
      <span key={s.employeeId} className="inline-flex items-center gap-1 rounded-full border bg-blue-50 px-2 py-0.5 text-xs font-medium text-blue-800 dark:bg-blue-950/50 dark:text-blue-300">
        {s.employeeName} — {workload(s)}
        {!disabled && <button type="button" aria-label={`Bỏ chọn ${s.employeeName}`} className="rounded-full hover:bg-blue-100 dark:hover:bg-blue-900" onClick={() => onToggle(s.employeeId)}><X className="size-3"/></button>}
      </span>)}</div>}
  </div>;
}

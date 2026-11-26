"use client";

import { useState } from "react";
import { Check, Loader2, Pencil, Plus, Settings2, Tag, Trash2, X } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuRadioGroup,
  DropdownMenuRadioItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { Input } from "@/components/ui/input";
import { toast } from "@/components/ui/sonner";
import { cn } from "@/lib/utils";
import { getScanErrorMessage } from "@/services/scans/http";
import {
  SCAN_LABEL_COLORS,
  scannedContractsApi,
  type ScanLabel,
  type ScanLabelColor,
  type ScanLabelTag,
} from "@/services/scans/scanned-contracts";

const MAX_NAME = 60;

/** Background, text and border of each colour, readable in light and dark themes. */
const COLOR_CLASSES: Record<ScanLabelColor, { chip: string; dot: string; name: string }> = {
  slate: { chip: "border-slate-300 bg-slate-100 text-slate-800 dark:border-slate-600 dark:bg-slate-800 dark:text-slate-100", dot: "bg-slate-500", name: "Xám" },
  blue: { chip: "border-blue-300 bg-blue-50 text-blue-800 dark:border-blue-700 dark:bg-blue-950 dark:text-blue-200", dot: "bg-blue-500", name: "Xanh dương" },
  green: { chip: "border-green-300 bg-green-50 text-green-800 dark:border-green-700 dark:bg-green-950 dark:text-green-200", dot: "bg-green-500", name: "Xanh lá" },
  amber: { chip: "border-amber-300 bg-amber-50 text-amber-900 dark:border-amber-700 dark:bg-amber-950 dark:text-amber-200", dot: "bg-amber-500", name: "Vàng" },
  red: { chip: "border-red-300 bg-red-50 text-red-800 dark:border-red-700 dark:bg-red-950 dark:text-red-200", dot: "bg-red-500", name: "Đỏ" },
  violet: { chip: "border-violet-300 bg-violet-50 text-violet-800 dark:border-violet-700 dark:bg-violet-950 dark:text-violet-200", dot: "bg-violet-500", name: "Tím" },
  teal: { chip: "border-teal-300 bg-teal-50 text-teal-800 dark:border-teal-700 dark:bg-teal-950 dark:text-teal-200", dot: "bg-teal-500", name: "Xanh ngọc" },
  pink: { chip: "border-pink-300 bg-pink-50 text-pink-800 dark:border-pink-700 dark:bg-pink-950 dark:text-pink-200", dot: "bg-pink-500", name: "Hồng" },
};

export function LabelChip({ label, onClick }: { label: ScanLabelTag; onClick?: () => void }) {
  const className = cn(
    "inline-flex max-w-full items-center gap-1 rounded-full border px-2 py-0.5 text-xs font-medium",
    COLOR_CLASSES[label.color].chip,
    onClick && "cursor-pointer hover:opacity-80",
  );
  const content = (
    <>
      <Tag className="size-3 shrink-0" aria-hidden="true" />
      <span className="truncate">{label.name}</span>
    </>
  );
  return onClick ? (
    <button type="button" className={className} onClick={onClick} title={`Lọc theo nhãn ${label.name}`}>
      {content}
    </button>
  ) : (
    <span className={className}>{content}</span>
  );
}

function ColorPicker({ value, onChange }: { value: ScanLabelColor; onChange: (color: ScanLabelColor) => void }) {
  return (
    <div className="flex flex-wrap gap-1.5" role="radiogroup" aria-label="Màu nhãn">
      {SCAN_LABEL_COLORS.map((color) => (
        <button
          key={color}
          type="button"
          role="radio"
          aria-checked={value === color}
          aria-label={COLOR_CLASSES[color].name}
          title={COLOR_CLASSES[color].name}
          onClick={() => onChange(color)}
          className={cn(
            "flex size-6 items-center justify-center rounded-full ring-offset-background focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring",
            COLOR_CLASSES[color].dot,
            value === color && "ring-2 ring-foreground ring-offset-2",
          )}
        >
          {value === color && <Check className="size-3.5 text-white" />}
        </button>
      ))}
    </div>
  );
}

/** Name and colour of a new label; Enter creates it. */
function NewLabelForm({ onCreated }: { onCreated: (label: ScanLabel) => void }) {
  const [name, setName] = useState("");
  const [color, setColor] = useState<ScanLabelColor>("blue");
  const [busy, setBusy] = useState(false);
  const create = async () => {
    if (!name.trim()) return;
    setBusy(true);
    try {
      const label = await scannedContractsApi.createLabel(name.trim(), color);
      toast.success(`Đã tạo nhãn ${label.name}.`);
      setName("");
      onCreated(label);
    } catch (error) {
      toast.error(getScanErrorMessage(error, "Không tạo được nhãn."));
    } finally {
      setBusy(false);
    }
  };
  return (
    <div className="space-y-2 rounded-md border border-dashed p-3">
      <p className="text-sm font-medium">Tạo nhãn mới</p>
      <div className="flex gap-2">
        <Input
          value={name}
          maxLength={MAX_NAME}
          placeholder="Ví dụ: Thuê kho, Lưu trữ 2014"
          aria-label="Tên nhãn mới"
          onChange={(event) => setName(event.target.value)}
          onKeyDown={(event) => {
            if (event.key === "Enter") {
              event.preventDefault();
              void create();
            }
          }}
        />
        <Button type="button" variant="outline" disabled={busy || !name.trim()} onClick={() => void create()}>
          {busy ? <Loader2 className="size-4 animate-spin" /> : <Plus className="size-4" />}
          Tạo
        </Button>
      </div>
      <ColorPicker value={color} onChange={setColor} />
    </div>
  );
}

/** Ticks the labels of one scan; new labels can be made on the spot. */
export function JobLabelsDialog({ jobId, fileName, current, labels, open, onOpenChange, onSaved, onLabelsChanged }: {
  jobId: string;
  fileName: string;
  current: ScanLabelTag[];
  labels: ScanLabel[];
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onSaved: () => void;
  onLabelsChanged: () => void;
}) {
  const [chosen, setChosen] = useState<Set<string>>(() => new Set(current.map((label) => label.labelId)));
  const [busy, setBusy] = useState(false);
  const toggle = (labelId: string, on: boolean) =>
    setChosen((previous) => {
      const next = new Set(previous);
      if (on) next.add(labelId);
      else next.delete(labelId);
      return next;
    });
  const save = async () => {
    setBusy(true);
    try {
      await scannedContractsApi.setJobLabels(jobId, [...chosen]);
      toast.success("Đã lưu nhãn.");
      onOpenChange(false);
      onSaved();
    } catch (error) {
      toast.error(getScanErrorMessage(error, "Không lưu được nhãn."));
    } finally {
      setBusy(false);
    }
  };
  return (
    <Dialog
      open={open}
      onOpenChange={(next) => {
        if (busy) return;
        // Opened again: start from the labels the scan has now.
        if (next) setChosen(new Set(current.map((label) => label.labelId)));
        onOpenChange(next);
      }}
    >
      <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-md">
        <DialogHeader>
          <DialogTitle>Gắn nhãn</DialogTitle>
          <DialogDescription className="[overflow-wrap:anywhere]">
            {fileName}. Nhãn dùng chung cả công ty; một bản scan gắn được nhiều nhãn.
          </DialogDescription>
        </DialogHeader>
        {labels.length === 0 ? (
          <p className="text-sm text-muted-foreground">Chưa có nhãn nào. Tạo nhãn đầu tiên ở dưới.</p>
        ) : (
          <ul className="max-h-64 space-y-1 overflow-y-auto">
            {labels.map((label) => {
              const id = `label-${jobId}-${label.labelId}`;
              return (
                <li key={label.labelId}>
                  <label htmlFor={id} className="flex cursor-pointer items-center gap-3 rounded-md px-2 py-1.5 hover:bg-muted">
                    <Checkbox id={id} checked={chosen.has(label.labelId)} onCheckedChange={(on) => toggle(label.labelId, on === true)} />
                    <LabelChip label={label} />
                    <span className="ml-auto text-xs text-muted-foreground tabular-nums">{label.fileCount}</span>
                  </label>
                </li>
              );
            })}
          </ul>
        )}
        <NewLabelForm
          onCreated={(label) => {
            toggle(label.labelId, true);
            onLabelsChanged();
          }}
        />
        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)} disabled={busy}>
            Hủy
          </Button>
          <Button onClick={() => void save()} disabled={busy}>
            {busy && <Loader2 className="size-4 animate-spin" />}
            Lưu nhãn
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

/** One label in the manage dialog: rename and recolour in place, or delete after a second click. */
function ManagedLabel({ label, onChanged }: { label: ScanLabel; onChanged: () => void }) {
  const [editing, setEditing] = useState(false);
  const [name, setName] = useState(label.name);
  const [color, setColor] = useState<ScanLabelColor>(label.color);
  const [confirming, setConfirming] = useState(false);
  const [busy, setBusy] = useState(false);

  const run = async (action: () => Promise<unknown>, done: string, failed: string) => {
    setBusy(true);
    try {
      await action();
      toast.success(done);
      setEditing(false);
      setConfirming(false);
      onChanged();
    } catch (error) {
      toast.error(getScanErrorMessage(error, failed));
    } finally {
      setBusy(false);
    }
  };

  if (editing) {
    return (
      <li className="space-y-2 rounded-md border p-2">
        <div className="flex gap-2">
          <Input value={name} maxLength={MAX_NAME} aria-label="Tên nhãn" onChange={(event) => setName(event.target.value)} />
          <Button
            size="sm"
            disabled={busy || !name.trim()}
            onClick={() => void run(() => scannedContractsApi.updateLabel(label.labelId, name.trim(), color), "Đã sửa nhãn.", "Không sửa được nhãn.")}
          >
            {busy ? <Loader2 className="size-4 animate-spin" /> : <Check className="size-4" />}
            Lưu
          </Button>
          <Button size="sm" variant="ghost" aria-label="Thôi sửa" onClick={() => setEditing(false)} disabled={busy}>
            <X className="size-4" />
          </Button>
        </div>
        <ColorPicker value={color} onChange={setColor} />
      </li>
    );
  }

  return (
    <li className="flex items-center gap-2 rounded-md px-2 py-1.5 hover:bg-muted">
      <LabelChip label={label} />
      <span className="text-xs text-muted-foreground tabular-nums">{label.fileCount} bản scan</span>
      <span className="ml-auto flex gap-1">
        {confirming ? (
          <>
            <Button
              size="sm"
              variant="destructive"
              disabled={busy}
              onClick={() => void run(() => scannedContractsApi.deleteLabel(label.labelId), `Đã xóa nhãn ${label.name}.`, "Không xóa được nhãn.")}
            >
              {busy && <Loader2 className="size-4 animate-spin" />}
              Xóa nhãn
            </Button>
            <Button size="sm" variant="ghost" onClick={() => setConfirming(false)} disabled={busy}>
              Hủy
            </Button>
          </>
        ) : (
          <>
            <Button size="sm" variant="ghost" aria-label={`Sửa nhãn ${label.name}`} onClick={() => setEditing(true)}>
              <Pencil className="size-4" />
            </Button>
            <Button
              size="sm"
              variant="ghost"
              className="text-destructive hover:text-destructive"
              aria-label={`Xóa nhãn ${label.name}`}
              onClick={() => setConfirming(true)}
            >
              <Trash2 className="size-4" />
            </Button>
          </>
        )}
      </span>
    </li>
  );
}

/** Rename, recolour and delete labels: checkers only, since it changes every scan carrying them. */
export function ManageLabelsDialog({ labels, open, onOpenChange, onChanged }: {
  labels: ScanLabel[];
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onChanged: () => void;
}) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>Quản lý nhãn</DialogTitle>
          <DialogDescription>
            Sửa tên hoặc màu sẽ đổi trên mọi bản scan mang nhãn đó. Xóa nhãn chỉ gỡ nhãn khỏi các bản scan, bản scan vẫn còn.
          </DialogDescription>
        </DialogHeader>
        {labels.length === 0 ? (
          <p className="text-sm text-muted-foreground">Chưa có nhãn nào.</p>
        ) : (
          <ul className="space-y-1">
            {labels.map((label) => (
              <ManagedLabel key={`${label.labelId}-${label.name}-${label.color}`} label={label} onChanged={onChanged} />
            ))}
          </ul>
        )}
        <NewLabelForm onCreated={onChanged} />
      </DialogContent>
    </Dialog>
  );
}

/** "Nhãn: …" next to the other filters; the manage entry is for checkers. */
export function LabelFilter({ labels, value, onChange, onManage }: {
  labels: ScanLabel[];
  value: string | null;
  onChange: (labelId: string | null) => void;
  onManage?: () => void;
}) {
  const chosen = labels.find((label) => label.labelId === value);
  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button type="button" size="sm" variant={chosen ? "default" : "outline"} aria-label="Lọc theo nhãn">
          <Tag className="size-4" />
          <span className="max-w-40 truncate">{chosen ? chosen.name : "Nhãn"}</span>
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="start" className="max-h-80 w-64 overflow-y-auto">
        <DropdownMenuLabel>Lọc theo nhãn</DropdownMenuLabel>
        <DropdownMenuRadioGroup value={value ?? ""} onValueChange={(next) => onChange(next || null)}>
          <DropdownMenuRadioItem value="">Mọi nhãn</DropdownMenuRadioItem>
          {labels.map((label) => (
            <DropdownMenuRadioItem key={label.labelId} value={label.labelId}>
              <span className={cn("size-2.5 shrink-0 rounded-full", COLOR_CLASSES[label.color].dot)} aria-hidden="true" />
              <span className="truncate">{label.name}</span>
              <span className="ml-auto text-xs text-muted-foreground tabular-nums">{label.fileCount}</span>
            </DropdownMenuRadioItem>
          ))}
        </DropdownMenuRadioGroup>
        {labels.length === 0 && <p className="px-2 py-1.5 text-sm text-muted-foreground">Chưa có nhãn nào.</p>}
        {onManage && (
          <>
            <DropdownMenuSeparator />
            <DropdownMenuItem onSelect={onManage}>
              <Settings2 className="size-4" />
              Quản lý nhãn
            </DropdownMenuItem>
          </>
        )}
      </DropdownMenuContent>
    </DropdownMenu>
  );
}

"use client";

import { useId, useState, type FormEvent } from "react";
import { Loader2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import type { AddScanKeywordInput, ScanKeywordType } from "@/services/scans/signed-scans";
import { KEYWORD_TYPE_LABELS, KEYWORD_TYPES } from "./format";

const PLACEHOLDERS: Record<ScanKeywordType, string> = {
  ContractCode: "VD: HD-20261026-000001",
  TaxCode: "VD: 0312345678",
  PhoneNumber: "VD: 0901 234 567",
  CustomerName: "VD: Công ty TNHH Thương mại ABC",
};

interface AddKeywordDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  /** Resolves true when the keyword was saved. */
  onSubmit: (input: AddScanKeywordInput) => Promise<boolean>;
}

export function AddKeywordDialog({ open, onOpenChange, onSubmit }: AddKeywordDialogProps) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-md">
        {/* The form mounts with the dialog, so each opening starts empty. */}
        <AddKeywordForm
          onCancel={() => onOpenChange(false)}
          onSubmit={async (input) => {
            if (await onSubmit(input)) onOpenChange(false);
          }}
        />
      </DialogContent>
    </Dialog>
  );
}

function AddKeywordForm({
  onCancel,
  onSubmit,
}: {
  onCancel: () => void;
  onSubmit: (input: AddScanKeywordInput) => Promise<void>;
}) {
  const [type, setType] = useState<ScanKeywordType>("ContractCode");
  const [value, setValue] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const typeId = useId();
  const valueId = useId();

  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (!value.trim()) {
      setError("Hãy nhập giá trị từ khóa.");
      return;
    }
    setError(null);
    setSaving(true);
    try {
      await onSubmit({ type, value: value.trim() });
    } finally {
      setSaving(false);
    }
  };

  return (
    <form onSubmit={submit} noValidate className="space-y-4">
      <DialogHeader>
        <DialogTitle>Thêm từ khóa</DialogTitle>
        <DialogDescription>
          Dùng khi OCR đọc thiếu, ví dụ bản quét mờ hoặc có chữ viết tay. Hệ thống tự bỏ dấu cách và chuẩn hóa
          trước khi lưu.
        </DialogDescription>
      </DialogHeader>

      <div className="flex flex-col gap-1.5">
        <Label htmlFor={typeId}>Loại từ khóa</Label>
        <Select value={type} onValueChange={(next) => setType(next as ScanKeywordType)} disabled={saving}>
          <SelectTrigger id={typeId} className="w-full">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {KEYWORD_TYPES.map((item) => (
              <SelectItem key={item} value={item}>
                {KEYWORD_TYPE_LABELS[item]}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      <div className="flex flex-col gap-1.5">
        <Label htmlFor={valueId}>Giá trị</Label>
        <Input
          id={valueId}
          value={value}
          onChange={(event) => setValue(event.target.value)}
          placeholder={PLACEHOLDERS[type]}
          autoComplete="off"
          disabled={saving}
          aria-invalid={error ? true : undefined}
          aria-describedby={error ? `${valueId}-error` : undefined}
        />
        {error && (
          <p id={`${valueId}-error`} className="text-sm text-destructive">
            {error}
          </p>
        )}
      </div>

      <DialogFooter>
        <Button type="button" variant="outline" onClick={onCancel} disabled={saving}>
          Hủy
        </Button>
        <Button type="submit" disabled={saving}>
          {saving && <Loader2 className="animate-spin" aria-hidden="true" />}
          Thêm
        </Button>
      </DialogFooter>
    </form>
  );
}

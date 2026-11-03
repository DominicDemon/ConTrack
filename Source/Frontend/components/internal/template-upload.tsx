"use client";
import { useState } from "react";
import { Upload, Loader2 } from "lucide-react";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription, DialogFooter } from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { internalApi, type TemplateDefinition } from "@/services/internal-api";
import { internalStyles as ui } from "./internal-theme";

export default function TemplateUpload({ onImported, disabled }: { onImported: (template: TemplateDefinition) => void; disabled?: boolean }) {
  const [open, setOpen] = useState(false), [busy, setBusy] = useState(false), [error, setError] = useState("");
  const [file, setFile] = useState<{ fileName: string; base64: string } | null>(null);
  const [name, setName] = useState(""), [content, setContent] = useState(""), [warning, setWarning] = useState("");
  const [checked, setChecked] = useState(false);
  async function extract(selected?: File) {
    if (!selected) return;
    setFile(null); setContent(""); setName(""); setChecked(false); setError(""); setWarning("");
    if (!/\.(docx|pdf)$/i.test(selected.name) || selected.size === 0 || selected.size > 10 * 1024 * 1024) { setError("Chọn DOCX/PDF có nội dung, tối đa 10 MB. File .doc cần lưu lại thành .docx."); return; }
    setBusy(true);
    try {
      const base64 = await new Promise<string>((resolve, reject) => { const reader = new FileReader(); reader.onload = () => resolve(String(reader.result).split(",")[1]); reader.onerror = () => reject(new Error("Không đọc được file.")); reader.readAsDataURL(selected); });
      const source = { fileName: selected.name, base64 };
      const result = await internalApi<{ name: string; content: string; warning?: string }>("/contract-templates/extract", "POST", source);
      setFile(source); setName(result.name); setContent(result.content); setWarning(result.warning || "Hãy đối chiếu với file gốc; bố cục, ảnh và bảng không được giữ nguyên.");
    } catch (e) { setError((e as Error).message); } finally { setBusy(false); }
  }
  async function save() {
    if (!file || !checked || !name.trim() || !content.trim()) return;
    setBusy(true); setError("");
    try { const template = await internalApi<TemplateDefinition>("/contract-templates/import", "POST", { ...file, name, content }); onImported(template); setOpen(false); }
    catch (e) { setError((e as Error).message); } finally { setBusy(false); }
  }
  return <>
    <Button type="button" variant="outline" disabled={disabled} onClick={() => { setOpen(true); setError(""); setFile(null); setContent(""); setName(""); setWarning(""); setChecked(false); }}><Upload className="size-4"/>Tải lên hợp đồng mẫu</Button>
    <Dialog open={open} onOpenChange={value => { if (!busy) setOpen(value); }}><DialogContent className="sm:max-w-3xl max-h-[90vh] overflow-y-auto" onInteractOutside={e => e.preventDefault()}>
      <DialogHeader><DialogTitle>Tải lên hợp đồng mẫu</DialogTitle><DialogDescription>Chọn Word (.docx) hoặc PDF, tối đa 10 MB / 100 trang PDF. Kiểm tra nội dung trước khi lưu thành mẫu dùng chung trong công ty.</DialogDescription></DialogHeader>
      <label className="grid gap-2 text-sm font-medium">File hợp đồng<Input type="file" accept=".docx,.pdf" disabled={busy} onChange={e => void extract(e.target.files?.[0])}/></label>
      {busy && <p role="status" className="flex items-center gap-2 text-sm"><Loader2 className="size-4 animate-spin"/>Đang xử lý…</p>}
      {error && <p role="alert" className={ui.error}>{error}</p>}
      {file && <>
        <p className="rounded-lg bg-amber-50 p-3 text-sm text-amber-900">{warning} PDF scan cần tự bổ sung chữ ở ô dưới; chưa có nhận dạng chữ tự động.</p>
        <label className="grid gap-2 text-sm font-medium">Tên mẫu<Input value={name} maxLength={200} disabled={busy} onChange={e => setName(e.target.value)}/></label>
        <label className="grid gap-2 text-sm font-medium">Nội dung mẫu<Textarea value={content} maxLength={100000} rows={14} disabled={busy} onChange={e => { setContent(e.target.value); setChecked(false); }} placeholder="Dán hoặc bổ sung nội dung hợp đồng tại đây…"/></label>
        <p className="text-xs text-muted-foreground">{content.length.toLocaleString("vi-VN")} / 100.000 ký tự. Nội dung chữ sẽ được sửa trực tiếp khi soạn nháp; thông tin trong file không tự thay theo khách hàng.</p>
        <label className="flex items-start gap-2 text-sm"><input type="checkbox" className="mt-1" checked={checked} disabled={busy} onChange={e => setChecked(e.target.checked)}/>Tôi đã kiểm tra nội dung so với file gốc. Mẫu này chưa được coi là mẫu đã phê duyệt.</label>
      </>}
      <DialogFooter><Button type="button" variant="outline" disabled={busy} onClick={() => setOpen(false)}>Hủy</Button><Button type="button" disabled={busy || !file || !checked || !name.trim() || !content.trim()} onClick={() => void save()}>Lưu mẫu và sử dụng</Button></DialogFooter>
    </DialogContent></Dialog>
  </>;
}

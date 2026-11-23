"use client";
import { useState } from "react";
import { CheckCircle2, RotateCcw } from "lucide-react";
import { Button } from "@/components/ui/button";
import { toast } from "@/components/ui/sonner";
import { customerTicketApi } from "@/services/portal-ticket-api";
import type { TicketDto } from "@/services/ticket-api";

/** Footer of a closed ticket: the customer can read the history, or reopen it with a reason when the problem is not solved. */
export default function TicketReopen({ ticketId, onReopened }: { ticketId: number; onReopened: (ticket: TicketDto) => void }) {
  const [open, setOpen] = useState(false); const [reason, setReason] = useState(""); const [busy, setBusy] = useState(false);
  const submit = async () => {
    if (reason.trim().length < 5) { toast.warning("Cho biết lý do mở lại (ít nhất 5 ký tự)."); return; }
    setBusy(true);
    try { onReopened(await customerTicketApi.reopen(ticketId, reason.trim())); toast.success("Đã mở lại yêu cầu. Bộ phận hỗ trợ sẽ xử lý tiếp."); setOpen(false); setReason(""); }
    catch (e) { toast.error((e as Error).message || "Không mở lại được yêu cầu."); }
    finally { setBusy(false); }
  };
  return <div className="border-t bg-slate-100 px-4 py-3.5 text-xs text-slate-600 dark:bg-slate-900 dark:text-slate-400">
    <div className="flex flex-wrap items-center justify-center gap-2">
      <CheckCircle2 className="size-4 text-emerald-600"/>
      <span className="font-medium">Yêu cầu hỗ trợ đã hoàn thành và đóng.</span>
      {!open && <Button type="button" size="sm" variant="outline" onClick={() => setOpen(true)}><RotateCcw className="size-3.5"/>Chưa hài lòng? Mở lại yêu cầu</Button>}
    </div>
    {open && <div className="mx-auto mt-3 grid max-w-xl gap-2">
      <label htmlFor="reopen-reason" className="font-medium text-slate-700 dark:text-slate-300">Vấn đề còn tồn tại là gì?</label>
      <textarea id="reopen-reason" className="min-h-20 rounded-md border bg-background p-2 text-sm" maxLength={2000} value={reason} disabled={busy}
        placeholder="Ví dụ: Lỗi vẫn xảy ra khi tôi ký hợp đồng…" onChange={e => setReason(e.target.value)}/>
      <div className="flex justify-end gap-2">
        <Button type="button" size="sm" variant="ghost" disabled={busy} onClick={() => setOpen(false)}>Hủy</Button>
        <Button type="button" size="sm" disabled={busy} onClick={() => void submit()}>{busy ? "Đang gửi…" : "Mở lại yêu cầu"}</Button>
      </div>
    </div>}
  </div>;
}

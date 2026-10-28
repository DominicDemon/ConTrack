"use client";
import { useEffect, useState } from "react";
import client from "@/lib/axios-interceptor";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription } from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { getApiErrorMessage } from "@/lib/api-error";
interface Audit { id: number; actorId: number; action: string; beforeJson: string; afterJson: string; occurredAt: string }
const labels: Record<string, string> = { PLAN_CREATED: "Tạo gói", PLAN_UPDATED: "Chỉnh thông tin", PLAN_STATUS_CHANGED: "Đổi trạng thái", PLAN_PRICE_CHANGED: "Đổi giá" };
export function PlanAuditDialog({ id, onClose }: { id: number | null; onClose: () => void }) {
  const [entries, setEntries] = useState<Audit[]>([]);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);
  const [attempt, setAttempt] = useState(0);
  useEffect(() => {
    if (id === null) return;
    let alive = true;
    async function load() {
      setLoading(true); setError(""); setEntries([]);
      try { const data = await client.get<unknown, Audit[]>(`/api/admin/plans/${id}/audits`); if (alive) setEntries(data); }
      catch (e) { if (alive) setError(getApiErrorMessage(e, "Không tải được nhật ký.")); }
      finally { if (alive) setLoading(false); }
    }
    void load(); return () => { alive = false; };
  }, [id, attempt]);
  return <Dialog open={id !== null} onOpenChange={(open) => { if (!open) onClose(); }}><DialogContent className="max-h-[85dvh] overflow-y-auto sm:max-w-2xl"><DialogHeader><DialogTitle>Nhật ký thay đổi gói</DialogTitle><DialogDescription>200 thao tác gần nhất, bao gồm người thực hiện và dữ liệu trước/sau.</DialogDescription></DialogHeader>
    {loading && <p role="status">Đang tải...</p>}
    {error && <div role="alert"><p>{error}</p><Button variant="outline" onClick={() => setAttempt((v) => v + 1)}>Thử lại</Button></div>}
    {!loading && !error && entries.length === 0 && <p className="text-sm text-muted-foreground">Chưa có thao tác được ghi nhận.</p>}
    {entries.map((entry) => <details key={entry.id} className="rounded-lg border p-3"><summary className="cursor-pointer text-sm"><strong>{labels[entry.action] ?? entry.action}</strong> · Admin #{entry.actorId} · {new Date(entry.occurredAt).toLocaleString("vi-VN")}</summary><div className="mt-3 grid gap-3 text-xs"><p>Trước thay đổi</p><pre className="max-h-52 overflow-auto rounded bg-muted/40 p-3">{JSON.stringify(JSON.parse(entry.beforeJson), null, 2)}</pre><p>Sau thay đổi</p><pre className="max-h-52 overflow-auto rounded bg-muted/40 p-3">{JSON.stringify(JSON.parse(entry.afterJson), null, 2)}</pre></div></details>)}
  </DialogContent></Dialog>;
}

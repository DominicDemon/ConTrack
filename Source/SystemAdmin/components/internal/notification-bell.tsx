"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { Bell, CheckCheck, FileText, Headset, MessageSquare } from "lucide-react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Popover, PopoverContent, PopoverTrigger } from "@/components/ui/popover";
import { toast } from "@/components/ui/sonner";
import { adminNotificationApi, type AdminNotificationItem } from "@/services/internal-notification-api";
import { internalApi } from "@/services/internal-api";
import { openNotificationTarget } from "../../../Shared/notification-navigation";
import { cn } from "@/lib/utils";

const CHANNEL_NAME = "contrack-admin-notifications";
const POLL_MS = 4000;

type ContractItem = { id: string; title: string; body: string; eventCode: string; documentId: string; createdAt: string; readAt: string | null };
type ContractPage = { items: ContractItem[]; unreadCount: number };
type Kind = "contract" | "support";
/** One row of the merged bell: a support ticket notification or a contract notification. */
type Entry = { key: string; kind: Kind; title: string; body: string; at: number; unread: boolean; tag?: string; priority?: string; ticket?: AdminNotificationItem; contract?: ContractItem };

const utc = (s: string) => Date.parse(s + (/Z$|[+-]\d\d:\d\d$/.test(s) ? "" : "Z"));
const fromTicket = (t: AdminNotificationItem): Entry => ({ key: "t:" + t.notificationId, kind: "support", title: t.title, body: t.body, at: utc(t.createdAtUtc),
  unread: !t.readAtUtc, tag: t.department, priority: t.priority, ticket: t });
const fromContract = (c: ContractItem): Entry => ({ key: "c:" + c.id, kind: "contract", title: c.title, body: c.body, at: utc(c.createdAt), unread: !c.readAt, contract: c });
const priorities: Record<string, [string, string]> = {
  Urgent: ["Khẩn cấp", "bg-rose-50 text-rose-700 border-rose-200 dark:bg-rose-500/10 dark:text-rose-400"],
  High: ["Cao", "bg-amber-50 text-amber-700 border-amber-200 dark:bg-amber-500/10 dark:text-amber-400"],
  Low: ["Thấp", ""],
};

/**
 * The single bell of the internal workspace: support ticket notifications (when the user handles tickets) and
 * contract notifications (submissions, customer answers, approvals, ready to sign, expiry) in one list, newest first.
 * New items pop up as a toast; opening one marks it read and goes to the ticket or the contract.
 */
export function AdminNotificationBell({ tickets = true }: { tickets?: boolean }) {
  const router = useRouter();
  const [open, setOpen] = useState(false);
  const [entries, setEntries] = useState<Entry[]>([]);
  const [filter, setFilter] = useState<"all" | Kind>("all");
  const [loading, setLoading] = useState(true);
  const seen = useRef<Set<string> | null>(null);

  const broadcastSync = () => {
    if (typeof BroadcastChannel === "undefined") return;
    const ch = new BroadcastChannel(CHANNEL_NAME); ch.postMessage({ type: "sync" }); ch.close();
  };

  const go = useCallback(async (entry: Entry) => {
    setOpen(false);
    if (entry.unread) {
      setEntries(prev => prev.map(e => e.key === entry.key ? { ...e, unread: false } : e));
      try {
        if (entry.ticket) await adminNotificationApi.markRead(entry.ticket.notificationId);
        else if (entry.contract) await internalApi(`/contract-notifications/${entry.contract.id}/read`, "POST");
      } catch { /* the next poll restores the real state */ }
      broadcastSync();
    }
    if (entry.ticket) openNotificationTarget(`/workspace/support-tickets/${entry.ticket.subjectId}`, router.push);
    else if (entry.contract) {
      const id = encodeURIComponent(entry.contract.documentId);
      openNotificationTarget(entry.contract.eventCode === "SIGNED_PAYMENT_CONFIRMED"
        ? `/workspace/contracts/signed?documentId=${id}` : `/workspace/contracts/drafts/${id}`, router.push);
    }
  }, [router]);

  const load = useCallback(async (silent = false) => {
    if (!silent) setLoading(true);
    const [ticketResult, contractResult] = await Promise.allSettled([
      tickets ? adminNotificationApi.list("all", 20) : Promise.resolve({ items: [] as AdminNotificationItem[], unreadCount: 0 }),
      internalApi<ContractPage>("/contract-notifications?limit=20"),
    ]);
    // A source that failed keeps its previous rows instead of vanishing from the list.
    setEntries(prev => {
      const keep = (kind: Kind) => prev.filter(e => e.kind === kind);
      const next = [
        ...(ticketResult.status === "fulfilled" ? ticketResult.value.items.map(fromTicket) : keep("support")),
        ...(contractResult.status === "fulfilled" ? contractResult.value.items.map(fromContract) : keep("contract")),
      ].sort((a, b) => b.at - a.at);
      if (seen.current === null) seen.current = new Set(next.map(e => e.key));
      else {
        const fresh = next.filter(e => !seen.current!.has(e.key) && e.unread);
        next.forEach(e => seen.current!.add(e.key));
        if (fresh.length) {
          const latest = fresh[0];
          setTimeout(() => toast.info(latest.title, { description: latest.body, duration: 8000,
            action: { label: latest.kind === "contract" ? "Mở hợp đồng" : "Xử lý ngay", onClick: () => void go(latest) } }));
          const ticket = fresh.find(e => e.ticket)?.ticket;
          if (ticket && typeof window !== "undefined") window.dispatchEvent(new CustomEvent("admin-support-ticket-updated", { detail: ticket }));
        }
      }
      return next;
    });
    if (!silent) setLoading(false);
  }, [tickets, go]);

  useEffect(() => {
    void load(false);
    const interval = setInterval(() => { if (document.visibilityState === "visible") void load(true); }, POLL_MS);
    const onFocus = () => void load(true);
    window.addEventListener("focus", onFocus);
    const channel = typeof BroadcastChannel !== "undefined" ? new BroadcastChannel(CHANNEL_NAME) : null;
    if (channel) channel.onmessage = () => void load(true);
    return () => { clearInterval(interval); window.removeEventListener("focus", onFocus); channel?.close(); };
  }, [load]);

  const markAllRead = async () => {
    setEntries(prev => prev.map(e => ({ ...e, unread: false })));
    await Promise.allSettled([tickets ? adminNotificationApi.markAllRead() : Promise.resolve(), internalApi("/contract-notifications/read-all", "POST")]);
    broadcastSync();
    toast.success("Đã đánh dấu tất cả thông báo là đã đọc");
  };

  const unread = entries.filter(e => e.unread).length;
  const count = (kind: Kind) => entries.filter(e => e.kind === kind && e.unread).length;
  const shown = entries.filter(e => filter === "all" || e.kind === filter);
  const tabs: ["all" | Kind, string][] = tickets ? [["all", "Tất cả"], ["contract", "Hợp đồng"], ["support", "Hỗ trợ"]] : [];

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <button type="button" className="relative p-2 hover:bg-accent rounded-full transition-colors text-muted-foreground hover:text-foreground focus-visible:outline-none"
          aria-label={`Thông báo${unread ? `, ${unread} chưa đọc` : ""}`} title="Thông báo">
          <Bell className="w-5 h-5" />
          {unread > 0 && <span className="absolute -right-0.5 -top-0.5 flex h-5 min-w-5 items-center justify-center rounded-full bg-rose-600 px-1 text-[10px] font-bold text-white shadow-sm animate-pulse">
            {unread > 99 ? "99+" : unread}</span>}
        </button>
      </PopoverTrigger>

      <PopoverContent align="end" className="w-[min(26rem,calc(100vw-1rem))] p-0 rounded-xl shadow-lg">
        <div className="flex items-center justify-between border-b px-4 py-3 bg-muted/30">
          <div className="flex items-center gap-2">
            <Bell className="w-4 h-4 text-primary" /><span className="font-semibold text-sm">Thông báo</span>
            {unread > 0 && <Badge variant="destructive" className="h-5 px-1.5 text-[10px]">{unread} mới</Badge>}
          </div>
          {unread > 0 && <Button type="button" variant="ghost" size="sm" className="h-7 px-2 text-xs text-primary" onClick={() => void markAllRead()}>
            <CheckCheck className="mr-1 size-3.5" />Đánh dấu đã đọc</Button>}
        </div>
        {tabs.length > 0 && <div className="flex gap-1 border-b px-3 py-2" role="tablist" aria-label="Lọc thông báo">
          {tabs.map(([value, label]) => {
            const n = value === "all" ? unread : count(value);
            return <button key={value} type="button" role="tab" aria-selected={filter === value} onClick={() => setFilter(value)}
              className={cn("rounded-full px-3 py-1 text-xs font-medium transition-colors", filter === value ? "bg-primary text-primary-foreground" : "text-muted-foreground hover:bg-accent")}>
              {label}{n > 0 ? ` (${n})` : ""}</button>;
          })}
        </div>}

        <div className="max-h-[min(26rem,70vh)] overflow-y-auto divide-y">
          {loading && entries.length === 0 && <p className="p-6 text-center text-sm text-muted-foreground">Đang tải thông báo...</p>}
          {!loading && shown.length === 0 && <div className="p-8 text-center space-y-2">
            <Bell className="w-8 h-8 text-muted-foreground/50 mx-auto" /><p className="text-sm text-muted-foreground">Chưa có thông báo nào.</p></div>}
          {shown.map(entry => {
            const support = entry.kind === "support";
            const priority = entry.priority ? priorities[entry.priority] ?? ["Bình thường", "bg-primary/10 text-primary border-primary/20"] : null;
            return <button key={entry.key} type="button" onClick={() => void go(entry)}
              className={cn("flex w-full gap-3 px-4 py-3 text-left transition-colors hover:bg-accent/70", entry.unread && "bg-primary/[0.04]")}>
              <div className="mt-1 shrink-0">
                <div className={cn("flex size-8 items-center justify-center rounded-lg", support ? "bg-sky-500/10 text-sky-600" : "bg-primary/10 text-primary")}>
                  {support ? (entry.ticket?.actionKey === "new-ticket" ? <Headset className="size-4" /> : <MessageSquare className="size-4" />) : <FileText className="size-4" />}
                </div>
              </div>
              <div className="min-w-0 flex-1 space-y-1">
                <div className="flex items-start justify-between gap-2">
                  <span className={cn("text-xs leading-snug", entry.unread ? "font-bold text-foreground" : "font-medium text-foreground/85")}>{entry.title}</span>
                  {entry.unread && <span className="mt-1 size-2 shrink-0 rounded-full bg-rose-500" />}
                </div>
                <p className="text-xs text-muted-foreground line-clamp-2 leading-relaxed">{entry.body}</p>
                <div className="flex flex-wrap items-center gap-1.5 pt-0.5">
                  <span className="text-[10px] font-medium text-muted-foreground bg-muted px-1.5 py-0.5 rounded">{support ? "Hỗ trợ" : "Hợp đồng"}</span>
                  {priority && <Badge variant="outline" className={cn("text-[10px] px-1.5 py-0", priority[1])}>{priority[0]}</Badge>}
                  {entry.tag && <span className="text-[10px] font-medium text-muted-foreground bg-muted px-1.5 py-0.5 rounded">{entry.tag}</span>}
                  <span className="text-[10px] text-muted-foreground ml-auto">
                    {new Date(entry.at).toLocaleString("vi-VN", { day: "2-digit", month: "2-digit", hour: "2-digit", minute: "2-digit" })}</span>
                </div>
              </div>
            </button>;
          })}
        </div>

        {tickets && <Link href="/workspace/support-tickets" onClick={() => setOpen(false)}
          className="block border-t px-4 py-2.5 text-center text-xs font-semibold text-primary hover:bg-accent transition-colors">
          Xem tất cả Yêu cầu Hỗ trợ & Ticket</Link>}
      </PopoverContent>
    </Popover>
  );
}

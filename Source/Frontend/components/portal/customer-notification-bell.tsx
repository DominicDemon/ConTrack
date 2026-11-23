"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { Bell, CheckCheck, FileText, Headset, MessageSquare } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Popover, PopoverContent, PopoverTrigger } from "@/components/ui/popover";
import { toast } from "@/components/ui/sonner";
import { cn } from "@/lib/utils";
import { customerApi } from "@/services/customer-api";
import { openNotificationTarget } from "../../../Shared/notification-navigation";
import { customerNotificationApi, type CustomerNotificationItem } from "@/services/portal-notification-api";

const CHANNEL_NAME = "contrack-customer-notifications";
const POLL_MS = 5000;
/** Contract items have no table: the browser remembers which ones were read. */
const READ_STORE = "contrack-portal-read-notices";
/** On a browser's first visit, contract items older than this count as read so the bell does not open with the whole history. */
const FRESH_MS = 3 * 24 * 3600 * 1000;

/** A contract item (CustomerContractNotice): key is stable per event, reviewId opens the contract. */
type Notice = { key: string; code: string; title: string; body: string; reviewId: string; at: string };
type Kind = "contract" | "support";
type Entry = { key: string; kind: Kind; title: string; body: string; at: number; unread: boolean; tag?: string; ticket?: CustomerNotificationItem; notice?: Notice };

const utc = (s: string) => Date.parse(s + (/Z$|[+-]\d\d:\d\d$/.test(s) ? "" : "Z"));
// shortcut: contract read state lives in this browser (no table), so another device shows those items as new again.
function loadRead(): Set<string> | null {
  try { const raw = sessionStorage.getItem(READ_STORE); return raw ? new Set(JSON.parse(raw) as string[]) : null; } catch { return null; }
}
function saveRead(keys: Set<string>) {
  try { sessionStorage.setItem(READ_STORE, JSON.stringify([...keys].slice(-500))); } catch { /* private mode: kept in memory only */ }
}

/**
 * The single bell of the customer portal: support ticket answers and contract events (a new version to read, staff answers,
 * the company's decision, the company signed, the contract signed) in one list, newest first. Opening an item goes to it.
 */
export function CustomerNotificationBell() {
  const router = useRouter();
  const [open, setOpen] = useState(false);
  const [tickets, setTickets] = useState<CustomerNotificationItem[]>([]);
  const [notices, setNotices] = useState<Notice[]>([]);
  const [read, setRead] = useState<Set<string>>(new Set());
  const [filter, setFilter] = useState<"all" | Kind>("all");
  const [loading, setLoading] = useState(true);
  const seen = useRef<Set<string> | null>(null);
  const readRef = useRef<Set<string> | null>(null);

  const entries: Entry[] = [
    ...tickets.map((t): Entry => ({ key: "t:" + t.notificationId, kind: "support", title: t.title, body: t.body, at: utc(t.createdAtUtc), unread: !t.readAtUtc, tag: t.department, ticket: t })),
    ...notices.map((n): Entry => ({ key: "c:" + n.key, kind: "contract", title: n.title, body: n.body, at: utc(n.at), unread: !read.has(n.key), notice: n })),
  ].sort((a, b) => b.at - a.at);

  const broadcastSync = () => {
    if (typeof BroadcastChannel === "undefined") return;
    const ch = new BroadcastChannel(CHANNEL_NAME); ch.postMessage({ type: "sync" }); ch.close();
  };
  const markContractRead = useCallback((keys: string[]) => {
    const next = new Set(readRef.current ?? []); keys.forEach(k => next.add(k));
    readRef.current = next; setRead(next); saveRead(next);
  }, []);

  const go = useCallback(async (entry: Entry) => {
    setOpen(false);
    if (entry.notice) {
      markContractRead([entry.notice.key]);
      openNotificationTarget(`/portal/contracts/${encodeURIComponent(entry.notice.reviewId)}`, router.push);
      return;
    }
    const ticket = entry.ticket!;
    if (!ticket.readAtUtc) {
      const now = new Date().toISOString();
      setTickets(prev => prev.map(t => t.notificationId === ticket.notificationId ? { ...t, readAtUtc: now } : t));
      try { await customerNotificationApi.markRead(ticket.notificationId); } catch { /* next poll restores the real state */ }
      broadcastSync();
    }
    openNotificationTarget(`/portal/support/${ticket.subjectId}`, router.push);
  }, [markContractRead, router]);

  const load = useCallback(async (silent = false) => {
    if (!silent) setLoading(true);
    const [ticketResult, noticeResult] = await Promise.allSettled([customerNotificationApi.list("all", 20), customerApi<Notice[]>("/contract-notices")]);
    const nextTickets = ticketResult.status === "fulfilled" ? ticketResult.value.items : null;
    const nextNotices = noticeResult.status === "fulfilled" ? noticeResult.value : null;
    if (nextNotices && readRef.current === null) {
      const stored = loadRead();
      readRef.current = stored ?? new Set(nextNotices.filter(n => Date.now() - utc(n.at) > FRESH_MS).map(n => n.key));
      if (!stored) saveRead(readRef.current);
      setRead(readRef.current);
    }
    if (nextTickets) setTickets(nextTickets);
    if (nextNotices) setNotices(nextNotices);
    // New unread items since the last poll pop up as a toast (the first poll only records what is already there).
    const unreadNow: Entry[] = [
      ...(nextTickets ?? []).filter(t => !t.readAtUtc).map((t): Entry => ({ key: "t:" + t.notificationId, kind: "support", title: t.title, body: t.body, at: utc(t.createdAtUtc), unread: true, ticket: t })),
      ...(nextNotices ?? []).filter(n => !readRef.current?.has(n.key)).map((n): Entry => ({ key: "c:" + n.key, kind: "contract", title: n.title, body: n.body, at: utc(n.at), unread: true, notice: n })),
    ].sort((a, b) => b.at - a.at);
    const all = [...(nextTickets ?? []).map(t => "t:" + t.notificationId), ...(nextNotices ?? []).map(n => "c:" + n.key)];
    if (seen.current === null) seen.current = new Set(all);
    else {
      const fresh = unreadNow.filter(e => !seen.current!.has(e.key));
      all.forEach(k => seen.current!.add(k));
      if (fresh.length) {
        const latest = fresh[0];
        toast.info(latest.title, { description: latest.body, duration: 8000, action: { label: latest.ticket ? "Xem kết quả" : "Mở hợp đồng", onClick: () => void go(latest) } });
        const ticket = fresh.find(e => e.ticket)?.ticket;
        if (ticket && typeof window !== "undefined") window.dispatchEvent(new CustomEvent("customer-support-ticket-updated", { detail: ticket }));
      }
    }
    if (!silent) setLoading(false);
  }, [go]);

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
    const now = new Date().toISOString();
    setTickets(prev => prev.map(t => ({ ...t, readAtUtc: t.readAtUtc ?? now })));
    markContractRead(notices.map(n => n.key));
    try { await customerNotificationApi.markAllRead(); } catch { /* next poll restores the real state */ }
    broadcastSync();
    toast.success("Đã đánh dấu tất cả thông báo là đã đọc");
  };

  const unread = entries.filter(e => e.unread).length;
  const count = (kind: Kind) => entries.filter(e => e.kind === kind && e.unread).length;
  const shown = entries.filter(e => filter === "all" || e.kind === filter);

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

      <PopoverContent align="end" className="w-[min(25rem,calc(100vw-1rem))] p-0 rounded-xl shadow-lg">
        <div className="flex items-center justify-between border-b px-4 py-3 bg-muted/30">
          <div className="flex items-center gap-2">
            <Bell className="w-4 h-4 text-primary" /><span className="font-semibold text-sm">Thông báo</span>
            {unread > 0 && <Badge variant="destructive" className="h-5 px-1.5 text-[10px]">{unread} mới</Badge>}
          </div>
          {unread > 0 && <Button type="button" variant="ghost" size="sm" className="h-7 px-2 text-xs text-primary" onClick={() => void markAllRead()}>
            <CheckCheck className="mr-1 size-3.5" />Đánh dấu đã đọc</Button>}
        </div>
        <div className="flex gap-1 border-b px-3 py-2" role="tablist" aria-label="Lọc thông báo">
          {([["all", "Tất cả"], ["contract", "Hợp đồng"], ["support", "Hỗ trợ"]] as ["all" | Kind, string][]).map(([value, label]) => {
            const n = value === "all" ? unread : count(value);
            return <button key={value} type="button" role="tab" aria-selected={filter === value} onClick={() => setFilter(value)}
              className={cn("rounded-full px-3 py-1 text-xs font-medium transition-colors", filter === value ? "bg-primary text-primary-foreground" : "text-muted-foreground hover:bg-accent")}>
              {label}{n > 0 ? ` (${n})` : ""}</button>;
          })}
        </div>

        <div className="max-h-[min(26rem,70vh)] overflow-y-auto divide-y">
          {loading && entries.length === 0 && <p className="p-6 text-center text-sm text-muted-foreground">Đang tải thông báo...</p>}
          {!loading && shown.length === 0 && <div className="p-8 text-center space-y-2">
            <Bell className="w-8 h-8 text-muted-foreground/50 mx-auto" /><p className="text-sm text-muted-foreground">Chưa có thông báo nào.</p></div>}
          {shown.map(entry => {
            const support = entry.kind === "support";
            const resolved = entry.ticket?.actionKey === "ticket-resolved";
            return <button key={entry.key} type="button" onClick={() => void go(entry)}
              className={cn("flex w-full gap-3 px-4 py-3 text-left transition-colors hover:bg-accent/70", entry.unread && "bg-primary/[0.04]")}>
              <div className="mt-1 shrink-0">
                <div className={cn("flex size-8 items-center justify-center rounded-lg", !support ? "bg-primary/10 text-primary" : resolved ? "bg-emerald-500/10 text-emerald-600" : "bg-sky-500/10 text-sky-600")}>
                  {!support ? <FileText className="size-4" /> : resolved ? <Headset className="size-4" /> : <MessageSquare className="size-4" />}
                </div>
              </div>
              <div className="min-w-0 flex-1 space-y-1">
                <div className="flex items-start justify-between gap-2">
                  <span className={cn("text-xs leading-snug", entry.unread ? "font-bold text-foreground" : "font-medium text-foreground/85")}>{entry.title}</span>
                  {entry.unread && <span className="mt-1 size-2 shrink-0 rounded-full bg-rose-500" />}
                </div>
                <p className="text-xs text-muted-foreground line-clamp-2 leading-relaxed">{entry.body}</p>
                <div className="flex items-center gap-1.5 pt-0.5">
                  <span className="text-[10px] font-medium text-muted-foreground bg-muted px-1.5 py-0.5 rounded">{support ? entry.tag || "Hỗ trợ" : "Hợp đồng"}</span>
                  <span className="text-[10px] text-muted-foreground ml-auto">
                    {new Date(entry.at).toLocaleString("vi-VN", { day: "2-digit", month: "2-digit", hour: "2-digit", minute: "2-digit" })}</span>
                </div>
              </div>
            </button>;
          })}
        </div>

        <div className="grid grid-cols-2 border-t text-center text-xs font-semibold text-primary">
          <Link href="/portal/contracts" onClick={() => setOpen(false)} className="px-4 py-2.5 hover:bg-accent transition-colors">Hợp đồng của bạn</Link>
          <Link href="/portal/support" onClick={() => setOpen(false)} className="border-l px-4 py-2.5 hover:bg-accent transition-colors">Yêu cầu hỗ trợ</Link>
        </div>
      </PopoverContent>
    </Popover>
  );
}

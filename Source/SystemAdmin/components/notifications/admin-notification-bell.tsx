"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { Bell, CheckCheck, Headset, MessageSquare } from "lucide-react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from "@/components/ui/popover";
import { toast } from "@/components/ui/sonner";
import {
  adminNotificationApi,
  type AdminNotificationItem,
} from "@/services/admin-notification-api";
import { cn } from "@/lib/utils";

const CHANNEL_NAME = "contrack-admin-notifications";

export function AdminNotificationBell() {
  const router = useRouter();
  const [open, setOpen] = useState(false);
  const [items, setItems] = useState<AdminNotificationItem[]>([]);
  const [unreadCount, setUnreadCount] = useState(0);
  const [loading, setLoading] = useState(true);
  const seenIdsRef = useRef<Set<number> | null>(null);

  const fetchNotifications = useCallback(
    async (silent = false) => {
      if (!silent) setLoading(true);
      try {
        const data = await adminNotificationApi.list("all", 20);
        setItems(data.items);
        setUnreadCount(data.unreadCount);

        // Phát hiện thông báo mới từ khách hàng để hiển thị Toast ngay trên Web của người xử lý
        if (seenIdsRef.current === null) {
          seenIdsRef.current = new Set(data.items.map((i) => i.notificationId));
        } else {
          const newUnreadItems = data.items.filter(
            (i) => !seenIdsRef.current!.has(i.notificationId) && !i.readAtUtc,
          );
          for (const item of data.items) {
            seenIdsRef.current.add(item.notificationId);
          }

          if (newUnreadItems.length > 0) {
            const latest = newUnreadItems[0];
            toast.info(latest.title, {
              description: latest.body,
              duration: 8000,
              action: {
                label: "Xử lý ngay",
                onClick: () => {
                  void adminNotificationApi.markRead(latest.notificationId);
                  router.push(`/support-tickets/${latest.subjectId}`);
                },
              },
            });

            if (typeof window !== "undefined") {
              window.dispatchEvent(
                new CustomEvent("admin-support-ticket-updated", {
                  detail: latest,
                }),
              );
            }
          }
        }
      } catch {
        // Ignore transient polling errors
      } finally {
        if (!silent) setLoading(false);
      }
    },
    [router],
  );

  useEffect(() => {
    void fetchNotifications(false);

    const interval = setInterval(() => {
      void fetchNotifications(true);
    }, 4000);

    const onFocus = () => void fetchNotifications(true);
    window.addEventListener("focus", onFocus);

    const channel =
      typeof BroadcastChannel !== "undefined"
        ? new BroadcastChannel(CHANNEL_NAME)
        : null;
    if (channel) {
      channel.onmessage = () => void fetchNotifications(true);
    }

    return () => {
      clearInterval(interval);
      window.removeEventListener("focus", onFocus);
      channel?.close();
    };
  }, [fetchNotifications]);

  const broadcastSync = () => {
    if (typeof BroadcastChannel === "undefined") return;
    const ch = new BroadcastChannel(CHANNEL_NAME);
    ch.postMessage({ type: "sync" });
    ch.close();
  };

  const handleOpenNotification = async (item: AdminNotificationItem) => {
    setOpen(false);
    if (!item.readAtUtc) {
      const now = new Date().toISOString();
      setItems((prev) =>
        prev.map((it) =>
          it.notificationId === item.notificationId
            ? { ...it, readAtUtc: now }
            : it,
        ),
      );
      setUnreadCount((c) => Math.max(0, c - 1));
      await adminNotificationApi.markRead(item.notificationId);
      broadcastSync();
    }
    router.push(`/support-tickets/${item.subjectId}`);
  };

  const handleMarkAllRead = async () => {
    const now = new Date().toISOString();
    setItems((prev) =>
      prev.map((it) => ({ ...it, readAtUtc: it.readAtUtc ?? now })),
    );
    setUnreadCount(0);
    await adminNotificationApi.markAllRead();
    broadcastSync();
    toast.success("Đã đánh dấu tất cả thông báo hỗ trợ là đã đọc");
  };

  const getPriorityBadge = (priority: string) => {
    switch (priority) {
      case "Urgent":
        return (
          <Badge
            variant="outline"
            className="text-[10px] px-1.5 py-0 bg-rose-50 text-rose-700 border-rose-200 dark:bg-rose-500/10 dark:text-rose-400"
          >
            Khẩn cấp
          </Badge>
        );
      case "High":
        return (
          <Badge
            variant="outline"
            className="text-[10px] px-1.5 py-0 bg-amber-50 text-amber-700 border-amber-200 dark:bg-amber-500/10 dark:text-amber-400"
          >
            Cao
          </Badge>
        );
      case "Low":
        return (
          <Badge variant="outline" className="text-[10px] px-1.5 py-0">
            Thấp
          </Badge>
        );
      default:
        return (
          <Badge
            variant="outline"
            className="text-[10px] px-1.5 py-0 bg-primary/10 text-primary border-primary/20"
          >
            Bình thường
          </Badge>
        );
    }
  };

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <button
          type="button"
          className="relative p-2 hover:bg-accent rounded-full transition-colors text-muted-foreground hover:text-foreground focus-visible:outline-none"
          aria-label={`Thông báo hỗ trợ${unreadCount ? `, ${unreadCount} chưa đọc` : ""}`}
        >
          <Bell className="w-5 h-5" />
          {unreadCount > 0 && (
            <span className="absolute -right-0.5 -top-0.5 flex h-5 min-w-5 items-center justify-center rounded-full bg-rose-600 px-1 text-[10px] font-bold text-white shadow-sm animate-pulse">
              {unreadCount > 99 ? "99+" : unreadCount}
            </span>
          )}
        </button>
      </PopoverTrigger>

      <PopoverContent
        align="end"
        className="w-[min(25rem,calc(100vw-1rem))] p-0 rounded-xl shadow-lg"
      >
        <div className="flex items-center justify-between border-b px-4 py-3 bg-muted/30">
          <div className="flex items-center gap-2">
            <Headset className="w-4 h-4 text-primary" />
            <span className="font-semibold text-sm">Thông báo Hỗ trợ & Ticket</span>
            {unreadCount > 0 && (
              <Badge variant="destructive" className="h-5 px-1.5 text-[10px]">
                {unreadCount} mới
              </Badge>
            )}
          </div>
          {unreadCount > 0 && (
            <Button
              type="button"
              variant="ghost"
              size="sm"
              className="h-7 px-2 text-xs text-primary"
              onClick={() => void handleMarkAllRead()}
            >
              <CheckCheck className="mr-1 size-3.5" />
              Đánh dấu đã đọc
            </Button>
          )}
        </div>

        <div className="max-h-[min(26rem,70vh)] overflow-y-auto divide-y">
          {loading && items.length === 0 && (
            <p className="p-6 text-center text-sm text-muted-foreground">
              Đang tải thông báo hỗ trợ...
            </p>
          )}

          {!loading && items.length === 0 && (
            <div className="p-8 text-center space-y-2">
              <Headset className="w-8 h-8 text-muted-foreground/50 mx-auto" />
              <p className="text-sm text-muted-foreground">
                Chưa có thông báo yêu cầu hỗ trợ nào.
              </p>
            </div>
          )}

          {items.map((item) => {
            const isUnread = !item.readAtUtc;
            return (
              <button
                key={item.notificationId}
                type="button"
                onClick={() => void handleOpenNotification(item)}
                className={cn(
                  "flex w-full gap-3 px-4 py-3 text-left transition-colors hover:bg-accent/70",
                  isUnread && "bg-primary/[0.04]",
                )}
              >
                <div className="mt-1 shrink-0">
                  <div
                    className={cn(
                      "flex size-8 items-center justify-center rounded-lg",
                      item.actionKey === "new-ticket"
                        ? "bg-primary/10 text-primary"
                        : "bg-sky-500/10 text-sky-600",
                    )}
                  >
                    {item.actionKey === "new-ticket" ? (
                      <Headset className="size-4" />
                    ) : (
                      <MessageSquare className="size-4" />
                    )}
                  </div>
                </div>

                <div className="min-w-0 flex-1 space-y-1">
                  <div className="flex items-start justify-between gap-2">
                    <span
                      className={cn(
                        "text-xs leading-snug",
                        isUnread
                          ? "font-bold text-foreground"
                          : "font-medium text-foreground/85",
                      )}
                    >
                      {item.title}
                    </span>
                    {isUnread && (
                      <span className="mt-1 size-2 shrink-0 rounded-full bg-rose-500" />
                    )}
                  </div>

                  <p className="text-xs text-muted-foreground line-clamp-2 leading-relaxed">
                    {item.body}
                  </p>

                  <div className="flex flex-wrap items-center gap-1.5 pt-0.5">
                    {getPriorityBadge(item.priority)}
                    <span className="text-[10px] font-medium text-muted-foreground bg-muted px-1.5 py-0.5 rounded">
                      {item.department}
                    </span>
                    <span className="text-[10px] text-muted-foreground ml-auto">
                      {new Date(item.createdAtUtc).toLocaleString("vi-VN", {
                        day: "2-digit",
                        month: "2-digit",
                        hour: "2-digit",
                        minute: "2-digit",
                      })}
                    </span>
                  </div>
                </div>
              </button>
            );
          })}
        </div>

        <Link
          href="/support-tickets"
          onClick={() => setOpen(false)}
          className="block border-t px-4 py-2.5 text-center text-xs font-semibold text-primary hover:bg-accent transition-colors"
        >
          Xem tất cả Yêu cầu Hỗ trợ & Ticket
        </Link>
      </PopoverContent>
    </Popover>
  );
}


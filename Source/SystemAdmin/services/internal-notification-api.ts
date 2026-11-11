import { internalApi } from "./internal-api";
import type { AdminNotificationResponse } from "./admin-notification-api";

// Same names and types as main's admin-notification-api.ts, served by /api/internal/tickets/notifications with the internal session.
// Unlike main's version there is no sample fallback.
export type { AdminNotificationItem, AdminNotificationResponse } from "./admin-notification-api";

export const adminNotificationApi = {
  list: (status: "all" | "unread" | "read" = "all", limit = 20) =>
    internalApi<AdminNotificationResponse>(`/tickets/notifications?status=${status}&limit=${limit}`),
  markRead: (id: number) => internalApi<void>(`/tickets/notifications/${id}/read`, "PATCH"),
  markAllRead: () => internalApi<void>("/tickets/notifications/mark-all-read", "POST"),
};

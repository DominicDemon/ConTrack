import { internalApi } from "./internal-api";

export interface AdminNotificationItem {
  notificationId: number;
  title: string;
  body: string;
  subjectType: string;
  subjectId: number;
  ticketCode: string;
  tenantName: string;
  tenantCode: string;
  priority: string;
  department: string;
  actionKey: string;
  createdAtUtc: string;
  readAtUtc: string | null;
}

export interface AdminNotificationResponse {
  items: AdminNotificationItem[];
  unreadCount: number;
}

export const adminNotificationApi = {
  list: (status: "all" | "unread" | "read" = "all", limit = 20) =>
    internalApi<AdminNotificationResponse>(
      `/tickets/notifications?status=${status}&limit=${limit}`,
    ),
  markRead: (id: number) =>
    internalApi<void>(`/tickets/notifications/${id}/read`, "PATCH"),
  markAllRead: () =>
    internalApi<void>("/tickets/notifications/mark-all-read", "POST"),
};

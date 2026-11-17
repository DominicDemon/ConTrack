import { customerApi, handleAuthError } from "./customer-api";

export interface CustomerNotificationItem {
  notificationId: number;
  customerId: number;
  title: string;
  body: string;
  subjectType: string;
  subjectId: number;
  ticketCode: string;
  department: string;
  priority: string;
  actionKey: string;
  createdAtUtc: string;
  readAtUtc: string | null;
}

export interface CustomerNotificationResponse {
  items: CustomerNotificationItem[];
  unreadCount: number;
}

async function call<T>(path: string, method = "GET", body?: unknown): Promise<T> {
  try {
    return await customerApi<T>(path, method, body);
  } catch (e) {
    if (typeof window !== "undefined") {
      handleAuthError(e, (url) => window.location.replace(url));
    }
    throw e;
  }
}

export const customerNotificationApi = {
  list: (status: "all" | "unread" | "read" = "all", limit = 20) =>
    call<CustomerNotificationResponse>(`/notifications?status=${status}&limit=${limit}`),
  markRead: (id: number) =>
    call<void>(`/notifications/${id}/read`, "PATCH"),
  markAllRead: () =>
    call<{ changed: number }>("/notifications/mark-all-read", "POST"),
};


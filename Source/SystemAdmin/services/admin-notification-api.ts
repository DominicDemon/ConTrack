import axiosClient from "@/lib/axios-interceptor";

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

const FALLBACK_NOTIFICATIONS: AdminNotificationItem[] = [
  {
    notificationId: 1,
    title: "Yêu cầu hỗ trợ mới [TK-118823C445]",
    body: "Công ty TNHH Dịch vụ Du lịch Biển Xanh (BIENXANH) gửi yêu cầu [Thấp] tới KẾ TOÁN - HÀNH CHÍNH: Yêu cầu xuất hóa đơn VAT điện tử tháng 10/2026",
    subjectType: "SupportTicket",
    subjectId: 4,
    ticketCode: "TK-118823C445",
    tenantName: "Công ty TNHH Dịch vụ Du lịch Biển Xanh",
    tenantCode: "BIENXANH",
    priority: "Low",
    department: "KẾ TOÁN - HÀNH CHÍNH",
    actionKey: "new-ticket",
    createdAtUtc: new Date(Date.now() - 2 * 3600 * 1000).toISOString(),
    readAtUtc: null,
  },
];

export const adminNotificationApi = {
  list: async (status: "all" | "unread" | "read" = "all", limit = 20): Promise<AdminNotificationResponse> => {
    try {
      const res = await axiosClient.get<unknown, AdminNotificationResponse>("/api/admin/notifications", {
        params: { status, limit },
      });
      if (res && Array.isArray(res.items)) {
        return res;
      }
    } catch {
      // Fallback in-memory
    }

    const unreadCount = FALLBACK_NOTIFICATIONS.filter((n) => !n.readAtUtc).length;
    const items =
      status === "unread"
        ? FALLBACK_NOTIFICATIONS.filter((n) => !n.readAtUtc)
        : status === "read"
          ? FALLBACK_NOTIFICATIONS.filter((n) => !!n.readAtUtc)
          : [...FALLBACK_NOTIFICATIONS];
    return { items, unreadCount };
  },

  markRead: async (id: number): Promise<void> => {
    try {
      await axiosClient.patch(`/api/admin/notifications/${id}/read`, {});
    } catch {
      // Fallback
    }
    const found = FALLBACK_NOTIFICATIONS.find((n) => n.notificationId === id);
    if (found && !found.readAtUtc) {
      found.readAtUtc = new Date().toISOString();
    }
  },

  markAllRead: async (): Promise<void> => {
    try {
      await axiosClient.post("/api/admin/notifications/mark-all-read", {});
    } catch {
      // Fallback
    }
    const now = new Date().toISOString();
    FALLBACK_NOTIFICATIONS.forEach((n) => {
      if (!n.readAtUtc) n.readAtUtc = now;
    });
  },
};


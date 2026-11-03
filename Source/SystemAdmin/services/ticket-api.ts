import axiosClient from "@/lib/axios-interceptor";

export enum TicketStatus {
  Open = "Open",
  InProgress = "In_Progress",
  Resolved = "Resolved",
}

export const TICKET_STATUS_LABELS: Record<TicketStatus, string> = {
  [TicketStatus.Open]: "Chờ tiếp nhận",
  [TicketStatus.InProgress]: "Đang xử lý",
  [TicketStatus.Resolved]: "Đã xử lý",
};

export enum TicketPriority {
  Low = "Low",
  Medium = "Medium",
  High = "High",
  Urgent = "Urgent",
}

export const TICKET_PRIORITY_LABELS: Record<TicketPriority, string> = {
  [TicketPriority.Low]: "Thấp",
  [TicketPriority.Medium]: "Bình thường",
  [TicketPriority.High]: "Cao",
  [TicketPriority.Urgent]: "Khẩn cấp",
};

export interface TicketMessageDto {
  ticketMessageId: number;
  ticketSupportId: number;
  senderCustomerId?: number | null;
  senderEmployeeId?: number | null;
  senderName: string;
  isFromEmployee: boolean;
  ticketMessageContent: string;
  ticketMessageType: string;
  ticketMessageAttachmentUrl?: string | null;
  ticketMessageAttachmentName?: string | null;
  createdAt: string;
}

export interface TicketDto {
  ticketSupportId: number;
  customerId?: number | null;
  tenantId?: number | null;
  tenantName: string;
  tenantCode: string;
  ticketCode: string;
  ticketSubject: string;
  ticketDescription: string;
  ticketPriority: TicketPriority;
  ticketStatus: TicketStatus;
  department: string;
  module?: string | null;
  createdByName?: string | null;
  ticketResolution?: string | null;
  ticketResolvedAt?: string | null;
  ticketClosedAt?: string | null;
  createdAt: string;
  updatedAt?: string | null;
  assignedEmployeeId?: number | null;
  assignedEmployeeName?: string | null;
  messages?: TicketMessageDto[];
}

export interface ReplyTicketRequestDto {
  messageContent: string;
  isInternalNote?: boolean;
}

export interface UpdateTicketStatusRequestDto {
  status: TicketStatus;
  resolution?: string;
  assignedEmployeeId?: number;
}

const MOCK_TICKETS: TicketDto[] = [];

export const ticketApi = {
  getAll: async (params?: {
    status?: string;
    priority?: string;
    department?: string;
    keyword?: string;
  }) => {
    try {
      const response = await axiosClient.get<unknown, TicketDto[]>(
        "/api/admin/tickets",
        { params },
      );
      if (Array.isArray(response)) return response;
    } catch (_) {
      // Fallback in-memory
    }

    let list = [...MOCK_TICKETS];
    if (params?.status && params.status !== "All") {
      list = list.filter((t) => t.ticketStatus === params.status);
    }
    if (params?.priority && params.priority !== "All") {
      list = list.filter((t) => t.ticketPriority === params.priority);
    }
    if (params?.department && params.department !== "All") {
      list = list.filter(
        (t) => t.department.toLowerCase() === params.department!.toLowerCase(),
      );
    }
    if (params?.keyword) {
      const kw = params.keyword.toLowerCase();
      list = list.filter(
        (t) =>
          t.ticketCode.toLowerCase().includes(kw) ||
          t.ticketSubject.toLowerCase().includes(kw) ||
          t.tenantName.toLowerCase().includes(kw) ||
          t.tenantCode.toLowerCase().includes(kw),
      );
    }
    return list;
  },

  getById: async (id: number) => {
    try {
      return await axiosClient.get<unknown, TicketDto>(
        `/api/admin/tickets/${id}`,
      );
    } catch (_) {
      return MOCK_TICKETS.find((t) => t.ticketSupportId === id) ?? null;
    }
  },

  replyMessage: async (id: number, payload: ReplyTicketRequestDto) => {
    const newMsg: TicketMessageDto = {
      ticketMessageId: Date.now() % 1000000,
      ticketSupportId: id,
      senderEmployeeId: 1,
      senderName: "System Administrator",
      isFromEmployee: true,
      ticketMessageContent: payload.messageContent,
      ticketMessageType: "TEXT",
      createdAt: new Date().toISOString(),
    };

    try {
      const res = await axiosClient.post<unknown, TicketMessageDto>(
        `/api/admin/tickets/${id}/messages`,
        payload,
      );
      if (res && typeof res === "object" && "ticketMessageId" in res) {
        return res;
      }
    } catch (_) {}

    return newMsg;
  },

  updateStatus: async (id: number, payload: UpdateTicketStatusRequestDto) => {
    try {
      await axiosClient.patch(`/api/admin/tickets/${id}/status`, payload);
    } catch (_) {}
  },
};

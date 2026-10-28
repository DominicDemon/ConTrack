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
  createdByName?: string | null;
  ticketCode: string;
  ticketSubject: string;
  ticketDescription: string;
  ticketPriority: TicketPriority;
  ticketStatus: TicketStatus;
  department: string;
  module?: string | null;
  ticketResolution?: string | null;
  ticketResolvedAt?: string | null;
  ticketClosedAt?: string | null;
  createdAt: string;
  updatedAt?: string | null;
  assignedEmployeeId?: number | null;
  assignedEmployeeName?: string | null;
  messages?: TicketMessageDto[];
}

export interface CreateSupportTicketRequestDto {
  subject: string;
  description: string;
  priority: TicketPriority;
  department: string;
  module?: string;
  attachmentNames?: string[];
}

export interface CustomerReplyTicketRequestDto {
  messageContent: string;
  attachmentName?: string;
}

const MOCK_CUSTOMER_TICKETS: TicketDto[] = [];

export const customerTicketApi = {
  getAll: async (params?: { status?: string; priority?: string; keyword?: string }) => {
    try {
      const response = await axiosClient.get<unknown, TicketDto[]>("/api/support/tickets", { params });
      if (Array.isArray(response)) return response;
    } catch {
      // Fallback in-memory
    }

    let list = [...MOCK_CUSTOMER_TICKETS];
    if (params?.status && params.status !== "All") {
      list = list.filter((t) => t.ticketStatus === params.status);
    }
    if (params?.priority && params.priority !== "All") {
      list = list.filter((t) => t.ticketPriority === params.priority);
    }
    if (params?.keyword) {
      const kw = params.keyword.toLowerCase();
      list = list.filter(
        (t) =>
          t.ticketCode.toLowerCase().includes(kw) ||
          t.ticketSubject.toLowerCase().includes(kw) ||
          t.ticketDescription.toLowerCase().includes(kw),
      );
    }
    return list;
  },

  getById: async (id: number) => {
    try {
      return await axiosClient.get<unknown, TicketDto>(`/api/support/tickets/${id}`);
    } catch {
      return MOCK_CUSTOMER_TICKETS.find((t) => t.ticketSupportId === id) ?? null;
    }
  },

  create: async (payload: CreateSupportTicketRequestDto) => {
    const response = await axiosClient.post<unknown, TicketDto>("/api/support/tickets", {
      ...payload,
      ticketSubject: payload.subject,
      ticketDescription: payload.description,
      ticketPriority: payload.priority,
    });
    return response;
  },

  sendMessage: async (id: number, payload: CustomerReplyTicketRequestDto) => {
    const response = await axiosClient.post<unknown, TicketMessageDto>(
      `/api/support/tickets/${id}/messages`,
      payload,
    );
    return response;
  },
};


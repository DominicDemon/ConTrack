import axiosClient from "@/lib/axios-interceptor";

export enum TicketStatus {
  Open = "Open",
  InProgress = "In_Progress",
  PendingHeadApproval = "PendingHeadApproval",
  Resolved = "Resolved",
}

export const TICKET_STATUS_LABELS: Record<string, string> = {
  [TicketStatus.Open]: "Chờ Trưởng phòng tiếp nhận",
  [TicketStatus.InProgress]: "Đang xử lý (đã phân công)",
  [TicketStatus.PendingHeadApproval]: "Chờ Trưởng phòng duyệt kết quả",
  [TicketStatus.Resolved]: "Đã xử lý xong",
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
  attachmentUrl?: string | null;
  attachmentName?: string | null;
  attachmentUrls?: string[];
  attachmentNames?: string[];
  createdAt: string;
}

export interface TicketAssignmentDto {
  ticketSupportEmployeeId: number;
  ticketSupportId: number;
  employeeId: number;
  employeeName: string;
  employeeCode?: string | null;
  employeeEmail?: string | null;
  assignmentRole: string; // "DEPARTMENT_HEAD", "ASSIGNEE"
  assignedAt?: string | null;
  unassignedAt?: string | null;
  assignedByEmployeeId?: number | null;
  assignedByName?: string | null;
}

export interface DepartmentStaffDto {
  employeeId: number;
  employeeName: string;
  employeeCode?: string | null;
  employeeEmail?: string | null;
  departmentId?: number | null;
  departmentName?: string | null;
  isManager: boolean;
}

export interface SupportServiceItemDto {
  serviceId: number;
  serviceCode?: string | null;
  serviceName: string;
  description?: string | null;
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
  departmentId?: number | null;
  department: string;
  serviceId?: number | null;
  serviceName?: string | null;
  module?: string | null;
  ticketResolution?: string | null;
  ticketResolvedAt?: string | null;
  ticketClosedAt?: string | null;
  createdAt: string;
  updatedAt?: string | null;
  departmentHeadEmployeeId?: number | null;
  departmentHeadName?: string | null;
  assignedEmployeeId?: number | null;
  assignedEmployeeName?: string | null;
  assignedAt?: string | null;
  viewerRole?: "DEPARTMENT_HEAD" | "STAFF" | "CUSTOMER" | "SYSADMIN" | string | null;
  viewerEmployeeId?: number | null;
  viewerDepartmentId?: number | null;
  attachmentUrl?: string | null;
  attachmentName?: string | null;
  attachmentUrls?: string[];
  attachmentNames?: string[];
  assignments?: TicketAssignmentDto[];
  messages?: TicketMessageDto[];
}

export interface CreateSupportTicketRequestDto {
  subject: string;
  description: string;
  priority?: TicketPriority;
  departmentId?: number;
  department: string;
  serviceId?: number | null;
  serviceName?: string;
  module?: string;
  attachmentNames?: string[];
  attachmentUrls?: string[];
}

export interface CustomerReplyTicketRequestDto {
  messageContent: string;
  attachmentName?: string;
  attachmentUrl?: string;
  attachmentNames?: string[];
  attachmentUrls?: string[];
}

const MOCK_CUSTOMER_TICKETS: TicketDto[] = [];

export const customerTicketApi = {
  getServices: async (): Promise<SupportServiceItemDto[]> => {
    try {
      const response = await axiosClient.get<unknown, SupportServiceItemDto[]>(
        "/api/support/tickets/services"
      );
      if (Array.isArray(response) && response.length > 0) return response;
    } catch {
      // Fallback
    }
    return [
      { serviceId: 1, serviceCode: "SVC-CLD-01", serviceName: "Dịch vụ Lưu trữ Đám mây (Cloud Server / VPS)" },
      { serviceId: 2, serviceCode: "SVC-CON-02", serviceName: "Dịch vụ Quản lý Hợp đồng Điện tử (ConTrack Enterprise)" },
      { serviceId: 3, serviceCode: "SVC-CA-03", serviceName: "Dịch vụ Chữ ký số & Xác thực Điện tử (eSign / USB Token)" },
      { serviceId: 4, serviceCode: "SVC-INV-04", serviceName: "Dịch vụ Hóa đơn Điện tử & Thanh toán" },
      { serviceId: 5, serviceCode: "SVC-SUP-05", serviceName: "Dịch vụ Bảo trì & Hỗ trợ Kỹ thuật Hệ thống" },
    ];
  },

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

  getDepartmentStaff: async (ticketId: number) => {
    return await axiosClient.get<unknown, DepartmentStaffDto[]>(
      `/api/support/tickets/${ticketId}/staff`
    );
  },

  assignToStaff: async (ticketId: number, payload: { staffEmployeeId: number; note?: string }) => {
    return await axiosClient.post<unknown, TicketDto>(
      `/api/support/tickets/${ticketId}/assign`,
      payload
    );
  },

  staffComplete: async (ticketId: number, payload: { resolution: string; note?: string }) => {
    return await axiosClient.post<unknown, TicketDto>(
      `/api/support/tickets/${ticketId}/staff-complete`,
      payload
    );
  },

  headResolve: async (
    ticketId: number,
    payload: { resolution: string; messageToCustomer?: string; closeTicket?: boolean }
  ) => {
    return await axiosClient.post<unknown, TicketDto>(
      `/api/support/tickets/${ticketId}/resolve`,
      payload
    );
  },
};

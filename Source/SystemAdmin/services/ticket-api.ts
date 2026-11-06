import axiosClient from "@/lib/axios-interceptor";

export enum TicketStatus {
  Open = "Open",
  InProgress = "In_Progress",
  PendingHeadApproval = "PendingHeadApproval",
  Resolved = "Resolved",
}

export const TICKET_STATUS_LABELS: Record<TicketStatus, string> = {
  [TicketStatus.Open]: "Chờ Trưởng phòng tiếp nhận",
  [TicketStatus.InProgress]: "Đang xử lý (Đã giao nhân viên)",
  [TicketStatus.PendingHeadApproval]: "Chờ Trưởng phòng duyệt kết quả",
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

export interface TicketEmployeeAssignmentDto {
  ticketSupportEmployeeId: number;
  ticketSupportId: number;
  employeeId: number;
  employeeName: string;
  employeeEmail?: string | null;
  assignmentRole: string;
  assignedAt: string;
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
  departmentId?: number | null;
  department: string;
  module?: string | null;
  createdByName?: string | null;
  ticketResolution?: string | null;
  ticketResolvedAt?: string | null;
  ticketClosedAt?: string | null;
  createdAt: string;
  updatedAt?: string | null;
  departmentHeadEmployeeId?: number | null;
  departmentHeadName?: string | null;
  assignedEmployeeId?: number | null;
  assignedEmployeeName?: string | null;
  viewerRole?: "DEPARTMENT_HEAD" | "STAFF" | "CUSTOMER" | "SYSADMIN" | string | null;
  viewerEmployeeId?: number | null;
  viewerDepartmentId?: number | null;
  assignments?: TicketEmployeeAssignmentDto[];
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

  getDepartmentStaff: async (ticketId: number): Promise<DepartmentStaffDto[]> => {
    try {
      const res = await axiosClient.get<unknown, DepartmentStaffDto[]>(
        `/api/admin/tickets/${ticketId}/staff`,
      );
      if (Array.isArray(res)) return res;
    } catch (_) {}
    return [];
  },

  assignToStaff: async (
    ticketId: number,
    payload: { staffEmployeeId: number; note?: string },
  ) => {
    return await axiosClient.post<unknown, TicketDto>(
      `/api/admin/tickets/${ticketId}/assign`,
      payload,
    );
  },

  staffComplete: async (
    ticketId: number,
    payload: { resolution: string },
  ) => {
    return await axiosClient.post<unknown, TicketDto>(
      `/api/admin/tickets/${ticketId}/staff-complete`,
      payload,
    );
  },

  headResolve: async (
    ticketId: number,
    payload: {
      resolution: string;
      messageToCustomer?: string;
      closeTicket?: boolean;
    },
  ) => {
    return await axiosClient.post<unknown, TicketDto>(
      `/api/admin/tickets/${ticketId}/resolve`,
      payload,
    );
  },
};

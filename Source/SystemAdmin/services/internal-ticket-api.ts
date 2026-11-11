import { internalApi } from "./internal-api";
import type { DepartmentStaffDto, ReplyTicketRequestDto, TicketDto, TicketMessageDto, TicketPriority, UpdateTicketStatusRequestDto } from "./ticket-api";

// Same names and types as main's ticket-api.ts, served by /api/internal/tickets with the internal session.
// Unlike main's version there is no mock fallback: a failed request is shown to the user instead of sample data.
export * from "./ticket-api";

const query = (params?: Record<string, string | undefined>) => {
  const search = new URLSearchParams(Object.entries(params ?? {}).filter((entry): entry is [string, string] => !!entry[1]));
  return search.size ? `?${search}` : "";
};

export const ticketApi = {
  getAll: (params?: { status?: string; priority?: string; department?: string; keyword?: string }) => internalApi<TicketDto[]>(`/tickets${query(params)}`),
  getById: (id: number) => internalApi<TicketDto>(`/tickets/${id}`),
  replyMessage: (id: number, payload: ReplyTicketRequestDto) => internalApi<TicketMessageDto>(`/tickets/${id}/messages`, "POST", payload),
  updateStatus: async (id: number, payload: UpdateTicketStatusRequestDto) => { await internalApi<TicketDto>(`/tickets/${id}/status`, "PATCH", payload); },
  getDepartmentStaff: (ticketId: number) => internalApi<DepartmentStaffDto[]>(`/tickets/${ticketId}/staff`),
  assignToStaff: (ticketId: number, payload: { staffEmployeeId: number; priority?: TicketPriority | string; note?: string }) =>
    internalApi<TicketDto>(`/tickets/${ticketId}/assign`, "POST", payload),
  staffComplete: (ticketId: number, payload: { resolution: string }) => internalApi<TicketDto>(`/tickets/${ticketId}/staff-complete`, "POST", payload),
  headResolve: (ticketId: number, payload: { resolution: string; messageToCustomer?: string; closeTicket?: boolean }) =>
    internalApi<TicketDto>(`/tickets/${ticketId}/resolve`, "POST", payload),
};

import { customerApi, handleAuthError } from "./customer-api";
import type { CreateSupportTicketRequestDto, CustomerReplyTicketRequestDto, SupportServiceItemDto, TicketDto, TicketMessageDto } from "./ticket-api";

// Same names and types as main's ticket-api.ts, served by /api/customer/support with the portal session (the customer is CUSTOMER.customer_id).
// Unlike main's version there is no mock fallback; a signed-out customer is sent to the portal login.
export * from "./ticket-api";

async function call<T>(path: string, method = "GET", body?: unknown): Promise<T> {
  try { return await customerApi<T>(path, method, body); }
  catch (e) { if (typeof window !== "undefined") handleAuthError(e, url => window.location.replace(url)); throw e; }
}

const query = (params?: Record<string, string | undefined>) => {
  const search = new URLSearchParams(Object.entries(params ?? {}).filter((entry): entry is [string, string] => !!entry[1]));
  return search.size ? `?${search}` : "";
};

export const customerTicketApi = {
  getServices: () => call<SupportServiceItemDto[]>("/support/services"),
  getAll: (params?: { status?: string; priority?: string; keyword?: string }) => call<TicketDto[]>(`/support/tickets${query(params)}`),
  getById: (id: number) => call<TicketDto>(`/support/tickets/${id}`),
  create: (payload: CreateSupportTicketRequestDto) => call<TicketDto>("/support/tickets", "POST", {
    ...payload, ticketSubject: payload.subject, ticketDescription: payload.description, ticketPriority: payload.priority,
  }),
  sendMessage: (id: number, payload: CustomerReplyTicketRequestDto) => call<TicketMessageDto>(`/support/tickets/${id}/messages`, "POST", payload),
};

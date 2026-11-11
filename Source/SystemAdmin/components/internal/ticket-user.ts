"use client";
import { useInternalUser } from "./internal-shell";

/** The fields main's ticket pages read from the legacy auth store, taken from the internal session (department + position). */
export function useTicketUser() {
  const user = useInternalUser();
  return {
    username: user.username,
    departmentName: user.department ?? undefined,
    // Department managers hold ticket.manage.all; Ban Giám Đốc has no ticket permission and never reaches these pages.
    supportRole: user.permissions.includes("ticket.manage.all") ? "DEPARTMENT_HEAD" : "STAFF",
  };
}

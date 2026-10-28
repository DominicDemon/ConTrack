import client from "@/lib/axios-interceptor";
import type { ServicePlan } from "@/lib/plan-management";
export const plansApi = {
  list: () => client.get<unknown, ServicePlan[]>("/api/admin/plans"),
  create: (body: unknown) => client.post("/api/admin/plans", body),
  update: (id: number, body: unknown) => client.put(`/api/admin/plans/${id}`, body),
  price: (id: number, price: number, revision: number) => client.post(`/api/admin/plans/${id}/prices`, { price, revision }),
  status: (id: number, active: boolean, revision: number) => client.patch(`/api/admin/plans/${id}/status`, { active, revision }),
};

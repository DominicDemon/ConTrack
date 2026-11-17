import axiosClient from "@/lib/axios-interceptor";
import type { SigningMonitorDetail, SigningMonitorEntry } from "@/services/internal-api";

const BASE = "/api/admin/contracts/signing-monitor";

export const contractSigningMonitorApi = {
  list: (search?: string) => axiosClient.get<unknown, SigningMonitorEntry[]>(BASE, { params: search ? { search } : undefined }),
  detail: (id: string) => axiosClient.get<unknown, SigningMonitorDetail>(`${BASE}/${encodeURIComponent(id)}`),
};

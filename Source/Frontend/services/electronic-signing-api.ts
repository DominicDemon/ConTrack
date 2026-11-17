import axiosClient from "@/lib/axios-interceptor";

export interface DirectorSigningQueueItem {
  sessionId: number;
  contractId: number;
  versionId: number;
  contractCode?: string | null;
  contractName: string;
  customerName: string;
  submittedAt: string;
}

export interface StartElectronicSigningRequest {
  contractId: number;
  versionId: number;
  directorName: string;
  directorPhone: string;
  customerName: string;
  customerPhone: string;
  linkLifetimeHours: number;
  directorPlacement: { pageNumber: number; x: number; y: number; width: number; height: number };
  customerPlacement: { pageNumber: number; x: number; y: number; width: number; height: number };
}

export interface DirectorSigningResult {
  sessionId: number;
  status: number;
  customerSigningLink: string;
  linkExpiresAt: string;
}

export interface ElectronicSigningSessionStatus {
  sessionId: number;
  contractId: number;
  versionId: number;
  status: number;
  customerSigningLink?: string | null;
  linkExpiresAt?: string | null;
  rejectionReason?: string | null;
}

const BASE = "/api/contracts/electronic-signing";

export const electronicSigningApi = {
  start: (request: StartElectronicSigningRequest) =>
    axiosClient.post<unknown, { sessionId: number }>(BASE, request),
  getStatus: (contractId: number, versionId: number) =>
    axiosClient.get<unknown, ElectronicSigningSessionStatus | null>(
      `${BASE}/contracts/${contractId}/versions/${versionId}/status`,
    ),
  getFinalPdf: (contractId: number, versionId: number) =>
    axiosClient.get<unknown, Blob>(
      `${BASE}/contracts/${contractId}/versions/${versionId}/final-document`,
      { responseType: "blob" },
    ),
  getDirectorQueue: () =>
    axiosClient.get<unknown, DirectorSigningQueueItem[]>(`${BASE}/director/queue`),
  getDirectorPdf: (sessionId: number) =>
    axiosClient.get<unknown, Blob>(`${BASE}/${sessionId}/director/document`, { responseType: "blob" }),
  signAsDirector: (sessionId: number, signaturePngBase64: string) =>
    axiosClient.post<unknown, DirectorSigningResult>(
      `${BASE}/${sessionId}/director/sign`,
      { signaturePngBase64, confirmed: true },
    ),
  rejectAsDirector: (sessionId: number, reason: string) =>
    axiosClient.post(`${BASE}/${sessionId}/director/reject`, { reason }),
};

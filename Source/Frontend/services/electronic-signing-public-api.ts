import { publicAxiosClient } from "@/lib/axios-interceptor";

export interface SigningOtpResponse {
  challengeId: string;
  expiresAt: string;
  deliveryChannel: string;
}

export const electronicSigningPublicApi = {
  requestOtp: (tenantCode: string, token: string) =>
    publicAxiosClient.post<unknown, SigningOtpResponse>(
      `/public/electronic-signing/${encodeURIComponent(tenantCode)}/${encodeURIComponent(token)}/otp/request`,
    ),
  verifyOtp: (tenantCode: string, token: string, challengeId: string, otp: string) =>
    publicAxiosClient.post(
      `/public/electronic-signing/${encodeURIComponent(tenantCode)}/${encodeURIComponent(token)}/otp/verify`,
      { role: 2, challengeId, otp },
    ),
  getPdf: (tenantCode: string, token: string, challengeId: string) =>
    publicAxiosClient.get<unknown, Blob>(
      `/public/electronic-signing/${encodeURIComponent(tenantCode)}/${encodeURIComponent(token)}/document`,
      { params: { challengeId }, responseType: "blob" },
    ),
  sign: (
    tenantCode: string,
    token: string,
    request: { challengeId: string; signaturePngBase64: string; confirmed: boolean },
  ) =>
    publicAxiosClient.post<unknown, Blob>(
      `/public/electronic-signing/${encodeURIComponent(tenantCode)}/${encodeURIComponent(token)}/sign`,
      request,
      { responseType: "blob" },
    ),
};

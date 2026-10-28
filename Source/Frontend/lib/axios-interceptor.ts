import axios, { AxiosError, AxiosInstance, AxiosResponse } from "axios";

import { useAuthStore } from "@/hooks/use-auth-store";
import { currentAuthHref } from "@/lib/auth-return";

const BASE_URL = process.env.NEXT_PUBLIC_API_URL;

const axiosClient: AxiosInstance = axios.create({
  baseURL: BASE_URL,
  headers: {
    "Content-Type": "application/json",
  },
  withCredentials: true,
});

export const publicAxiosClient: AxiosInstance = axios.create({
  baseURL: BASE_URL,
  headers: {
    "Content-Type": "application/json",
  },
  withCredentials: true,
});

const unwrapApiResponse = (response: AxiosResponse) => {
  const res = response.data;
  if (res && typeof res === "object" && "success" in res && "data" in res) {
    return res.data;
  }
  return res;
};

const getEmployeeLoginError = (error: AxiosError) => {
  const data = error.response?.data;
  if (data && typeof data === "object" && "code" in data) {
    const code = (data as { code?: unknown }).code;
    if (code === "EmployeeInactive") return "employee_inactive";
  }
  return "session_expired";
};

const getResponseErrorCode = (error: AxiosError) => {
  const data = error.response?.data;
  if (data && typeof data === "object" && "code" in data) {
    return typeof data.code === "string" ? data.code : undefined;
  }
  return undefined;
};

let redirecting = false;
function redirectOnce(href: string) {
  if (redirecting) return;
  redirecting = true;
  window.location.replace(href);
}

axiosClient.interceptors.response.use(
  unwrapApiResponse,

  (error: AxiosError) => {
    if (
      error.response?.status === 403 &&
      getResponseErrorCode(error) === "MustChangePassword" &&
      typeof window !== "undefined" &&
      window.location.pathname !== "/change-password"
    ) {
      redirectOnce(currentAuthHref("/change-password", { required: "1" }, useAuthStore.getState().user?.tenantCode));
    }
    if (error.response?.status === 401) {
      if (typeof window !== "undefined") {
        if (window.location.pathname !== "/") {
          redirectOnce(currentAuthHref("/", { error: getEmployeeLoginError(error) }, useAuthStore.getState().user?.tenantCode));
        }
      }
    }

    return Promise.reject(error);
  },
);

publicAxiosClient.interceptors.response.use(
  unwrapApiResponse,
  (error: AxiosError) => Promise.reject(error),
);

export default axiosClient;

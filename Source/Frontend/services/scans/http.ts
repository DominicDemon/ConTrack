import axios from "axios";

/** The paper-scan routes of the internal API, through the same-origin /api/internal proxy (next.config.mjs). */
export const scanApi = axios.create({ baseURL: "/api/internal", withCredentials: true });

let csrfToken: string | undefined;

// Every call that changes data carries the CSRF token of the internal session.
scanApi.interceptors.request.use(async (config) => {
  if ((config.method ?? "get").toLowerCase() !== "get") {
    if (!csrfToken) {
      const { data } = await axios.get<{ token: string }>("/api/internal/auth/csrf", { withCredentials: true });
      csrfToken = data.token;
    }
    config.headers.set("X-CSRF-TOKEN", csrfToken);
  }
  return config;
});

// A 400 may mean the token expired with the session: the next call asks for a new one.
scanApi.interceptors.response.use(undefined, (error: unknown) => {
  if (axios.isAxiosError(error) && error.response?.status === 400) csrfToken = undefined;
  return Promise.reject(error);
});

/** The internal API answers errors as { message, code }. */
export const getScanErrorMessage = (error: unknown, fallback: string): string => {
  if (axios.isAxiosError(error)) {
    if (!error.response) return "Không kết nối được máy chủ. Vui lòng thử lại.";
    const data = error.response.data as { message?: unknown } | undefined;
    if (data && typeof data === "object" && typeof data.message === "string" && data.message.trim()) return data.message;
    return fallback;
  }
  if (error instanceof Error && error.message) return error.message;
  return fallback;
};

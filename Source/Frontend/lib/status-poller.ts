export const POLL_INTERVAL = 4_000;
export const POLL_LIMIT = 15 * 60_000;

/** Completion-based scheduling: one request at a time, including foreground refreshes. */
export function startStatusPoller<T>(options: {
  request: (signal: AbortSignal) => Promise<T>;
  terminal: (value: T) => boolean;
  onValue: (value: T) => void;
  onError: (error: unknown) => void;
  onTimeout: () => void;
}) {
  let stopped = false, running = false, foregroundPending = false;
  let timer: ReturnType<typeof setTimeout> | undefined;
  let controller: AbortController | undefined;
  const startedAt = Date.now();
  const cleanup = () => {
    stopped = true;
    clearTimeout(timer);
    clearTimeout(deadline);
    controller?.abort();
    document.removeEventListener("visibilitychange", foreground);
  };
  const expire = () => { if (!stopped) { cleanup(); options.onTimeout(); } };
  const run = async () => {
    if (stopped || running) return;
    if (Date.now() - startedAt >= POLL_LIMIT) { expire(); return; }
    running = true;
    controller = new AbortController();
    try {
      const value = await options.request(controller.signal);
      if (stopped) return;
      options.onValue(value);
      if (options.terminal(value)) { cleanup(); return; }
    } catch (error) {
      if (stopped) return;
      options.onError(error);
      const status = (error as { response?: { status?: number } })?.response?.status;
      if (status === 400 || status === 403 || status === 404) { cleanup(); return; }
    } finally { running = false; }
    if (!stopped) {
      timer = setTimeout(() => void run(), foregroundPending ? 0 : POLL_INTERVAL);
      foregroundPending = false;
    }
  };
  const foreground = () => {
    if (document.visibilityState !== "visible" || stopped) return;
    if (running) { foregroundPending = true; return; }
    clearTimeout(timer);
    void run();
  };
  const deadline = setTimeout(expire, POLL_LIMIT);
  document.addEventListener("visibilitychange", foreground);
  // Strict Mode's setup/cleanup probe cannot dispatch a stale request.
  queueMicrotask(() => void run());
  return cleanup;
}

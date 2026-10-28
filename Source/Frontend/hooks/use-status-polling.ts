"use client";

import { useEffect, useState } from "react";
import { startStatusPoller } from "@/lib/status-poller";

export function useStatusPolling<T>(key: string, request: (signal: AbortSignal) => Promise<T>, terminal: (value: T) => boolean) {
  const [state, setState] = useState<{ key: string; value: T | null; error: unknown; timedOut: boolean }>({ key, value: null, error: null, timedOut: false });
  const [attempt, setAttempt] = useState(0);
  useEffect(() => {
    if (!key) return;
    return startStatusPoller({ request, terminal,
      onValue: value => setState({ key, value, error: null, timedOut: false }),
      onError: error => setState(old => ({ key, value: old.key === key ? old.value : null, error, timedOut: false })),
      onTimeout: () => setState(old => ({ key, value: old.key === key ? old.value : null,
        error: old.key === key ? old.error : null, timedOut: true })),
    });
  }, [key, request, terminal, attempt]);
  return { value: state.key === key ? state.value : null, error: state.key === key ? state.error : null,
    timedOut: state.key === key && state.timedOut,
    retry: () => { setState(old => ({ ...old, timedOut: false, error: null })); setAttempt(x => x + 1); } };
}

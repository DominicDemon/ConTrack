"use client";

import { useEffect, useState } from "react";
import axios from "axios";

import { useAuthStore } from "@/hooks/use-auth-store";
import { authApi, type UserProfileDto } from "@/services/auth-api";

export type PricingSession =
  | { status: "checking" }
  | { status: "guest" }
  | { status: "authenticated"; user: UserProfileDto }
  | { status: "error" };

export function usePricingSession() {
  const [session, setSession] = useState<PricingSession>({ status: "checking" });
  const [attempt, setAttempt] = useState(0);
  const setUser = useAuthStore((state) => state.setUser);

  useEffect(() => {
    const controller = new AbortController();
    const checkSession = async () => {
      try {
        const user = await authApi.getPublicSession(controller.signal);
        if (controller.signal.aborted) return;
        setUser(user);
        setSession({ status: "authenticated", user });
      } catch (error: unknown) {
        if (controller.signal.aborted) return;
        if (axios.isAxiosError(error) && error.response?.status === 401) {
          setUser(null);
          setSession({ status: "guest" });
        } else {
          // A network/server failure is not evidence that the visitor is logged out.
          setSession({ status: "error" });
        }
      }
    };
    void checkSession();
    return () => controller.abort();
  }, [attempt, setUser]);

  const retry = () => {
    setSession({ status: "checking" });
    setAttempt((value) => value + 1);
  };

  return { session, retry };
}

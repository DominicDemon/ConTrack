"use client";
import { useCallback, useEffect, useState } from "react";

/**
 * The customer's file key, remembered for {@link REMEMBER_MINUTES} minutes after its last use so it need not be typed
 * on every page. Kept in this tab's sessionStorage only (gone when the tab closes, never sent anywhere by itself).
 */
export const REMEMBER_MINUTES = 15;
const STORE = "portal-file-key";

type Stored = { key: string; until: number };

function read(): string {
  try {
    const raw = sessionStorage.getItem(STORE);
    if (!raw) return "";
    const stored = JSON.parse(raw) as Stored;
    if (stored.until > Date.now()) return stored.key;
    sessionStorage.removeItem(STORE);
  } catch { /* storage blocked: the key is simply not remembered */ }
  return "";
}

function write(key: string) {
  try {
    if (key) sessionStorage.setItem(STORE, JSON.stringify({ key, until: Date.now() + REMEMBER_MINUTES * 60_000 } satisfies Stored));
    else sessionStorage.removeItem(STORE);
  } catch { /* ignore */ }
}

/** [key, setKey, remembered, forget]; the key is remembered on every change and forgotten after 15 idle minutes. */
export function useRememberedFileKey(): [string, (key: string) => void, boolean, () => void] {
  const [key, setKeyState] = useState("");
  const [remembered, setRemembered] = useState(false);
  useEffect(() => {
    const saved = read();
    // eslint-disable-next-line react-hooks/set-state-in-effect -- sessionStorage is only readable after mount
    if (saved) { setKeyState(saved); setRemembered(true); }
    const timer = setInterval(() => { if (!read()) { setKeyState(""); setRemembered(false); } }, 30_000);
    return () => clearInterval(timer);
  }, []);
  const setKey = useCallback((value: string) => { setKeyState(value); write(value.trim()); setRemembered(!!value.trim()); }, []);
  const forget = useCallback(() => { write(""); setKeyState(""); setRemembered(false); }, []);
  return [key, setKey, remembered, forget];
}

/** Call after the key was used successfully: the 15 minutes start again. */
export function touchFileKey(key: string) { if (key.trim()) write(key.trim()); }

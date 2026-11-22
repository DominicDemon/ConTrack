"use client";

import { useState } from "react";
import { Input } from "@/components/ui/input";
import { cn } from "@/lib/utils";

/** "2026-11-20" to "20/11/2026"; anything else to "". */
export const isoToVn = (iso: string) => {
  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(iso);
  return match ? `${match[3]}/${match[2]}/${match[1]}` : "";
};

/** "20/11/2026" to "2026-11-20" when it is a real day; null otherwise. */
export const vnToIso = (text: string): string | null => {
  const match = /^(\d{2})\/(\d{2})\/(\d{4})$/.exec(text);
  if (!match) return null;
  const [day, month, year] = [Number(match[1]), Number(match[2]), Number(match[3])];
  const date = new Date(Date.UTC(year, month - 1, day));
  if (year < 1900 || date.getUTCFullYear() !== year || date.getUTCMonth() !== month - 1 || date.getUTCDate() !== day) return null;
  return `${match[3]}-${match[2]}-${match[1]}`;
};

/** Digits typed in order become dd/mm/yyyy; the slashes are added on the way. */
const mask = (typed: string) => {
  const digits = typed.replace(/\D/g, "").slice(0, 8);
  return [digits.slice(0, 2), digits.slice(2, 4), digits.slice(4, 8)].filter(Boolean).join("/");
};

/**
 * A day typed as dd/mm/yyyy whatever the browser language (an input of type "date" shows mm/dd/yyyy on an English
 * system). The value given and received stays ISO (yyyy-mm-dd), "" when empty, as the API expects.
 */
export function DateInput({
  value,
  onChange,
  min,
  max,
  id,
  disabled,
  className,
  "aria-label": ariaLabel,
}: {
  value: string;
  onChange: (iso: string) => void;
  min?: string;
  max?: string;
  id?: string;
  disabled?: boolean;
  className?: string;
  "aria-label"?: string;
}) {
  const [text, setText] = useState(isoToVn(value));
  const [touched, setTouched] = useState(false);

  // A value set from outside (cleared filter, date read by OCR) is shown; what is being typed is left alone.
  const [seen, setSeen] = useState(value);
  if (seen !== value) {
    setSeen(value);
    if ((vnToIso(text) ?? "") !== value) setText(isoToVn(value));
  }

  const iso = vnToIso(text);
  const problem = !touched || text === "" ? null
    : iso === null ? "Nhập ngày dạng dd/mm/yyyy."
    : min && iso < min ? `Không trước ${isoToVn(min)}.`
    : max && iso > max ? `Không sau ${isoToVn(max)}.`
    : null;

  return (
    <span className="block space-y-1">
      <Input
        id={id}
        aria-label={ariaLabel}
        inputMode="numeric"
        placeholder="dd/mm/yyyy"
        maxLength={10}
        value={text}
        disabled={disabled}
        aria-invalid={problem ? true : undefined}
        className={cn("font-mono", className)}
        onChange={(event) => {
          const next = mask(event.target.value);
          setText(next);
          const parsed = vnToIso(next);
          if (next === "") onChange("");
          else if (parsed && (!min || parsed >= min) && (!max || parsed <= max)) onChange(parsed);
        }}
        onBlur={() => setTouched(true)}
      />
      {problem && <span className="block text-xs text-destructive">{problem}</span>}
    </span>
  );
}

"use client";

import { useState, type ClipboardEvent, type KeyboardEvent } from "react";
import { X } from "lucide-react";
import { cn } from "@/lib/utils";

/** Same limit as the server (ScanEmail.MaxRecipients). */
export const MAX_EMAILS = 10;

const EMAIL = /^[^\s@,;]+@[^\s@,;]+\.[^\s@,;]+$/;
export const isEmail = (value: string) => EMAIL.test(value);

/** Pasted or typed text split into addresses: commas, semicolons, spaces and new lines all separate. */
const split = (text: string) => text.split(/[\s,;]+/).map((part) => part.trim()).filter(Boolean);

/**
 * Several email addresses in one box: each address becomes a chip once Enter, a comma, a semicolon or a space is
 * typed, or when a list is pasted. Wrong addresses are shown in red; Backspace on the empty box removes the last one.
 */
export function EmailInput({
  id,
  value,
  onChange,
  disabled,
  invalid,
}: {
  id?: string;
  value: string[];
  onChange: (emails: string[]) => void;
  disabled?: boolean;
  invalid?: boolean;
}) {
  const [draft, setDraft] = useState("");

  const add = (text: string) => {
    const known = new Set(value.map((email) => email.toLowerCase()));
    const added = split(text).filter((email) => {
      const key = email.toLowerCase();
      if (known.has(key)) return false;
      known.add(key);
      return true;
    });
    if (added.length > 0) onChange([...value, ...added]);
    setDraft("");
  };

  const remove = (index: number) => onChange(value.filter((_, at) => at !== index));

  const keyDown = (event: KeyboardEvent<HTMLInputElement>) => {
    if (["Enter", ",", ";", " ", "Tab"].includes(event.key) && draft.trim()) {
      event.preventDefault();
      add(draft);
    } else if (event.key === "Enter") {
      event.preventDefault();
    } else if (event.key === "Backspace" && !draft && value.length > 0) {
      remove(value.length - 1);
    }
  };

  const paste = (event: ClipboardEvent<HTMLInputElement>) => {
    const text = event.clipboardData.getData("text");
    if (split(text).length > 1) {
      event.preventDefault();
      add(`${draft} ${text}`);
    }
  };

  return (
    <div
      className={cn(
        "flex min-h-10 w-full flex-wrap items-center gap-1.5 rounded-md border bg-transparent px-2 py-1.5 text-sm shadow-xs",
        "focus-within:border-ring focus-within:ring-[3px] focus-within:ring-ring/50",
        invalid && "border-destructive",
        disabled && "opacity-60",
      )}
    >
      {value.map((email, index) => {
        const wrong = !isEmail(email);
        return (
          <span
            key={`${email}-${index}`}
            className={cn(
              "inline-flex max-w-full items-center gap-1 rounded-full border py-0.5 pr-1 pl-2.5",
              wrong ? "border-destructive/50 bg-destructive/10 text-destructive" : "bg-muted",
            )}
            title={wrong ? "Email không hợp lệ" : undefined}
          >
            <span className="truncate">{email}</span>
            <button
              type="button"
              className="rounded-full p-0.5 hover:bg-foreground/10 disabled:pointer-events-none"
              aria-label={`Bỏ ${email}`}
              disabled={disabled}
              onClick={() => remove(index)}
            >
              <X className="size-3.5" aria-hidden="true" />
            </button>
          </span>
        );
      })}
      <input
        id={id}
        type="email"
        inputMode="email"
        autoComplete="off"
        value={draft}
        disabled={disabled}
        onChange={(event) => setDraft(event.target.value)}
        onKeyDown={keyDown}
        onPaste={paste}
        onBlur={() => draft.trim() && add(draft)}
        placeholder={value.length === 0 ? "ten@congty.vn, khach@gmail.com…" : ""}
        className="min-w-[10rem] flex-1 bg-transparent px-1 py-0.5 outline-none placeholder:text-muted-foreground"
      />
    </div>
  );
}

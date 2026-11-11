import { Fragment, type ReactNode } from "react";
import { cn } from "@/lib/utils";

/** Upper case without Vietnamese accents, as the server compares: "Thương mại" and "THUONG MAI" match. */
const fold = (char: string) =>
  char
    .normalize("NFD")
    .replace(/[̀-ͯ]/g, "")
    .replace(/đ/g, "d")
    .replace(/Đ/g, "D")
    .toUpperCase();

/** The characters kept for comparing, folded, each with the index of the character it came from. */
function project(text: string, keep: (char: string) => boolean) {
  let folded = "";
  const origin: number[] = [];
  for (let index = 0; index < text.length; index += 1) {
    const char = text[index];
    if (!keep(char)) continue;
    for (const part of fold(char)) {
      folded += part;
      origin.push(index);
    }
  }
  return { folded, origin };
}

const notSpace = (char: string) => !/\s/.test(char);
const isDigit = (char: string) => /\d/.test(char);

/** Digits of a typed phone or tax code; +84 912… is the stored 0912…. */
const typedDigits = (query: string) => {
  const digits = query.replace(/\D/g, "");
  const international = /^\s*\+84/.test(query) || (/^\s*84/.test(query) && digits.length >= 11);
  return international ? `0${digits.slice(2)}` : digits;
};

/**
 * Where the typed text occurs in a shown value, as [start, end) ranges of that value. Compared like the search:
 * without case, accents or spaces; a phone or tax code also by its digits alone ("0912 345" in "0912 345 678").
 */
export function matchRanges(text: string, query: string): Array<[number, number]> {
  const attempts: Array<[(char: string) => boolean, string]> = [[notSpace, project(query, notSpace).folded]];
  const digits = typedDigits(query);
  if (digits.length >= 3) attempts.push([isDigit, digits]);

  for (const [keep, needle] of attempts) {
    if (!needle) continue;
    const { folded, origin } = project(text, keep);
    const ranges: Array<[number, number]> = [];
    for (let at = folded.indexOf(needle); at >= 0; at = folded.indexOf(needle, at + needle.length)) {
      const start = origin[at];
      const end = origin[at + needle.length - 1] + 1;
      const last = ranges[ranges.length - 1];
      if (last && start <= last[1]) last[1] = Math.max(last[1], end);
      else ranges.push([start, end]);
    }
    if (ranges.length > 0) return ranges;
  }
  return [];
}

/** The text with every part matching the query marked. */
export function Highlight({ text, query, className }: { text: string; query: string; className?: string }) {
  const ranges = query.trim() ? matchRanges(text, query) : [];
  if (ranges.length === 0) return <>{text}</>;

  const parts: ReactNode[] = [];
  let at = 0;
  ranges.forEach(([start, end], index) => {
    if (start > at) parts.push(<Fragment key={`t${index}`}>{text.slice(at, start)}</Fragment>);
    parts.push(
      <mark
        key={`m${index}`}
        className={cn("rounded-sm bg-amber-200 px-0.5 font-semibold text-foreground dark:bg-amber-500/40", className)}
      >
        {text.slice(start, end)}
      </mark>,
    );
    at = end;
  });
  if (at < text.length) parts.push(<Fragment key="rest">{text.slice(at)}</Fragment>);
  return <>{parts}</>;
}

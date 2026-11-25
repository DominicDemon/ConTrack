import type { ReactNode } from "react";

/**
 * The vault search as the page shows it: the same rules as the server (VaultSearch.cs), so what is highlighted is what
 * made a document match. Accents, case, spaces and punctuation do not matter; a phone typed +84… also means 0….
 */

/** One character without accents, lower case: "Đ" → "d", "Ế" → "e". */
const plain = (character: string) =>
  character.replace(/đ/g, "d").replace(/Đ/g, "D").normalize("NFD").replace(/\p{M}/gu, "").toLowerCase();

const isWordChar = (character: string) => /[\p{L}\p{N}]/u.test(character);

/** Lower case, no accents, split on anything that is not a letter or a digit. */
export function searchWords(value: string | null | undefined): string[] {
  return Array.from(value ?? "").map(c => { const p = plain(c); return isWordChar(p) ? p : " "; }).join("").split(" ").filter(Boolean);
}

const variants = (word: string) =>
  /^84\d{8,10}$/.test(word) ? [word, "0" + word.slice(2)] : [word];

/** The pieces of the query to look for: the whole query without spaces, then each word of 2 characters or more. */
function needles(query: string): string[] {
  const words = searchWords(query);
  const whole = words.join("");
  return [...new Set([...(whole.length >= 2 ? variants(whole) : []), ...words.filter(w => w.length >= 2).flatMap(variants)])];
}

/** Whether a field (a keyword value, the title…) contains part of the query, by the server's rules. */
export function fieldMatches(value: string | null | undefined, query: string): boolean {
  const compact = searchWords(value).join("");
  return !!compact && needles(query).some(needle => compact.includes(needle));
}

/** The text with the parts that match the query marked; accents and punctuation inside a match are kept as typed. */
export function Highlight({ text, query }: { text: string | null | undefined; query: string }): ReactNode {
  const value = text ?? "";
  const found = needles(query);
  if (!value || found.length === 0) return value;
  // The compact form (letters and digits only) and, for each of its characters, where it sits in the original text.
  const chars = Array.from(value);
  let compact = ""; const at: number[] = [];
  chars.forEach((c, i) => { const p = plain(c); if (isWordChar(p)) for (const piece of p) { compact += piece; at.push(i); } });
  const marked = new Array<boolean>(chars.length).fill(false);
  for (const needle of found) {
    for (let start = compact.indexOf(needle); start >= 0; start = compact.indexOf(needle, start + 1)) {
      for (let i = at[start]; i <= at[start + needle.length - 1]; i++) marked[i] = true;
    }
  }
  if (!marked.includes(true)) return value;
  const parts: ReactNode[] = []; let run = ""; let inMark = marked[0];
  const flush = (key: number) => { if (run) parts.push(inMark ? <mark key={key} style={{ background: "#fde68a", color: "inherit", borderRadius: 2, padding: 0 }}>{run}</mark> : run); run = ""; };
  chars.forEach((c, i) => { if (marked[i] !== inMark) { flush(i); inMark = marked[i]; } run += c; });
  flush(chars.length);
  return <>{parts}</>;
}

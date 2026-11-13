"use client";

import Link from "next/link";
import { useCallback, useEffect, useId, useRef, useState, type FormEvent } from "react";
import { Eye, Loader2, Mail, ScanLine, Search } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { cn } from "@/lib/utils";
import { getScanErrorMessage } from "@/services/scans/http";
import {
  signedEvidenceApi,
  type ScanSearchMatch,
  type ScanSearchResponse,
  type ScanSearchResult,
  type ScannedSearchResult,
  type ScanSearchType,
} from "@/services/scans/signed-scans";
import { contractHref } from "./contract-list";
import { formatDateTime, formatKeywordValue, formatSignDate, KEYWORD_TYPE_LABELS } from "./format";
import { Highlight } from "./highlight";
import { ScanEmailDialog, ScanReviewDialog, type ScanTarget } from "./scan-actions";
import { ContractStatusBadge, EvidenceStatusBadge, StatusBadge } from "./status-badge";

const FILTERS: { value: ScanSearchType; label: string }[] = [
  { value: "All", label: "Tất cả" },
  { value: "ContractCode", label: "Số hợp đồng" },
  { value: "TaxCode", label: "Mã số thuế" },
  { value: "PhoneNumber", label: "Số điện thoại" },
  { value: "CustomerName", label: "Tên khách hàng" },
];

/** The server needs this many characters to search. */
const MIN_LENGTH = 3;

/** Searching starts this long after the last key press, so typing does not send a request per letter. */
const TYPING_DELAY_MS = 300;

const describe = (match: ScanSearchMatch) => {
  const label = KEYWORD_TYPE_LABELS[match.type].toLowerCase();
  return match.page ? `${label}, trang ${match.page}` : `${label} thêm tay`;
};

/** The keywords that matched, with the typed part marked inside each one. */
function Matches({ matches, query }: { matches: ScanSearchMatch[]; query: string }) {
  return (
    <ul className="mt-2 flex flex-wrap gap-x-4 gap-y-1.5 text-sm">
      {matches.map((match, index) => (
        <li key={`${match.type}-${match.value}-${index}`}>
          <span className="rounded border bg-muted/50 px-1.5 py-0.5 tabular-nums">
            <Highlight text={formatKeywordValue(match.type, match.value)} query={query} />
          </span>{" "}
          <span className="text-muted-foreground">· {describe(match)}</span>
        </li>
      ))}
    </ul>
  );
}

const scanTarget = (scan: ScannedSearchResult): ScanTarget => ({
  kind: "scan",
  id: scan.jobId,
  title: scan.contractCode ?? scan.fileName,
  href: `/workspace/scanned-contracts/jobs/${scan.jobId}`,
  matches: scan.matches,
  details: [
    { label: "Tệp", value: scan.fileName },
    { label: "Số hợp đồng", value: scan.contractCode ?? "" },
    { label: "Tên hợp đồng", value: scan.contractName ?? "" },
    { label: "Khách hàng", value: scan.customerName ?? "" },
    { label: "Người tải", value: scan.uploadedBy },
    { label: "Ngày tải", value: formatDateTime(scan.uploadedAt) },
  ].filter((detail) => detail.value),
});

const evidenceTarget = (item: ScanSearchResult): ScanTarget => ({
  kind: "evidence",
  id: item.evidenceId,
  title: item.documentCode ?? item.documentName,
  href: contractHref(item.documentId),
  matches: item.matches,
  details: [
    { label: "Số hợp đồng", value: item.documentCode ?? "" },
    { label: "Tên hợp đồng", value: item.documentName },
    { label: "Khách hàng", value: item.customerName },
    { label: "Ngày ký", value: formatSignDate(item.signDate) },
    { label: "Bản ký", value: `#${item.evidenceId}` },
  ].filter((detail) => detail.value),
});

/** Review and email: the same two actions on every result. */
function ResultActions({
  target,
  onReview,
  onEmail,
}: {
  target: ScanTarget;
  onReview: (target: ScanTarget) => void;
  onEmail: (target: ScanTarget) => void;
}) {
  return (
    <div className="mt-3 flex flex-wrap gap-2 border-t pt-3">
      <Button type="button" variant="outline" size="sm" onClick={() => onReview(target)}>
        <Eye aria-hidden="true" />
        Review
      </Button>
      <Button type="button" variant="outline" size="sm" onClick={() => onEmail(target)}>
        <Mail aria-hidden="true" />
        Gửi email
      </Button>
    </div>
  );
}

/**
 * Finds contracts by the keywords read from their scans: contract code, tax code, phone or customer name. Searches
 * while typing and marks the typed text in every result. Both the paper-signing evidence of contracts in the system
 * and scanned old paper contracts are listed.
 */
export function ScanSearch() {
  const [query, setQuery] = useState("");
  const [type, setType] = useState<ScanSearchType>("All");
  const [response, setResponse] = useState<ScanSearchResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [searching, setSearching] = useState(false);
  const inputId = useId();
  const [reviewing, setReviewing] = useState<ScanTarget | null>(null);
  const [emailing, setEmailing] = useState<ScanTarget | null>(null);
  const closeReview = useCallback(() => setReviewing(null), []);
  const closeEmail = useCallback(() => setEmailing(null), []);
  // Only the answer to the latest request is shown; a slower, older answer is dropped.
  const latest = useRef(0);

  const run = useCallback(async (text: string, filter: ScanSearchType) => {
    const trimmed = text.trim();
    const request = ++latest.current;
    if (trimmed.length < MIN_LENGTH) {
      setSearching(false);
      setError(null);
      if (trimmed.length === 0) setResponse(null);
      return;
    }
    setError(null);
    setSearching(true);
    try {
      const found = await signedEvidenceApi.search({ q: trimmed, type: filter });
      if (request === latest.current) setResponse(found);
    } catch (searchError) {
      if (request === latest.current) setError(getScanErrorMessage(searchError, "Không tìm được. Vui lòng thử lại."));
    } finally {
      if (request === latest.current) setSearching(false);
    }
  }, []);

  // Search as the employee types (and when the filter changes), a moment after the last key press.
  useEffect(() => {
    const timer = window.setTimeout(() => void run(query, type), TYPING_DELAY_MS);
    return () => window.clearTimeout(timer);
  }, [query, type, run]);

  const submit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    void run(query, type);
  };

  const typed = query.trim();
  const tooShort = typed.length > 0 && typed.length < MIN_LENGTH;
  // Marks follow the text the shown results were found with.
  const marked = response?.query ?? "";
  const total = response ? response.items.length + response.scans.length : 0;

  return (
    <div className="space-y-5">
      <form role="search" onSubmit={submit} noValidate className="space-y-3.5">
        <Label htmlFor={inputId}>Từ khóa</Label>
        <div className="flex flex-wrap gap-3">
          <div className="relative min-w-0 flex-[1_1_320px]">
            <Input
              id={inputId}
              type="search"
              value={query}
              onChange={(event) => setQuery(event.target.value)}
              placeholder="Gõ để tìm, VD: 0312345678, 0901 234 567, HD-20261026-000001, Thương mại ABC"
              autoComplete="off"
              className="pr-9"
              aria-invalid={error ? true : undefined}
              aria-describedby={error ? `${inputId}-error` : `${inputId}-hint`}
            />
            {searching && (
              <Loader2
                className="absolute top-1/2 right-3 size-4 -translate-y-1/2 animate-spin text-muted-foreground"
                aria-hidden="true"
              />
            )}
          </div>
          <Button type="submit" disabled={searching}>
            <Search aria-hidden="true" />
            Tìm
          </Button>
        </div>
        {error ? (
          <p id={`${inputId}-error`} role="alert" className="text-sm text-destructive">
            {error}
          </p>
        ) : (
          <p id={`${inputId}-hint`} className="text-xs text-muted-foreground">
            {tooShort ? `Gõ thêm ${MIN_LENGTH - typed.length} ký tự nữa để tìm.` : "Kết quả hiện ngay khi gõ, không cần dấu."}
          </p>
        )}
        <div role="group" aria-label="Lọc theo loại" className="flex flex-wrap gap-2">
          {FILTERS.map((filter) => {
            const selected = type === filter.value;
            return (
              <Button
                key={filter.value}
                type="button"
                variant="outline"
                size="sm"
                aria-pressed={selected}
                onClick={() => setType(filter.value)}
                className={cn(
                  "rounded-full",
                  selected && "border-primary bg-primary/10 font-semibold text-primary hover:bg-primary/15",
                )}
              >
                {filter.label}
              </Button>
            );
          })}
        </div>
      </form>

      {response && (
        <div className={cn("space-y-3 border-t pt-4 transition-opacity", searching && "opacity-60")}>
          <p role="status" aria-live="polite" className="text-muted-foreground">
            {total === 0 ? (
              <>Không tìm thấy bản ký giấy hay bản scan nào khớp với “{response.query}”.</>
            ) : (
              <>
                <strong className="font-semibold text-foreground">{total} kết quả</strong> khớp với “{response.query}”
              </>
            )}
          </p>

          <ul className="space-y-3">
            {response.scans.map((scan) => (
              <li key={`scan-${scan.jobId}`} className="rounded-xl border p-4 transition-colors hover:bg-accent/30">
                <div className="flex flex-col gap-2 sm:flex-row sm:items-start sm:justify-between">
                  <div className="min-w-0">
                    <div className="flex flex-wrap items-center gap-2">
                      <ScanLine className="size-4 text-primary" aria-hidden="true" />
                      <Link
                        href={`/workspace/scanned-contracts/jobs/${scan.jobId}`}
                        className="font-semibold break-all text-primary hover:underline"
                      >
                        <Highlight text={scan.contractCode ?? scan.fileName} query={marked} />
                      </Link>
                      {scan.contractId ? (
                        <StatusBadge tone="success">Hợp đồng scan</StatusBadge>
                      ) : (
                        <StatusBadge tone="info">Bản scan</StatusBadge>
                      )}
                    </div>
                    {scan.contractId ? (
                      <p className="mt-1 text-sm break-all">
                        <Highlight text={scan.contractName ?? ""} query={marked} />
                        {scan.customerName && (
                          <>
                            {" · "}
                            <Highlight text={scan.customerName} query={marked} />
                          </>
                        )}
                      </p>
                    ) : null}
                  </div>
                  <div className="shrink-0 text-xs text-muted-foreground">
                    {scan.uploadedBy} · {formatDateTime(scan.uploadedAt)}
                  </div>
                </div>
                <Matches matches={scan.matches} query={marked} />
                <ResultActions target={scanTarget(scan)} onReview={setReviewing} onEmail={setEmailing} />
              </li>
            ))}
          </ul>

          <ul className="space-y-3">
            {response.items.map((item) => (
              <li key={item.evidenceId} className="rounded-xl border p-4 transition-colors hover:bg-accent/30">
                <div className="flex flex-col gap-2 sm:flex-row sm:items-start sm:justify-between">
                  <div className="min-w-0">
                    <div className="flex flex-wrap items-center gap-2">
                      <Link href={contractHref(item.documentId)} className="font-semibold text-primary hover:underline">
                        <Highlight text={item.documentCode ?? item.documentName} query={marked} />
                      </Link>
                      <ContractStatusBadge status={item.contractStatus} />
                    </div>
                    <p className="mt-1 text-sm">
                      <Highlight text={item.documentName} query={marked} />
                      {" · "}
                      <Highlight text={item.customerName} query={marked} />
                    </p>
                  </div>
                  <div className="flex shrink-0 items-center gap-2 text-xs text-muted-foreground">
                    <StatusBadge tone="neutral">Bản ký giấy</StatusBadge>
                    <EvidenceStatusBadge status={item.evidenceStatus} />
                    bản ký #{item.evidenceId} · ngày ký {formatSignDate(item.signDate)}
                  </div>
                </div>
                <Matches matches={item.matches} query={marked} />
                <ResultActions target={evidenceTarget(item)} onReview={setReviewing} onEmail={setEmailing} />
              </li>
            ))}
          </ul>
        </div>
      )}

      <ScanReviewDialog
        target={reviewing}
        query={marked}
        onClose={closeReview}
        onEmail={(target) => {
          setReviewing(null);
          setEmailing(target);
        }}
      />
      <ScanEmailDialog target={emailing} onClose={closeEmail} />
    </div>
  );
}

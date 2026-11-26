"use client";

import { ListPager } from "@/components/scanned-contracts/list-pager";
import { DateInput } from "@/components/signed-scans/date-input";
import Link from "next/link";
import { useCallback, useEffect, useId, useRef, useState, type FormEvent } from "react";
import { Eye, FileText, Loader2, Mail, ScanLine, Search } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { cn } from "@/lib/utils";
import { getScanErrorMessage } from "@/services/scans/http";
import {
  signedEvidenceApi,
  type OnlineContractMatch,
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
  { value: "Email", label: "Email" },
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

/** What matched on a contract made online: where the value comes from, not an OCR page. */
function OnlineMatches({ matches, query }: { matches: OnlineContractMatch[]; query: string }) {
  return (
    <ul className="mt-2 flex flex-wrap gap-x-4 gap-y-1.5 text-sm">
      {matches.map((match, index) => {
        const label = match.type === "ContractName" ? "tên hợp đồng" : KEYWORD_TYPE_LABELS[match.type].toLowerCase();
        const where = match.source === "Customer" ? "hồ sơ khách hàng" : "trong hợp đồng";
        const value = match.type === "ContractName" ? match.value : formatKeywordValue(match.type, match.value);
        return (
          <li key={`${match.type}-${match.value}-${index}`}>
            <span className="rounded border bg-muted/50 px-1.5 py-0.5 tabular-nums">
              <Highlight text={value} query={query} />
            </span>{" "}
            <span className="text-muted-foreground">
              · {label}, {where}
            </span>
          </li>
        );
      })}
    </ul>
  );
}

const scanTarget = (scan: ScannedSearchResult): ScanTarget => ({
  kind: "scan",
  id: scan.jobId,
  title: scan.contractCode ?? scan.fileName,
  href: `/workspace/scanned-contracts/jobs/${scan.jobId}`,
  matches: scan.matches,
  emails: scan.emails ?? [],
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
  emails: item.emails ?? [],
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
 * One search over every contract: contracts made online (by their own data), paper-signing evidence and scanned old
 * paper contracts (by the keywords read from the scans). Searches while typing and marks the typed text in every result.
 */
export function ScanSearch() {
  const [onlinePage, setOnlinePage] = useState(1);
  const [scanPage, setScanPage] = useState(1);
  const [contractPage, setContractPage] = useState(1);
  const [query, setQuery] = useState("");
  const [type, setType] = useState<ScanSearchType>("All");
  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");
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

  const run = useCallback(async (text: string, filter: ScanSearchType, first: string, last: string) => {
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
      const found = await signedEvidenceApi.search({ q: trimmed, type: filter, from: first, to: last });
      if (request === latest.current) {
        setOnlinePage(1);
        setScanPage(1);
        setContractPage(1);
        setResponse(found);
      }
    } catch (searchError) {
      if (request === latest.current) setError(getScanErrorMessage(searchError, "Không tìm được. Vui lòng thử lại."));
    } finally {
      if (request === latest.current) setSearching(false);
    }
  }, []);

  // Search as the employee types (and when the filter changes), a moment after the last key press.
  useEffect(() => {
    const timer = window.setTimeout(() => void run(query, type, from, to), TYPING_DELAY_MS);
    return () => window.clearTimeout(timer);
  }, [query, type, from, to, run]);

  const submit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    void run(query, type, from, to);
  };

  const typed = query.trim();
  const tooShort = typed.length > 0 && typed.length < MIN_LENGTH;
  // Marks follow the text the shown results were found with.
  const marked = response?.query ?? "";
  const online = response?.contracts ?? [];
  const total = response ? response.items.length + response.scans.length + online.length : 0;

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
        <div className="flex flex-wrap items-end gap-3 text-sm">
          <label className="space-y-1">
            <span className="block text-xs text-muted-foreground">Từ ngày</span>
            <DateInput aria-label="Từ ngày" value={from} max={to || undefined} onChange={setFrom} className="w-36" />
          </label>
          <label className="space-y-1">
            <span className="block text-xs text-muted-foreground">Đến ngày</span>
            <DateInput aria-label="Đến ngày" value={to} min={from || undefined} onChange={setTo} className="w-36" />
          </label>
          {(from || to) && (
            <Button type="button" variant="ghost" size="sm" onClick={() => { setFrom(""); setTo(""); }}>
              Bỏ lọc ngày
            </Button>
          )}
          <span className="text-xs text-muted-foreground">
            Hợp đồng online: theo ngày tạo. Bản scan cũ: theo ngày tải lên. Bản ký giấy: theo ngày ký.
          </span>
          <span className="ml-auto flex gap-2">
            <Button asChild type="button" variant="ghost" size="sm">
              <Link href="/workspace/scan-emails">
                <Mail aria-hidden="true" />
                Lịch sử gửi email
              </Link>
            </Button>
          </span>
        </div>
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
              <>Không tìm thấy hợp đồng online, bản ký giấy hay bản scan nào khớp với “{response.query}”.</>
            ) : (
              <>
                <strong className="font-semibold text-foreground">{total} kết quả</strong> khớp với “{response.query}”
              </>
            )}
          </p>

          <ul className="space-y-3">
            {online.slice((onlinePage - 1) * 10, onlinePage * 10).map((contract) => (
              <li key={`online-${contract.documentId}`} className="rounded-xl border p-4 transition-colors hover:bg-accent/30">
                <div className="flex flex-col gap-2 sm:flex-row sm:items-start sm:justify-between">
                  <div className="min-w-0">
                    <div className="flex flex-wrap items-center gap-2">
                      <FileText className="size-4 text-primary" aria-hidden="true" />
                      <Link
                        href={
                          contract.hasDraft === false
                            ? contractHref(contract.documentId)
                            : `/workspace/contracts/drafts/${contract.documentId}`
                        }
                        className="font-semibold [overflow-wrap:anywhere] text-primary hover:underline"
                      >
                        <Highlight text={contract.documentCode ?? contract.documentName} query={marked} />
                      </Link>
                      <ContractStatusBadge status={contract.contractStatus} />
                    </div>
                    <p className="mt-1 text-sm [overflow-wrap:anywhere]">
                      <Highlight text={contract.documentName} query={marked} />
                      {" · "}
                      <Highlight text={contract.customerName} query={marked} />
                    </p>
                  </div>
                  <div className="flex shrink-0 items-center gap-2 text-xs text-muted-foreground">
                    <StatusBadge tone="neutral">Hợp đồng online</StatusBadge>
                    {contract.versionNo ? `phiên bản ${contract.versionNo} · ` : ""}cập nhật {formatDateTime(contract.updatedAt)}
                  </div>
                </div>
                <OnlineMatches matches={contract.matches} query={marked} />
              </li>
            ))}
          </ul>
          <ListPager page={onlinePage} pageSize={10} total={online.length} onPage={setOnlinePage} label="hợp đồng online"/>

          <ul className="space-y-3">
            {response.scans.slice((scanPage - 1) * 10, scanPage * 10).map((scan) => (
              <li key={`scan-${scan.jobId}`} className="rounded-xl border p-4 transition-colors hover:bg-accent/30">
                <div className="flex flex-col gap-2 sm:flex-row sm:items-start sm:justify-between">
                  <div className="min-w-0">
                    <div className="flex flex-wrap items-center gap-2">
                      <ScanLine className="size-4 text-primary" aria-hidden="true" />
                      <Link
                        href={`/workspace/scanned-contracts/jobs/${scan.jobId}`}
                        className="font-semibold [overflow-wrap:anywhere] text-primary hover:underline"
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
                      <p className="mt-1 text-sm [overflow-wrap:anywhere]">
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
          <ListPager page={scanPage} pageSize={10} total={response.scans.length} onPage={setScanPage} label="bản scan"/>

          <ul className="space-y-3">
            {response.items.slice((contractPage - 1) * 10, contractPage * 10).map((item) => (
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
          <ListPager page={contractPage} pageSize={10} total={response.items.length} onPage={setContractPage} label="hợp đồng"/>
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

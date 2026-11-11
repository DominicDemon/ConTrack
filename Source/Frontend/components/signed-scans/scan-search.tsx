"use client";

import Link from "next/link";
import { useId, useState, type FormEvent } from "react";
import { Loader2, ScanLine, Search } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { cn } from "@/lib/utils";
import { getScanErrorMessage } from "@/services/scans/http";
import {
  signedEvidenceApi,
  type ScanSearchMatch,
  type ScanSearchResponse,
  type ScanSearchType,
} from "@/services/scans/signed-scans";
import { contractHref } from "./contract-list";
import { formatKeywordValue, formatSignDate, KEYWORD_TYPE_LABELS } from "./format";
import { ContractStatusBadge, EvidenceStatusBadge, StatusBadge } from "./status-badge";
import { formatDateTime } from "./format";

const FILTERS: { value: ScanSearchType; label: string }[] = [
  { value: "All", label: "Tất cả" },
  { value: "ContractCode", label: "Số hợp đồng" },
  { value: "TaxCode", label: "Mã số thuế" },
  { value: "PhoneNumber", label: "Số điện thoại" },
  { value: "CustomerName", label: "Tên khách hàng" },
];

const describe = (match: ScanSearchMatch) => {
  const label = KEYWORD_TYPE_LABELS[match.type].toLowerCase();
  return match.page ? `${label}, trang ${match.page}` : `${label} thêm tay`;
};

function Matches({ matches }: { matches: ScanSearchMatch[] }) {
  return (
    <ul className="mt-2 flex flex-wrap gap-x-4 gap-y-1 text-sm">
      {matches.map((match, index) => (
        <li key={`${match.type}-${match.value}-${index}`}>
          <mark className="rounded bg-orange-100 px-1 text-foreground tabular-nums dark:bg-orange-500/20">
            {formatKeywordValue(match.type, match.value)}
          </mark>{" "}
          <span className="text-muted-foreground">· {describe(match)}</span>
        </li>
      ))}
    </ul>
  );
}

/**
 * Finds contracts by the keywords read from their scans: contract code, tax code, phone or customer name. Both the
 * paper-signing evidence of contracts in the system and scanned old paper contracts are listed.
 */
export function ScanSearch() {
  const [query, setQuery] = useState("");
  const [type, setType] = useState<ScanSearchType>("All");
  const [response, setResponse] = useState<ScanSearchResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [searching, setSearching] = useState(false);
  const inputId = useId();

  const run = async (text: string, filter: ScanSearchType) => {
    const trimmed = text.trim();
    if (trimmed.length < 3) {
      setError("Nhập ít nhất 3 ký tự để tìm.");
      return;
    }
    setError(null);
    setSearching(true);
    try {
      setResponse(await signedEvidenceApi.search({ q: trimmed, type: filter }));
    } catch (searchError) {
      setError(getScanErrorMessage(searchError, "Không tìm được. Vui lòng thử lại."));
    } finally {
      setSearching(false);
    }
  };

  const submit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    void run(query, type);
  };

  const changeFilter = (filter: ScanSearchType) => {
    setType(filter);
    if (response) void run(query, filter);
  };

  return (
    <div className="space-y-5">
      <form role="search" onSubmit={submit} noValidate className="space-y-3.5">
        <Label htmlFor={inputId}>Từ khóa</Label>
        <div className="flex flex-wrap gap-3">
          <Input
            id={inputId}
            type="search"
            value={query}
            onChange={(event) => setQuery(event.target.value)}
            placeholder="VD: 0312345678, 0901 234 567, HD-20261026-000001, Thương mại ABC"
            className="min-w-0 flex-[1_1_320px]"
            aria-invalid={error ? true : undefined}
            aria-describedby={error ? `${inputId}-error` : undefined}
          />
          <Button type="submit" disabled={searching}>
            {searching ? <Loader2 className="animate-spin" aria-hidden="true" /> : <Search aria-hidden="true" />}
            Tìm
          </Button>
        </div>
        {error && (
          <p id={`${inputId}-error`} role="alert" className="text-sm text-destructive">
            {error}
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
                onClick={() => changeFilter(filter.value)}
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
        <div className="space-y-3 border-t pt-4">
          <p role="status" className="text-muted-foreground">
            {response.items.length + response.scans.length === 0 ? (
              <>Không tìm thấy bản ký giấy hay bản scan nào khớp với “{response.query}”.</>
            ) : (
              <>
                <strong className="font-semibold text-foreground">{response.items.length + response.scans.length} kết quả</strong>{" "}
                khớp với “{response.query}”
              </>
            )}
          </p>

          <ul className="space-y-3">
            {response.scans.map((scan) => (
              <li key={`scan-${scan.jobId}`}>
                <Link
                  href={`/workspace/scanned-contracts/jobs/${scan.jobId}`}
                  className="block rounded-xl border p-4 transition-colors hover:bg-accent/40"
                >
                  <div className="flex flex-col gap-2 sm:flex-row sm:items-start sm:justify-between">
                    <div className="min-w-0">
                      <div className="flex flex-wrap items-center gap-2">
                        <ScanLine className="size-4 text-primary" aria-hidden="true" />
                        <span className="font-semibold text-primary">{scan.contractCode ?? scan.fileName}</span>
                        {scan.contractId ? (
                          <StatusBadge tone="success">Hợp đồng scan</StatusBadge>
                        ) : (
                          <StatusBadge tone="info">Bản scan</StatusBadge>
                        )}
                      </div>
                      <p className="mt-1 text-sm break-all">
                        {scan.contractId ? `${scan.contractName ?? ""}${scan.customerName ? ` · ${scan.customerName}` : ""}` : scan.fileName}
                      </p>
                    </div>
                    <div className="shrink-0 text-xs text-muted-foreground">
                      {scan.uploadedBy} · {formatDateTime(scan.uploadedAt)}
                    </div>
                  </div>
                  <Matches matches={scan.matches} />
                </Link>
              </li>
            ))}
          </ul>

          <ul className="space-y-3">
            {response.items.map((item) => (
              <li key={item.evidenceId}>
                <Link
                  href={contractHref(item.documentId)}
                  className="block rounded-xl border p-4 transition-colors hover:bg-accent/40"
                >
                  <div className="flex flex-col gap-2 sm:flex-row sm:items-start sm:justify-between">
                    <div className="min-w-0">
                      <div className="flex flex-wrap items-center gap-2">
                        <span className="font-semibold text-primary">{item.documentCode ?? item.documentName}</span>
                        <ContractStatusBadge status={item.contractStatus} />
                      </div>
                      <p className="mt-1 text-sm">
                        {item.documentName} · {item.customerName}
                      </p>
                    </div>
                    <div className="flex shrink-0 items-center gap-2 text-xs text-muted-foreground">
                      <StatusBadge tone="neutral">Bản ký giấy</StatusBadge>
                      <EvidenceStatusBadge status={item.evidenceStatus} />
                      bản ký #{item.evidenceId} · ngày ký {formatSignDate(item.signDate)}
                    </div>
                  </div>
                  <Matches matches={item.matches} />
                </Link>
              </li>
            ))}
          </ul>
        </div>
      )}
    </div>
  );
}

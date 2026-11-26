"use client";

import { useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { Eye } from "lucide-react";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { cn } from "@/lib/utils";
import { getScanErrorMessage } from "@/services/scans/http";
import { signedEvidenceApi, type ContractListItem } from "@/services/scans/signed-scans";
import { ListPager } from "../scanned-contracts/list-pager";
import { SortHeader, useListView } from "@/components/internal/list-tools";
import { Input } from "@/components/ui/input";
import { formatMoney, type StatusTone } from "./format";
import { StatusBadge } from "./status-badge";

export const contractHref = (documentId: string) => `/workspace/signed-contracts/${documentId}`;

/**
 * Where a contract stands in paper signing. The server lists only contracts signed on paper: waiting for their copy,
 * or with one. "Chờ ký" alone would read as "go and sign"; the paper is often signed already and only the scan is missing.
 */
type Step = "review" | "upload" | "resend" | "accepted" | "other";

const STEPS: Record<Step, { label: string; tone: StatusTone; order: number; hint: string }> = {
  review: { label: "Chờ kiểm tra", tone: "pending", order: 0, hint: "Đã có bản scan: kiểm tra chữ ký, con dấu rồi xác nhận." },
  resend: { label: "Cần nộp lại", tone: "danger", order: 1, hint: "Bản scan trước không hợp lệ: nộp bản scan khác." },
  upload: { label: "Chờ nộp bản ký", tone: "info", order: 2, hint: "In, hai bên ký và đóng dấu, rồi scan và tải lên." },
  accepted: { label: "Đã xác nhận", tone: "success", order: 3, hint: "Bản ký giấy hợp lệ, hợp đồng đã ký." },
  other: { label: "Khác", tone: "neutral", order: 4, hint: "" },
};

const stepOf = (item: ContractListItem): Step => {
  if (item.paperEvidenceStatus === "PendingReview") return "review";
  if (item.paperEvidenceStatus === "Accepted") return "accepted";
  if (item.paperEvidenceStatus === "Rejected") return "resend";
  if (item.contractStatus === "PendingSignature") return "upload";
  return "other";
};

type Filter = "todo" | "review" | "upload" | "accepted" | "all";
const FILTERS: { value: Filter; label: string; steps: Step[] }[] = [
  { value: "todo", label: "Cần xử lý", steps: ["review", "resend", "upload"] },
  { value: "review", label: "Chờ kiểm tra", steps: ["review"] },
  { value: "upload", label: "Chờ nộp bản ký", steps: ["upload", "resend"] },
  { value: "accepted", label: "Đã xác nhận", steps: ["accepted"] },
  { value: "all", label: "Tất cả", steps: ["review", "resend", "upload", "accepted", "other"] },
];


function StepBadge({ item }: { item: ContractListItem }) {
  const step = STEPS[stepOf(item)];
  return (
    <span title={step.hint}>
      <StatusBadge tone={step.tone}>{step.label}</StatusBadge>
    </span>
  );
}

/** The paper-signing queue: copies to check first, then contracts waiting for their signed copy. */
export function ContractList() {
  const router = useRouter();
  const [state, setState] = useState<{ items: ContractListItem[] | null; error: string | null }>({ items: null, error: null });
  const [filter, setFilter] = useState<Filter>("todo");
  const [search, setSearch] = useState("");

  useEffect(() => {
    let cancelled = false;
    signedEvidenceApi.listContracts().then(
      (items) => !cancelled && setState({ items, error: null }),
      (error: unknown) =>
        !cancelled && setState({ items: null, error: getScanErrorMessage(error, "Không tải được danh sách hợp đồng.") }),
    );
    return () => {
      cancelled = true;
    };
  }, []);

  // Work first: copies to check, then copies to send again, then contracts waiting for a copy; newest first inside a group.
  const sorted = useMemo(
    () =>
      [...(state.items ?? [])].sort(
        (a, b) => STEPS[stepOf(a)].order - STEPS[stepOf(b)].order || Number(b.documentId) - Number(a.documentId),
      ),
    [state.items],
  );
  const counts = useMemo(() => {
    const result = {} as Record<Filter, number>;
    for (const f of FILTERS) result[f.value] = sorted.filter((item) => f.steps.includes(stepOf(item))).length;
    return result;
  }, [sorted]);

  const list = useListView(sorted, { search, fields: item => [item.documentCode, item.documentName, item.customerName], filterKey: filter,
    filter: item => FILTERS.find(f => f.value === filter)!.steps.includes(stepOf(item)),
    sorts: { step: item => STEPS[stepOf(item)].order, code: item => item.documentCode, name: item => item.documentName, customer: item => item.customerName, amount: item => item.totalAmount },
    initial: { key: "step", dir: "asc" } });

  if (state.error) {
    return (
      <Alert variant="destructive">
        <AlertTitle>Không tải được danh sách</AlertTitle>
        <AlertDescription>{state.error}</AlertDescription>
      </Alert>
    );
  }

  const rows = list.rows;
  const choose = (value: Filter) => setFilter(value);

  return (
    <Card className="gap-0 border-border bg-card p-0 shadow-sm">
      <CardContent className="space-y-4 p-4">
        <Input type="search" aria-label="Tìm hợp đồng" placeholder="Tìm mã, tên hợp đồng hoặc khách hàng…" value={search} onChange={(event) => setSearch(event.target.value)} className="max-w-md" />
        <div role="group" aria-label="Lọc theo bước ký giấy" className="flex flex-wrap gap-2">
          {FILTERS.map((f) => {
            const selected = filter === f.value;
            return (
              <Button
                key={f.value}
                type="button"
                variant="outline"
                size="sm"
                aria-pressed={selected}
                onClick={() => choose(f.value)}
                className={cn("rounded-full", selected && "border-primary bg-primary/10 font-semibold text-primary hover:bg-primary/15")}
              >
                {f.label}
                {state.items && <span className="ml-1 tabular-nums text-muted-foreground">({counts[f.value]})</span>}
              </Button>
            );
          })}
        </div>

        {!state.items ? (
          <div className="space-y-3">
            {[0, 1, 2].map((index) => (
              <Skeleton key={index} className="h-12 w-full" />
            ))}
          </div>
        ) : list.total === 0 ? (
          <p className="py-12 text-center text-sm text-muted-foreground">
            {search ? "Không tìm thấy hợp đồng phù hợp. Thử đổi từ khóa." : filter === "todo"
              ? "Không có hợp đồng nào cần nộp hay kiểm tra bản ký giấy."
              : "Không có hợp đồng nào ở bước này."}
          </p>
        ) : (
          <>
            <div className="hidden md:block">
              <Table>
                <TableHeader>
                  <TableRow>
                    <SortHeader label="Mã hợp đồng" column="code" sort={list.sort} onSort={list.sortBy} className="h-10 px-2 text-left align-middle font-medium whitespace-nowrap"/>
                    <SortHeader label="Tên hợp đồng" column="name" sort={list.sort} onSort={list.sortBy} className="h-10 px-2 text-left align-middle font-medium whitespace-nowrap"/>
                    <SortHeader label="Khách hàng" column="customer" sort={list.sort} onSort={list.sortBy} className="h-10 px-2 text-left align-middle font-medium whitespace-nowrap"/>
                    <SortHeader label="Giá trị" column="amount" sort={list.sort} onSort={list.sortBy} className="h-10 px-2 text-left align-middle font-medium whitespace-nowrap text-right" firstDir="desc"/>
                    <SortHeader label="Bước ký giấy" column="step" sort={list.sort} onSort={list.sortBy} className="h-10 px-2 text-left align-middle font-medium whitespace-nowrap text-center"/>
                    <TableHead className="pr-4 text-right">Thao tác</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {rows.map((item) => (
                    <TableRow key={item.documentId} className="cursor-pointer" onClick={() => router.push(contractHref(item.documentId))}>
                      <TableCell className="font-semibold">{item.documentCode ?? "Chưa cấp mã"}</TableCell>
                      <TableCell className="max-w-64 whitespace-normal">{item.documentName}</TableCell>
                      <TableCell className="max-w-64 whitespace-normal">{item.customerName}</TableCell>
                      <TableCell className="text-right font-semibold text-primary tabular-nums">{formatMoney(item.totalAmount)}</TableCell>
                      <TableCell className="text-center">
                        <StepBadge item={item} />
                      </TableCell>
                      <TableCell className="text-right">
                        <Button asChild variant="outline" size="sm" onClick={(event) => event.stopPropagation()}>
                          <Link href={contractHref(item.documentId)}>
                            <Eye className="size-4" /> {stepOf(item) === "review" ? "Kiểm tra" : stepOf(item) === "accepted" ? "Xem" : "Nộp bản ký"}
                          </Link>
                        </Button>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </div>
            <ul className="space-y-3 md:hidden">
              {rows.map((item) => (
                <li key={item.documentId}>
                  <Link
                    href={contractHref(item.documentId)}
                    className="block rounded-xl border border-border bg-card p-4 shadow-sm active:bg-secondary/40"
                  >
                    <div className="mb-3 flex items-start justify-between gap-3">
                      <div className="min-w-0">
                        <p className="font-semibold text-foreground">{item.documentCode ?? "Chưa cấp mã"}</p>
                        <p className="mt-1 line-clamp-2 text-sm text-muted-foreground">{item.documentName}</p>
                      </div>
                      <StepBadge item={item} />
                    </div>
                    <div className="grid grid-cols-2 gap-2 rounded-lg border bg-muted/40 p-2.5 text-sm">
                      <div>
                        <p className="text-xs text-muted-foreground">Khách hàng</p>
                        <p className="truncate font-medium">{item.customerName}</p>
                      </div>
                      <div>
                        <p className="text-xs text-muted-foreground">Giá trị</p>
                        <p className="truncate font-semibold text-primary">{formatMoney(item.totalAmount)}</p>
                      </div>
                    </div>
                  </Link>
                </li>
              ))}
            </ul>
            <ListPager page={list.page} pageSize={list.pageSize} total={list.total} onPage={list.setPage} label="hợp đồng" />
          </>
        )}
      </CardContent>
    </Card>
  );
}

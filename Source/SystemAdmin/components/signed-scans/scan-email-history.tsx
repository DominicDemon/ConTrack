"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { SortHeader, useListView } from "@/components/internal/list-tools";
import { ListPager } from "@/components/scanned-contracts/list-pager";
import { Input } from "@/components/ui/input";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Skeleton } from "@/components/ui/skeleton";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { getScanErrorMessage } from "@/services/scans/http";
import { signedEvidenceApi, type ScanEmailHistoryItem } from "@/services/scans/signed-scans";
import { useScanAccess } from "@/components/internal/scan-access";
import { formatDateTime } from "./format";

/** Who sent which scan to whom: one's own sendings, or everyone's for those who answer for the archive. */
export function ScanEmailHistory() {
  const [rows, setRows] = useState<ScanEmailHistoryItem[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState(""); const [kind, setKind] = useState(""); const [result, setResult] = useState("");
  const list = useListView(rows ?? [], { search, fields: r => [r.sender, r.fileName, ...r.sent, ...r.failed], filterKey: [kind, result],
    filter: r => (!kind || r.kind === kind) && (!result || (result === "FAILED") === r.failed.length > 0),
    sorts: { sentAt: r => r.sentAt, sender: r => r.sender, file: r => r.fileName, to: r => r.sent.length }, initial: { key: "sentAt", dir: "desc" } });
  // The director oversees sendings without the scan screens: file names are shown without a link for them.
  const { can } = useScanAccess();
  const linkable = (kind: ScanEmailHistoryItem["kind"]) =>
    can(kind === "scan" ? "contract.scanned.view" : "contract.signed_evidence.view");

  useEffect(() => {
    signedEvidenceApi.emailHistory().then(setRows, (loadError: unknown) =>
      setError(getScanErrorMessage(loadError, "Không tải được lịch sử gửi email.")),
    );
  }, []);

  if (error)
    return (
      <Alert variant="destructive">
        <AlertTitle>Không tải được</AlertTitle>
        <AlertDescription>{error}</AlertDescription>
      </Alert>
    );
  if (!rows) return <Skeleton className="h-24 w-full" />;
  if (rows.length === 0) return <p className="text-sm text-muted-foreground">Chưa có bản scan nào được gửi qua email.</p>;

  return (
    <div className="space-y-4">
    <div className="flex flex-wrap gap-2">
      <Input type="search" aria-label="Tìm lần gửi" placeholder="Tìm người gửi, tên tệp hoặc email nhận…" value={search} onChange={(event) => setSearch(event.target.value)} className="max-w-md" />
      <select aria-label="Lọc loại tệp" className="h-9 rounded-md border bg-background px-3 text-sm" value={kind} onChange={(event) => setKind(event.target.value)}>
        <option value="">Bản ký giấy và bản scan</option><option value="evidence">Bản ký giấy</option><option value="scan">Bản scan hợp đồng giấy</option></select>
      <select aria-label="Lọc kết quả gửi" className="h-9 rounded-md border bg-background px-3 text-sm" value={result} onChange={(event) => setResult(event.target.value)}>
        <option value="">Mọi kết quả</option><option value="OK">Gửi được hết</option><option value="FAILED">Có địa chỉ lỗi</option></select>
    </div>
    {list.total === 0 ? <p className="py-12 text-center text-sm text-muted-foreground">Không tìm thấy lần gửi phù hợp. Thử đổi từ khóa hoặc bộ lọc.</p> : <>
    <Table>
      <TableHeader>
        <TableRow>
          <SortHeader label="Thời gian" column="sentAt" sort={list.sort} onSort={list.sortBy} className="h-10 px-2 text-left align-middle font-medium whitespace-nowrap" firstDir="desc"/>
          <SortHeader label="Người gửi" column="sender" sort={list.sort} onSort={list.sortBy} className="h-10 px-2 text-left align-middle font-medium whitespace-nowrap"/>
          <SortHeader label="Tệp" column="file" sort={list.sort} onSort={list.sortBy} className="h-10 px-2 text-left align-middle font-medium whitespace-nowrap"/>
          <SortHeader label="Đã gửi tới" column="to" sort={list.sort} onSort={list.sortBy} className="h-10 px-2 text-left align-middle font-medium whitespace-nowrap" firstDir="desc"/>
          <TableHead>Lỗi</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {list.rows.map((row, index) => (
          <TableRow key={`${row.sentAt}-${index}`}>
            <TableCell className="whitespace-nowrap text-sm">{formatDateTime(row.sentAt)}</TableCell>
            <TableCell className="text-sm">{row.sender}</TableCell>
            <TableCell className="max-w-[16rem] text-sm [overflow-wrap:anywhere]">
              {linkable(row.kind) ? (
                <Link
                  className="text-primary hover:underline"
                  href={row.kind === "scan" ? `/workspace/scanned-contracts/jobs/${row.targetId}` : `/workspace/signed-contracts`}
                >
                  {row.fileName}
                </Link>
              ) : (
                row.fileName
              )}
              {row.withNote && <span className="block text-xs text-muted-foreground">có lời nhắn</span>}
            </TableCell>
            <TableCell className="text-sm [overflow-wrap:anywhere]">{row.sent.join(", ") || "—"}</TableCell>
            <TableCell className="text-sm text-destructive [overflow-wrap:anywhere]">{row.failed.join(", ") || ""}</TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
    <ListPager page={list.page} pageSize={list.pageSize} total={list.total} onPage={list.setPage} label="lần gửi" /></>}
    </div>
  );
}

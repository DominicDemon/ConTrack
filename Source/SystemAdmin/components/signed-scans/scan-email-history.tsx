"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Skeleton } from "@/components/ui/skeleton";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { getScanErrorMessage } from "@/services/scans/http";
import { signedEvidenceApi, type ScanEmailHistoryItem } from "@/services/scans/signed-scans";
import { formatDateTime } from "./format";

/** Who sent which scan to whom: one's own sendings, or everyone's for those who answer for the archive. */
export function ScanEmailHistory() {
  const [rows, setRows] = useState<ScanEmailHistoryItem[] | null>(null);
  const [error, setError] = useState<string | null>(null);

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
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>Thời gian</TableHead>
          <TableHead>Người gửi</TableHead>
          <TableHead>Tệp</TableHead>
          <TableHead>Đã gửi tới</TableHead>
          <TableHead>Lỗi</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {rows.map((row, index) => (
          <TableRow key={`${row.sentAt}-${index}`}>
            <TableCell className="whitespace-nowrap text-sm">{formatDateTime(row.sentAt)}</TableCell>
            <TableCell className="text-sm">{row.sender}</TableCell>
            <TableCell className="max-w-[16rem] text-sm [overflow-wrap:anywhere]">
              <Link
                className="text-primary hover:underline"
                href={row.kind === "scan" ? `/workspace/scanned-contracts/jobs/${row.targetId}` : `/workspace/signed-contracts`}
              >
                {row.fileName}
              </Link>
              {row.withNote && <span className="block text-xs text-muted-foreground">có lời nhắn</span>}
            </TableCell>
            <TableCell className="text-sm [overflow-wrap:anywhere]">{row.sent.join(", ") || "—"}</TableCell>
            <TableCell className="text-sm text-destructive [overflow-wrap:anywhere]">{row.failed.join(", ") || ""}</TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
}

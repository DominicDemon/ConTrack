"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { Eye } from "lucide-react";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { getScanErrorMessage } from "@/services/scans/http";
import { signedEvidenceApi, type ContractListItem } from "@/services/scans/signed-scans";
import { formatMoney } from "./format";
import { ContractStatusBadge, EvidenceStatusBadge } from "./status-badge";

export const contractHref = (documentId: string) => `/workspace/signed-contracts/${documentId}`;

function PaperStatus({ item }: { item: ContractListItem }) {
  if (item.signedOnline) return <span className="text-xs text-muted-foreground">Ký online</span>;
  if (!item.paperEvidenceStatus) return <span className="text-xs text-muted-foreground">—</span>;
  return <EvidenceStatusBadge status={item.paperEvidenceStatus} />;
}

/** Contracts as the old contract list shows them, with the state of their paper signing. */
export function ContractList() {
  const router = useRouter();
  const [state, setState] = useState<{ items: ContractListItem[] | null; error: string | null }>({ items: null, error: null });

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

  if (state.error) {
    return (
      <Alert variant="destructive">
        <AlertTitle>Không tải được danh sách</AlertTitle>
        <AlertDescription>{state.error}</AlertDescription>
      </Alert>
    );
  }

  const { items } = state;
  return (
    <Card className="gap-0 border-border bg-card p-0 shadow-sm">
      <CardContent className="p-4">
        {!items ? (
          <div className="space-y-3">
            {[0, 1, 2].map((index) => (
              <Skeleton key={index} className="h-12 w-full" />
            ))}
          </div>
        ) : items.length === 0 ? (
          <p className="py-12 text-center text-sm text-muted-foreground">Chưa có hợp đồng nào.</p>
        ) : (
          <>
            <div className="hidden md:block">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Mã hợp đồng</TableHead>
                    <TableHead>Tên hợp đồng</TableHead>
                    <TableHead>Khách hàng</TableHead>
                    <TableHead className="text-right">Giá trị</TableHead>
                    <TableHead className="text-center">Trạng thái</TableHead>
                    <TableHead className="text-center">Bản ký giấy</TableHead>
                    <TableHead className="pr-4 text-right">Thao tác</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {items.map((item) => (
                    <TableRow key={item.documentId} className="cursor-pointer" onClick={() => router.push(contractHref(item.documentId))}>
                      <TableCell className="font-semibold">{item.documentCode ?? "Chưa cấp mã"}</TableCell>
                      <TableCell className="max-w-64 whitespace-normal">{item.documentName}</TableCell>
                      <TableCell className="max-w-64 whitespace-normal">{item.customerName}</TableCell>
                      <TableCell className="text-right font-semibold text-primary tabular-nums">{formatMoney(item.totalAmount)}</TableCell>
                      <TableCell className="text-center">
                        <ContractStatusBadge status={item.contractStatus} />
                      </TableCell>
                      <TableCell className="text-center">
                        <PaperStatus item={item} />
                      </TableCell>
                      <TableCell className="text-right">
                        <Button asChild variant="outline" size="sm" onClick={(event) => event.stopPropagation()}>
                          <Link href={contractHref(item.documentId)}>
                            <Eye className="size-4" /> Chi tiết
                          </Link>
                        </Button>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </div>
            <ul className="space-y-3 md:hidden">
              {items.map((item) => (
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
                      <ContractStatusBadge status={item.contractStatus} />
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
                      <div className="col-span-2 flex items-center justify-between border-t pt-2">
                        <p className="text-xs text-muted-foreground">Bản ký giấy</p>
                        <PaperStatus item={item} />
                      </div>
                    </div>
                  </Link>
                </li>
              ))}
            </ul>
          </>
        )}
      </CardContent>
    </Card>
  );
}

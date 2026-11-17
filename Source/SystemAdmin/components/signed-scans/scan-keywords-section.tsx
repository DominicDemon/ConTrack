"use client";

import { Fragment, useId, useState } from "react";
import { AlertCircle, Loader2, Plus, RefreshCw, ScanText, X } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Progress } from "@/components/ui/progress";
import { Skeleton } from "@/components/ui/skeleton";
import { cn } from "@/lib/utils";
import type { AddScanKeywordInput, ScanKeyword, ScanKeywordState } from "@/services/scans/signed-scans";
import { AddKeywordDialog } from "./add-keyword-dialog";
import {
  formatDateTime,
  formatKeywordValue,
  KEYWORD_JOB_STATUS,
  KEYWORD_TYPE_LABELS,
  KEYWORD_TYPES,
  keywordErrorMessage,
} from "./format";
import { StatusBadge } from "./status-badge";

/** The API tries a scan this many times before it reports the reading failed (ScanKeywordJobRunner.MaxAttempts). */
const SCAN_READ_ATTEMPTS = 3;

interface ScanKeywordsSectionProps {
  keywords: ScanKeywordState;
  onRetry: () => Promise<void>;
  /** Resolves true when the keyword was saved, so the dialog can close. */
  onAddKeyword: (input: AddScanKeywordInput) => Promise<boolean>;
  onRemoveKeyword: (keyword: ScanKeyword) => Promise<void>;
}

export function ScanKeywordsSection({
  keywords,
  onRetry,
  onAddKeyword,
  onRemoveKeyword,
}: ScanKeywordsSectionProps) {
  const status = KEYWORD_JOB_STATUS[keywords.status];
  const titleId = useId();
  const [adding, setAdding] = useState(false);
  const [retrying, setRetrying] = useState(false);
  const [removingId, setRemovingId] = useState<string | null>(null);

  const reading = keywords.status === "Pending" || keywords.status === "Processing";
  const readCount = keywords.items.filter((item) => item.source === "Ocr").length;
  const pageCount = keywords.pageCount ?? 0;
  const pagesRead = keywords.pagesRead ?? 0;

  const retry = async () => {
    setRetrying(true);
    try {
      await onRetry();
    } finally {
      setRetrying(false);
    }
  };

  const remove = async (keyword: ScanKeyword) => {
    setRemovingId(keyword.keywordId);
    try {
      await onRemoveKeyword(keyword);
    } finally {
      setRemovingId(null);
    }
  };

  return (
    <Card aria-labelledby={titleId} role="region">
      <CardHeader className="flex flex-row flex-wrap items-center justify-between gap-3">
        <CardTitle id={titleId} className="flex flex-wrap items-center gap-2">
          <ScanText className="size-5 text-primary" />
          Từ khóa tìm kiếm
          <StatusBadge tone={status.tone}>{status.label}</StatusBadge>
        </CardTitle>
        {keywords.status === "Done" && (
          <Button type="button" variant="outline" size="sm" onClick={() => setAdding(true)}>
            <Plus aria-hidden="true" />
            Thêm từ khóa
          </Button>
        )}
      </CardHeader>
      <CardContent className="space-y-3">

      {keywords.status === "Pending" && (
        <p role="status" className="text-muted-foreground">
          Đang chờ máy chủ đọc bản quét…
        </p>
      )}

      {keywords.status === "Processing" &&
        (pageCount > 0 ? (
          <div role="status" className="space-y-2">
            <p className="text-muted-foreground">
              Đang đọc trang {Math.min(pagesRead + 1, pageCount)}/{pageCount}…
            </p>
            <Progress value={(pagesRead / pageCount) * 100} aria-label="Tiến độ đọc từ khóa" />
          </div>
        ) : (
          <p role="status" className="flex items-center gap-2 text-muted-foreground">
            <Loader2 className="size-4 animate-spin" aria-hidden="true" />
            Máy chủ đang đọc bản quét bằng OCR, mỗi trang mất vài giây…
          </p>
        ))}

      {keywords.status === "Done" && (
        <p className="text-muted-foreground">
          Đã đọc {pagesRead}/{pageCount} trang · {readCount} từ khóa
          {keywords.completedAt ? ` · xong lúc ${formatDateTime(keywords.completedAt)}` : ""}
        </p>
      )}

      {keywords.status === "Failed" && (
        <div
          role="alert"
          className="flex gap-3 rounded-lg border border-red-200 bg-red-50 p-4 dark:border-red-500/30 dark:bg-red-500/10"
        >
          <AlertCircle className="mt-0.5 size-5 shrink-0 text-red-700 dark:text-red-400" aria-hidden="true" />
          <div className="min-w-0 space-y-1.5">
            <p className="font-semibold text-red-800 dark:text-red-300">Chưa đọc được từ khóa</p>
            <p>
              {keywordErrorMessage(keywords.lastErrorCode)} sau {SCAN_READ_ATTEMPTS} lần thử
              {keywords.lastErrorCode && (
                <>
                  {" "}
                  (mã lỗi <code className="font-mono text-sm">{keywords.lastErrorCode}</code>)
                </>
              )}
              . Bản quét vẫn được lưu đầy đủ, chỉ chưa tìm kiếm được theo từ khóa.
            </p>
            <p className="text-sm text-muted-foreground">Nếu lỗi lặp lại, hãy báo quản trị hệ thống.</p>
            <div className="flex flex-wrap gap-3 pt-1.5">
              <Button type="button" onClick={retry} disabled={retrying}>
                {retrying ? (
                  <Loader2 className="animate-spin" aria-hidden="true" />
                ) : (
                  <RefreshCw aria-hidden="true" />
                )}
                Đọc lại
              </Button>
              <Button type="button" variant="outline" onClick={() => setAdding(true)}>
                Thêm từ khóa bằng tay
              </Button>
            </div>
          </div>
        </div>
      )}

      {reading ? (
        <div aria-hidden="true" className="grid grid-cols-[8.5rem_minmax(0,1fr)] items-center gap-x-4 gap-y-3">
          {KEYWORD_TYPES.map((type) => (
            <Fragment key={type}>
              <span className="text-muted-foreground">{KEYWORD_TYPE_LABELS[type]}</span>
              <Skeleton className="h-7 w-40" />
            </Fragment>
          ))}
        </div>
      ) : (
        (keywords.status === "Done" || keywords.items.length > 0) && (
          <dl className="grid grid-cols-[8.5rem_minmax(0,1fr)] items-baseline gap-x-4 gap-y-2.5">
            {KEYWORD_TYPES.map((type) => {
              const items = keywords.items.filter((item) => item.type === type);
              return (
                <Fragment key={type}>
                  <dt className="text-muted-foreground">{KEYWORD_TYPE_LABELS[type]}</dt>
                  <dd className="flex flex-wrap gap-2">
                    {items.length === 0 && <span className="text-muted-foreground">Chưa có</span>}
                    {items.map((item) => {
                      const shown = formatKeywordValue(type, item.value);
                      const manual = item.source === "Manual";
                      return (
                        <span
                          key={item.keywordId}
                          className={cn(
                            "inline-flex items-center gap-1.5 rounded-md border bg-background py-0.5 pl-2.5 text-sm tabular-nums",
                            manual ? "pr-1" : "pr-2.5",
                          )}
                        >
                          <span className="font-medium">{shown}</span>
                          <span className="text-xs text-muted-foreground">
                            {item.page ? `trang ${item.page}` : "thêm tay"}
                          </span>
                          {manual && (
                            <Button
                              type="button"
                              variant="ghost"
                              size="icon-sm"
                              className="size-6"
                              aria-label={`Xóa từ khóa ${shown}`}
                              disabled={removingId === item.keywordId}
                              onClick={() => remove(item)}
                            >
                              <X aria-hidden="true" />
                            </Button>
                          )}
                        </span>
                      );
                    })}
                  </dd>
                </Fragment>
              );
            })}
          </dl>
        )
      )}

      {keywords.status === "Done" && (
        <p className="text-sm text-muted-foreground">
          Từ khóa được đọc tự động nên có thể thiếu khi bản quét mờ hoặc có chữ viết tay. Khi đó hãy thêm bằng tay.
        </p>
      )}

      <AddKeywordDialog open={adding} onOpenChange={setAdding} onSubmit={onAddKeyword} />
      </CardContent>
    </Card>
  );
}

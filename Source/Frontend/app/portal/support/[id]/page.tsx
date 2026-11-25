"use client";

import { use, useCallback, useEffect, useState } from "react";
import { onNotificationRefresh } from "../../../../../Shared/notification-navigation";
import { useRouter } from "next/navigation";
import {
  ArrowLeft,
  Building2,
  Calendar,
  CheckCircle2,
  Clock,
  Layers,
  Loader2,
  UserCheck,
  X,
  ZoomIn,
  ZoomOut,
} from "lucide-react";
import { toast } from "@/components/ui/sonner";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import TicketReopen from "@/components/portal/ticket-reopen";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import PortalHeader from "@/components/portal/portal-header";
import { cn } from "@/lib/utils";
import {
  customerTicketApi,
  TicketDto,
  TicketStatus,
} from "@/services/portal-ticket-api";

function formatZaloTime(dateStr: string) {
  const d = new Date(dateStr);
  if (Number.isNaN(d.getTime())) return "--:--";
  const hm = d.toLocaleTimeString("vi-VN", {
    hour: "2-digit",
    minute: "2-digit",
    hour12: false,
  });
  const dm = d.toLocaleDateString("vi-VN", {
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
  });
  return `${hm} • ${dm}`;
}

export default function CustomerSupportTicketDetailPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = use(params);
  const ticketId = Number(id);
  const router = useRouter();

  const [ticket, setTicket] = useState<TicketDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [previewImage, setPreviewImage] = useState<string | null>(null);
  const [zoomScale, setZoomScale] = useState(1);

  const loadDetail = useCallback(
    async (silent = false) => {
      if (!silent) setLoading(true);
      try {
        const data = await customerTicketApi.getById(ticketId);
        if (data) {
          setTicket(data);
        }
      } catch {
        if (!silent) {
          toast.error("Không thể tải chi tiết yêu cầu hỗ trợ");
        }
      } finally {
        if (!silent) setLoading(false);
      }
    },
    [ticketId],
  );

  useEffect(() => {
    void loadDetail(false);
  }, [loadDetail]);

  useEffect(() => onNotificationRefresh(() => void loadDetail(true)), [loadDetail]);

  useEffect(() => {
    const timer = setInterval(() => {
      void loadDetail(true);
    }, 2000);
    return () => clearInterval(timer);
  }, [loadDetail]);

  if (loading) {
    return (
      <>
        <PortalHeader />
        <div className="grow flex items-center justify-center p-10">
          <Loader2 className="size-6 animate-spin text-primary" />
          <span className="ml-2 text-sm text-muted-foreground">
            Đang tải thông tin yêu cầu hỗ trợ...
          </span>
        </div>
      </>
    );
  }

  if (!ticket) {
    return (
      <>
        <PortalHeader />
        <div className="grow p-2 lg:p-10">
          <Card className="mx-auto max-w-xl border-dashed">
            <CardContent className="py-12 text-center">
              <p className="font-semibold text-foreground">
                Không tìm thấy yêu cầu hỗ trợ hoặc bạn không có quyền truy cập
              </p>
              <Button
                className="mt-4"
                variant="outline"
                onClick={() => router.push("/portal/support")}
              >
                <ArrowLeft className="mr-2 size-4" />
                Quay lại danh sách
              </Button>
            </CardContent>
          </Card>
        </div>
      </>
    );
  }

  const isResolved = ticket.ticketStatus === TicketStatus.Resolved;
  const serviceLabel = ticket.serviceName || ticket.module || "Dịch vụ chung";
  const initialImages =
    ticket.attachmentUrls && ticket.attachmentUrls.length > 0
      ? ticket.attachmentUrls.filter(
          (u) =>
            u &&
            (u.startsWith("data:image") ||
              u.startsWith("http") ||
              u.startsWith("/")),
        )
 : [];

  return (
    <>
      <PortalHeader />

      <div className="grow overflow-y-auto p-2 lg:p-10 space-y-6">
        <div className="space-y-6">
          <div className="flex flex-col gap-4 md:flex-row md:items-start md:justify-between">
            <div>
              <Button
                variant="ghost"
                className="-ml-3 mb-2 text-muted-foreground"
                onClick={() => router.push("/portal/support")}
              >
                <ArrowLeft className="size-4 mr-2" />
                Quay lại danh sách yêu cầu
              </Button>
              <div className="flex flex-wrap items-center gap-2.5">
                <h1 className="text-2xl font-bold tracking-tight text-foreground font-mono">
                  {ticket.ticketCode}
                </h1>
                <Badge
                  variant="outline"
                  className={cn(
                    "font-semibold",
                    isResolved
                      ? "border-emerald-200 bg-emerald-50 text-emerald-700"
                      : ticket.ticketStatus === TicketStatus.InProgress
                        ? "border-sky-200 bg-sky-50 text-sky-700"
                        : "border-amber-200 bg-amber-50 text-amber-700",
                  )}
                >
                  {isResolved
                    ? "Đã đóng / Hoàn tất"
                    : ticket.ticketStatus === TicketStatus.InProgress
                      ? "Đang xử lý"
                      : ticket.ticketStatus === TicketStatus.PendingHeadApproval
                        ? "Chờ phản hồi"
                        : "Chờ xử lý"}
                </Badge>
                <Badge variant="secondary" className="font-medium">
                  Dịch vụ: {serviceLabel}
                </Badge>
              </div>
              <p className="mt-2 max-w-3xl text-base font-medium text-foreground">
                {ticket.ticketSubject}
              </p>
            </div>

          </div>

          {ticket.ticketResolution && (
            <div className="flex items-start gap-3 rounded-xl border border-emerald-200 bg-emerald-50/90 p-4 text-emerald-950 shadow-xs dark:border-emerald-500/30 dark:bg-emerald-950/30 dark:text-emerald-200">
              <CheckCircle2 className="mt-0.5 size-5 shrink-0 text-emerald-600" />
              <div className="space-y-1 text-sm">
                <p className="font-bold">
                  Phản hồi kết quả chính thức từ Trưởng phòng {ticket.department}
                  :
                </p>
                <p className="whitespace-pre-line leading-relaxed">
                  {ticket.ticketResolution}
                </p>
              </div>
            </div>
          )}

          {isResolved && <TicketReopen ticketId={ticket.ticketSupportId} onReopened={setTicket} />}

            <div className="grid grid-cols-1 gap-5 md:grid-cols-3">
              <Card className="md:col-span-2 shadow-xs">
                <CardHeader className="border-b pb-3">
                  <CardTitle className="text-base font-bold">
                    Nội dung câu hỏi & Hình ảnh minh họa ban đầu
                  </CardTitle>
                </CardHeader>
                <CardContent className="space-y-4 pt-4">
                  <div className="rounded-lg border bg-slate-50/70 p-4 text-sm leading-relaxed whitespace-pre-line dark:bg-slate-900/50">
                    {ticket.ticketDescription}
                  </div>

                  {initialImages.length > 0 && (
                    <div className="space-y-2">
                      <p className="text-xs font-semibold text-muted-foreground">
                        Hình ảnh minh họa đính kèm (Bấm vào ảnh để phóng to):
                      </p>
                      <div className="grid grid-cols-2 gap-3 sm:grid-cols-3">
                        {initialImages.map((imgUrl, idx) => (
                          <button
                            key={idx}
                            type="button"
                            onClick={() => {
                              setPreviewImage(imgUrl);
                              setZoomScale(1);
                            }}
                            className="group relative h-40 overflow-hidden rounded-xl border bg-slate-950/5 cursor-zoom-in"
                          >
                            <img
                              src={imgUrl}
                              alt={`Ảnh minh họa ${idx + 1}`}
                              className="h-full w-full object-cover transition-transform duration-200 group-hover:scale-105"
                            />
                            <span className="absolute inset-0 flex items-center justify-center bg-black/30 opacity-0 transition-opacity group-hover:opacity-100">
                              <span className="inline-flex items-center gap-1 rounded-full bg-black/75 px-3 py-1 text-xs font-medium text-white">
                                <ZoomIn className="size-3.5" />
                                Xem ảnh to
                              </span>
                            </span>
                          </button>
                        ))}
                      </div>
                    </div>
                  )}
                </CardContent>
              </Card>

              <Card className="shadow-xs">
                <CardHeader className="border-b pb-3">
                  <CardTitle className="text-base font-bold">
                    Thông tin phiếu hỗ trợ
                  </CardTitle>
                </CardHeader>
                <CardContent className="space-y-4 pt-4 text-sm">
                  <div className="flex items-start gap-3">
                    <Layers className="mt-0.5 size-4 shrink-0 text-teal-600" />
                    <div>
                      <p className="text-xs text-muted-foreground">Dịch vụ</p>
                      <p className="font-semibold text-foreground">
                        {serviceLabel}
                      </p>
                    </div>
                  </div>

                  <div className="flex items-start gap-3">
                    <Building2 className="mt-0.5 size-4 shrink-0 text-teal-600" />
                    <div>
                      <p className="text-xs text-muted-foreground">
                        Phòng ban tiếp nhận
                      </p>
                      <p className="font-semibold text-foreground">
                        {ticket.department}
                      </p>
                    </div>
                  </div>

                  <div className="flex items-start gap-3">
                    <UserCheck className="mt-0.5 size-4 shrink-0 text-teal-600" />
                    <div>
                      <p className="text-xs text-muted-foreground">
                        Trưởng phòng phụ trách
                      </p>
                      <p className="font-semibold text-foreground">
                        {ticket.departmentHeadName || "Đang điều phối"}
                      </p>
                    </div>
                  </div>

                  <div className="flex items-start gap-3">
                    <Calendar className="mt-0.5 size-4 shrink-0 text-teal-600" />
                    <div>
                      <p className="text-xs text-muted-foreground">
                        Thời gian gửi
                      </p>
                      <p className="font-medium text-foreground">
                        {formatZaloTime(ticket.createdAt)}
                      </p>
                    </div>
                  </div>

                  {ticket.ticketResolvedAt && (
                    <div className="flex items-start gap-3">
                      <Clock className="mt-0.5 size-4 shrink-0 text-emerald-600" />
                      <div>
                        <p className="text-xs text-muted-foreground">
                          Thời gian hoàn tất
                        </p>
                        <p className="font-medium text-emerald-600">
                          {formatZaloTime(ticket.ticketResolvedAt)}
                        </p>
                      </div>
                    </div>
                  )}
                </CardContent>
              </Card>
            </div>
        </div>
      </div>

      {previewImage && (
        <div
          onClick={() => setPreviewImage(null)}
          className="fixed inset-0 z-50 flex flex-col items-center justify-center bg-black/90 p-4 backdrop-blur-xs"
        >
          <div
            onClick={(e) => e.stopPropagation()}
            className="mb-3 flex items-center gap-2 rounded-full border border-white/15 bg-black/70 px-4 py-1.5 text-white shadow-lg"
          >
            <button
              type="button"
              onClick={() => setZoomScale((s) => Math.max(0.5, s - 0.25))}
              className="rounded-full p-1.5 hover:bg-white/15"
              title="Thu nhỏ"
            >
              <ZoomOut className="size-4" />
            </button>
            <span className="min-w-12 text-center text-xs font-semibold">
              {Math.round(zoomScale * 100)}%
            </span>
            <button
              type="button"
              onClick={() => setZoomScale((s) => Math.min(3, s + 0.25))}
              className="rounded-full p-1.5 hover:bg-white/15"
              title="Phóng to"
            >
              <ZoomIn className="size-4" />
            </button>
            <div className="mx-1 h-4 w-px bg-white/20" />
            <button
              type="button"
              onClick={() => setPreviewImage(null)}
              className="rounded-full p-1.5 text-rose-400 hover:bg-white/15"
              title="Đóng"
            >
              <X className="size-4" />
            </button>
          </div>

          <div
            onClick={(e) => e.stopPropagation()}
            className="flex max-h-[85vh] max-w-[92vw] items-center justify-center overflow-auto rounded-lg"
          >
            <img
              src={previewImage}
              alt="Xem ảnh phóng to"
              style={{ transform: `scale(${zoomScale})` }}
              className="max-h-[82vh] max-w-[90vw] rounded-lg object-contain transition-transform duration-150"
            />
          </div>
        </div>
      )}
    </>
  );
}

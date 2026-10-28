"use client";

import { useCallback, useEffect, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import {
  ArrowLeft,
  Building2,
  CheckCircle2,
  Clock,
  Copy,
  Loader2,
  MessageSquare,
  Paperclip,
  RefreshCw,
  Save,
  Send,
  ShieldCheck,
  User,
} from "lucide-react";
import { toast } from "@/components/ui/sonner";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Textarea } from "@/components/ui/textarea";
import { Header } from "@/components/ui/custom/header";
import { cn } from "@/lib/utils";
import {
  ticketApi,
  TicketDto,
  TicketPriority,
  TicketStatus,
  TICKET_PRIORITY_LABELS,
  TICKET_STATUS_LABELS,
} from "@/services/ticket-api";

const QUICK_RESPONSES = [
  "Chào anh/chị, bộ phận kỹ thuật ConTrack đã ghi nhận sự cố và đang tiến hành kiểm tra.",
  "Sự cố đã được đội ngũ kỹ thuật xử lý xong trên hệ thống. Anh/chị vui lòng kiểm tra lại giúp em nhé.",
  "Anh/chị vui lòng cung cấp thêm ảnh chụp màn hình thông báo lỗi hoặc mã hợp đồng gặp vấn đề để bên em hỗ trợ.",
  "Bộ phận Kế toán đã xuất hóa đơn điện tử và gửi về email quản trị của công ty mình rồi ạ.",
];

function TicketStatusBadge({ status }: { status: TicketStatus }) {
  const config: Record<TicketStatus, { label: string; className: string }> = {
    [TicketStatus.Open]: {
      label: "Chờ tiếp nhận",
      className:
        "bg-amber-50 text-amber-700 border-amber-200 dark:bg-amber-500/10 dark:text-amber-400 dark:border-amber-500/20",
    },
    [TicketStatus.InProgress]: {
      label: "Đang xử lý",
      className:
        "bg-cyan-50 text-cyan-700 border-cyan-200 dark:bg-cyan-500/10 dark:text-cyan-400 dark:border-cyan-500/20",
    },
    [TicketStatus.Resolved]: {
      label: "Đã xử lý",
      className:
        "bg-emerald-50 text-emerald-700 border-emerald-200 dark:bg-emerald-500/10 dark:text-emerald-400 dark:border-emerald-500/20",
    },
  };
  const current = config[status] ?? config[TicketStatus.Open];
  return (
    <Badge variant="outline" className={cn("font-medium", current.className)}>
      {current.label}
    </Badge>
  );
}

export default function AdminSupportTicketDetailPage() {
  const params = useParams<{ id: string }>();
  const router = useRouter();
  const ticketId = Number(params?.id);

  const [ticket, setTicket] = useState<TicketDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [replyMessage, setReplyMessage] = useState("");
  const [sendingReply, setSendingReply] = useState(false);
  const [statusUpdate, setStatusUpdate] = useState<TicketStatus>(
    TicketStatus.Open,
  );
  const [resolutionText, setResolutionText] = useState("");
  const [updatingStatus, setUpdatingStatus] = useState(false);

  const loadTicket = useCallback(async () => {
    if (!Number.isFinite(ticketId) || ticketId <= 0) return;
    setLoading(true);
    try {
      const data = await ticketApi.getById(ticketId);
      setTicket(data);
      if (data) {
        setStatusUpdate(data.ticketStatus);
        setResolutionText(data.ticketResolution || "");
      }
    } catch {
      toast.error("Không thể tải thông tin chi tiết ticket hỗ trợ");
    } finally {
      setLoading(false);
    }
  }, [ticketId]);

  useEffect(() => {
    void loadTicket();
  }, [loadTicket]);

  useEffect(() => {
    const handleRealtimeUpdate = () => {
      void loadTicket();
    };
    window.addEventListener(
      "admin-support-ticket-updated",
      handleRealtimeUpdate,
    );
    return () => {
      window.removeEventListener(
        "admin-support-ticket-updated",
        handleRealtimeUpdate,
      );
    };
  }, [loadTicket]);

  const handleCopy = (text: string, label: string) => {
    navigator.clipboard.writeText(text);
    toast.success(`Đã sao chép ${label}: ${text}`);
  };

  const handleSendReply = async () => {
    if (!ticket || !replyMessage.trim()) return;

    setSendingReply(true);
    try {
      const newMsg = await ticketApi.replyMessage(ticket.ticketSupportId, {
        messageContent: replyMessage.trim(),
      });
      toast.success("Đã gửi phản hồi tới Web & App Mobile của Tenant");
      setTicket((prev) => {
        if (!prev) return null;
        const nextStatus =
          prev.ticketStatus === TicketStatus.Open
            ? TicketStatus.InProgress
            : prev.ticketStatus;
        setStatusUpdate(nextStatus);
        return {
          ...prev,
          ticketStatus: nextStatus,
          messages: [...(prev.messages || []), newMsg],
        };
      });
      setReplyMessage("");
    } catch {
      toast.error("Không thể gửi phản hồi. Vui lòng thử lại.");
    } finally {
      setSendingReply(false);
    }
  };

  const handleUpdateStatus = async () => {
    if (!ticket) return;

    setUpdatingStatus(true);
    try {
      await ticketApi.updateStatus(ticket.ticketSupportId, {
        status: statusUpdate,
        resolution: resolutionText.trim(),
      });
      toast.success("Đã lưu cập nhật trạng thái xử lý yêu cầu");
      setTicket((prev) => {
        if (!prev) return null;
        return {
          ...prev,
          ticketStatus: statusUpdate,
          ticketResolution: resolutionText.trim(),
        };
      });
    } catch {
      toast.error("Không thể cập nhật trạng thái.");
    } finally {
      setUpdatingStatus(false);
    }
  };

  return (
    <>
      <Header title="Xử lý & Phản hồi Ticket" />

      <div className="grow overflow-y-auto p-2 lg:p-10 space-y-6">
        {/* Page Header chuẩn */}
        <div className="flex flex-col gap-4 md:flex-row md:items-start md:justify-between">
          <div>
            <Button
              variant="ghost"
              className="-ml-3 mb-2 text-muted-foreground"
              onClick={() => router.push("/support-tickets")}
            >
              <ArrowLeft className="size-4 mr-2" />
              Quay lại danh sách
            </Button>
            <div className="flex flex-wrap items-center gap-2">
              <h1 className="text-2xl font-bold tracking-tight text-foreground font-mono">
                {ticket ? ticket.ticketCode : "Đang tải..."}
              </h1>
              {ticket && <TicketStatusBadge status={ticket.ticketStatus} />}
            </div>
            {ticket && (
              <p className="mt-2 max-w-3xl text-sm text-muted-foreground">
                {ticket.ticketSubject}
              </p>
            )}
          </div>

          <div className="flex flex-wrap items-center gap-2">
            <Button
              variant="outline"
              size="sm"
              onClick={() => void loadTicket()}
              disabled={loading}
              className="h-9 gap-1.5"
            >
              <RefreshCw className={cn("size-4", loading && "animate-spin")} />
              <span>Làm mới</span>
            </Button>
            <Button
              size="sm"
              onClick={handleUpdateStatus}
              disabled={updatingStatus || loading || !ticket}
              className="h-9 gap-1.5 shadow-sm"
            >
              {updatingStatus ? (
                <Loader2 className="size-4 animate-spin" />
              ) : (
                <Save className="size-4" />
              )}
              <span>Lưu cập nhật trạng thái</span>
            </Button>
          </div>
        </div>

        {loading && !ticket ? (
          <Card className="border-border shadow-sm p-12 text-center text-sm text-muted-foreground">
            <Loader2 className="size-6 animate-spin mx-auto mb-2 text-primary" />
            Đang tải dữ liệu bàn làm việc xử lý Ticket...
          </Card>
        ) : !ticket ? (
          <Card className="border-border shadow-sm p-12 text-center space-y-3">
            <p className="text-sm text-muted-foreground">
              Không tìm thấy yêu cầu hỗ trợ này.
            </p>
            <Button
              variant="outline"
              size="sm"
              onClick={() => router.push("/support-tickets")}
            >
              Quay lại danh sách
            </Button>
          </Card>
        ) : (
          <div className="grid grid-cols-1 items-start gap-6 lg:grid-cols-12">
            {/* Cột trái (8/12): Luồng hội thoại trao đổi 2 chiều & phản hồi */}
            <div className="space-y-6 lg:col-span-7 xl:col-span-8">
              <Card className="border-border shadow-sm">
                <CardHeader className="border-b pb-4">
                  <div className="flex flex-wrap items-center justify-between gap-2">
                    <div className="flex items-center gap-2.5">
                      <div className="flex size-9 items-center justify-center rounded-lg bg-primary/10 text-primary">
                        <MessageSquare className="size-5" />
                      </div>
                      <div>
                        <CardTitle className="text-base font-semibold">
                          Lịch sử trao đổi với Tenant (Web & App Mobile)
                        </CardTitle>
                        <CardDescription className="text-xs">
                          Phản hồi trực tiếp cho khách hàng trên cả Web và ứng
                          dụng di động
                        </CardDescription>
                      </div>
                    </div>

                    <div className="flex items-center gap-2">
                      <Badge variant="outline" className="text-xs font-normal">
                        {ticket.department}
                      </Badge>
                      <Badge
                        variant="outline"
                        className={cn(
                          "text-xs font-medium",
                          ticket.ticketPriority === TicketPriority.Urgent &&
                            "border-rose-500/30 bg-rose-500/10 text-rose-600",
                        )}
                      >
                        Ưu tiên: {TICKET_PRIORITY_LABELS[ticket.ticketPriority]}
                      </Badge>
                    </div>
                  </div>
                </CardHeader>

                <CardContent className="space-y-4 pt-5 max-h-[480px] overflow-y-auto">
                  {ticket.messages?.map((msg) => (
                    <div
                      key={msg.ticketMessageId}
                      className={cn(
                        "rounded-xl border p-4 text-sm max-w-[88%]",
                        msg.isFromEmployee
                          ? "ml-auto bg-primary/5 border-primary/20 text-foreground"
                          : "mr-auto bg-secondary/50 border-border text-foreground",
                      )}
                    >
                      <div className="flex items-center justify-between gap-4 mb-1.5">
                        <span className="font-semibold text-xs flex items-center gap-1.5 text-primary">
                          {msg.isFromEmployee ? (
                            <>
                              <ShieldCheck className="size-4" />
                              <span>{msg.senderName} (Admin)</span>
                            </>
                          ) : (
                            <>
                              <User className="size-4 text-muted-foreground" />
                              <span>{msg.senderName}</span>
                            </>
                          )}
                        </span>
                        <span className="text-[11px] text-muted-foreground">
                          {new Date(msg.createdAt).toLocaleString("vi-VN", {
                            day: "2-digit",
                            month: "2-digit",
                            year: "numeric",
                            hour: "2-digit",
                            minute: "2-digit",
                          })}
                        </span>
                      </div>
                      <p className="whitespace-pre-wrap leading-relaxed">
                        {msg.ticketMessageContent}
                      </p>
                      {msg.ticketMessageAttachmentName && (
                        <div className="mt-2.5 pt-2 border-t border-border/60 flex items-center gap-1.5 text-xs text-primary font-medium">
                          <Paperclip className="size-3.5" />
                          <span>
                            Tệp đính kèm: {msg.ticketMessageAttachmentName}
                          </span>
                        </div>
                      )}
                    </div>
                  ))}
                </CardContent>

                <div className="border-t p-5 space-y-3 bg-muted/10">
                  <div className="space-y-1.5">
                    <span className="text-xs font-medium text-muted-foreground">
                      Mẫu trả lời nhanh:
                    </span>
                    <div className="flex flex-wrap gap-1.5">
                      {QUICK_RESPONSES.map((tpl, i) => (
                        <button
                          key={i}
                          type="button"
                          onClick={() => setReplyMessage(tpl)}
                          className="text-xs bg-background hover:bg-secondary text-foreground px-2.5 py-1.5 rounded-lg transition-colors truncate max-w-[320px] border border-border"
                          title={tpl}
                        >
                          {tpl}
                        </button>
                      ))}
                    </div>
                  </div>

                  <Textarea
                    value={replyMessage}
                    onChange={(e) => setReplyMessage(e.target.value)}
                    rows={3}
                    placeholder="Nhập nội dung phản hồi gửi về Web & App Mobile của khách hàng..."
                    className="resize-y bg-background text-sm"
                  />

                  <div className="flex justify-end">
                    <Button
                      size="sm"
                      disabled={!replyMessage.trim() || sendingReply}
                      onClick={handleSendReply}
                      className="h-9 gap-1.5 px-5 shadow-sm"
                    >
                      {sendingReply ? (
                        <Loader2 className="size-4 animate-spin" />
                      ) : (
                        <Send className="size-4" />
                      )}
                      <span>Gửi phản hồi cho Tenant</span>
                    </Button>
                  </div>
                </div>
              </Card>
            </div>

            {/* Cột phải (4/12): Thông tin Tenant & Cập nhật trạng thái */}
            <div className="space-y-6 lg:col-span-5 xl:col-span-4">
              {/* Card 1: Thông tin Tổ chức & Hồ sơ */}
              <Card className="border-border shadow-sm">
                <CardHeader className="border-b pb-4">
                  <div className="flex items-center gap-2.5">
                    <div className="flex size-9 items-center justify-center rounded-lg bg-secondary text-foreground">
                      <Building2 className="size-5" />
                    </div>
                    <div>
                      <CardTitle className="text-base font-semibold">
                        Thông tin Tổ chức & Điều phối
                      </CardTitle>
                      <CardDescription className="text-xs">
                        Chi tiết khách hàng gửi yêu cầu hỗ trợ
                      </CardDescription>
                    </div>
                  </div>
                </CardHeader>
                <CardContent className="pt-5">
                  <div className="rounded-xl border bg-muted/30 p-3.5 space-y-2.5 text-xs">
                    <div className="flex items-center justify-between">
                      <span className="text-muted-foreground">Mã Ticket:</span>
                      <div className="flex items-center gap-1.5 font-mono font-semibold text-foreground">
                        <span>{ticket.ticketCode}</span>
                        <button
                          type="button"
                          onClick={() =>
                            handleCopy(ticket.ticketCode, "mã ticket")
                          }
                          className="p-0.5 rounded hover:bg-muted text-muted-foreground hover:text-foreground"
                        >
                          <Copy className="size-3" />
                        </button>
                      </div>
                    </div>

                    <div className="flex items-center justify-between gap-2">
                      <span className="text-muted-foreground shrink-0">
                        Tổ chức:
                      </span>
                      <span className="font-semibold text-foreground text-right">
                        {ticket.tenantName}
                      </span>
                    </div>

                    <div className="flex items-center justify-between">
                      <span className="text-muted-foreground">Mã Tenant:</span>
                      <div className="flex items-center gap-1.5">
                        <span className="font-mono bg-background px-2 py-0.5 rounded border">
                          {ticket.tenantCode}
                        </span>
                        <button
                          type="button"
                          onClick={() =>
                            handleCopy(ticket.tenantCode, "mã tenant")
                          }
                          className="p-0.5 rounded hover:bg-muted text-muted-foreground hover:text-foreground"
                        >
                          <Copy className="size-3" />
                        </button>
                      </div>
                    </div>

                    {ticket.createdByName && (
                      <div className="flex items-center justify-between gap-2">
                        <span className="text-muted-foreground shrink-0">
                          Người gửi:
                        </span>
                        <span className="font-medium text-foreground text-right">
                          {ticket.createdByName}
                        </span>
                      </div>
                    )}

                    <div className="flex items-center justify-between">
                      <span className="text-muted-foreground">
                        Phòng ban tiếp nhận:
                      </span>
                      <span className="font-medium text-foreground">
                        {ticket.department}
                      </span>
                    </div>

                    {ticket.module && (
                      <div className="flex items-center justify-between gap-2">
                        <span className="text-muted-foreground shrink-0">
                          Phân hệ:
                        </span>
                        <span className="font-medium text-foreground text-right">
                          {ticket.module}
                        </span>
                      </div>
                    )}

                    <div className="flex items-center justify-between gap-2">
                      <span className="text-muted-foreground shrink-0">
                        Chuyên viên phụ trách:
                      </span>
                      <span className="font-semibold text-primary text-right">
                        {ticket.assignedEmployeeName || "Chưa phân công"}
                      </span>
                    </div>

                    <div className="flex items-center justify-between">
                      <span className="text-muted-foreground">
                        Thời gian tạo:
                      </span>
                      <span className="text-foreground flex items-center gap-1">
                        <Clock className="size-3 text-muted-foreground" />
                        {new Date(ticket.createdAt).toLocaleString("vi-VN")}
                      </span>
                    </div>
                  </div>
                </CardContent>
              </Card>

              {/* Card 2: Cập nhật trạng thái xử lý */}
              <Card className="border-border shadow-sm">
                <CardHeader className="border-b pb-4">
                  <div className="flex items-center gap-2.5">
                    <div className="flex size-9 items-center justify-center rounded-lg bg-emerald-500/10 text-emerald-600 dark:text-emerald-400">
                      <CheckCircle2 className="size-5" />
                    </div>
                    <div>
                      <CardTitle className="text-base font-semibold">
                        Trạng thái & Kết quả xử lý
                      </CardTitle>
                      <CardDescription className="text-xs">
                        Cập nhật trạng thái và ghi chú hướng giải quyết
                      </CardDescription>
                    </div>
                  </div>
                </CardHeader>
                <CardContent className="space-y-4 pt-5">
                  <div className="space-y-1.5">
                    <Label className="text-xs font-medium">
                      Trạng thái xử lý
                    </Label>
                    <Select
                      value={statusUpdate}
                      onValueChange={(val) =>
                        setStatusUpdate(val as TicketStatus)
                      }
                    >
                      <SelectTrigger className="h-10 bg-background">
                        <SelectValue placeholder="Chọn trạng thái" />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value={TicketStatus.Open}>
                          Chờ tiếp nhận
                        </SelectItem>
                        <SelectItem value={TicketStatus.InProgress}>
                          Đang xử lý
                        </SelectItem>
                        <SelectItem value={TicketStatus.Resolved}>
                          Đã xử lý
                        </SelectItem>
                      </SelectContent>
                    </Select>
                  </div>

                  <div className="space-y-1.5">
                    <Label className="text-xs font-medium">
                      Ghi chú kết quả xử lý (Resolution)
                    </Label>
                    <Textarea
                      value={resolutionText}
                      onChange={(e) => setResolutionText(e.target.value)}
                      rows={4}
                      placeholder="Ghi nhận phương án / kết quả khắc phục sự cố hiển thị cho khách hàng..."
                      className="resize-y bg-background text-sm"
                    />
                  </div>

                  <Button
                    size="sm"
                    variant="default"
                    onClick={handleUpdateStatus}
                    disabled={updatingStatus}
                    className="w-full h-9 gap-1.5 shadow-sm"
                  >
                    {updatingStatus ? (
                      <Loader2 className="size-4 animate-spin" />
                    ) : (
                      <CheckCircle2 className="size-4" />
                    )}
                    <span>Lưu cập nhật trạng thái</span>
                  </Button>
                </CardContent>
              </Card>
            </div>
          </div>
        )}
      </div>
    </>
  );
}

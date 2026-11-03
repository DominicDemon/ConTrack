"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import {
  ArrowLeft,
  Building2,
  CheckCircle2,
  Clock,
  Copy,
  Headset,
  Loader2,
  MessageSquare,
  Paperclip,
  Plus,
  RefreshCw,
  Send,
  ShieldCheck,
  Trash2,
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
import { Textarea } from "@/components/ui/textarea";
import { Header } from "@/components/ui/custom/header";
import { cn } from "@/lib/utils";
import {
  customerTicketApi,
  TicketDto,
  TicketPriority,
  TicketStatus,
  TICKET_PRIORITY_LABELS,
  TICKET_STATUS_LABELS,
} from "@/services/ticket-api";

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
        "bg-sky-50 text-sky-700 border-sky-200 dark:bg-sky-500/10 dark:text-sky-400 dark:border-sky-500/20",
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

export default function CustomerSupportTicketDetailPage() {
  const params = useParams<{ id: string }>();
  const router = useRouter();
  const ticketId = Number(params?.id);

  const [ticket, setTicket] = useState<TicketDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [replyMessage, setReplyMessage] = useState("");
  const [replyAttachmentName, setReplyAttachmentName] = useState<
    string | undefined
  >(undefined);
  const [sendingReply, setSendingReply] = useState(false);
  const replyFileInputRef = useRef<HTMLInputElement | null>(null);

  const loadTicket = useCallback(async () => {
    if (!Number.isFinite(ticketId) || ticketId <= 0) return;
    setLoading(true);
    try {
      const data = await customerTicketApi.getById(ticketId);
      setTicket(data);
    } catch {
      toast.error("Không thể tải thông tin chi tiết yêu cầu hỗ trợ");
    } finally {
      setLoading(false);
    }
  }, [ticketId]);

  useEffect(() => {
    void loadTicket();
  }, [loadTicket]);

  const handleCopy = (text: string, label: string) => {
    navigator.clipboard.writeText(text);
    toast.success(`Đã sao chép ${label}: ${text}`);
  };

  const handleSendCustomerReply = async () => {
    if (!ticket || !replyMessage.trim()) return;

    setSendingReply(true);
    try {
      const newMsg = await customerTicketApi.sendMessage(
        ticket.ticketSupportId,
        {
          messageContent: replyMessage.trim(),
          attachmentName: replyAttachmentName,
        },
      );

      toast.success("Đã gửi phản hồi tới bộ phận hỗ trợ thành công.");
      setTicket((prev) => {
        if (!prev) return null;
        return {
          ...prev,
          messages: [...(prev.messages || []), newMsg],
        };
      });
      setReplyMessage("");
      setReplyAttachmentName(undefined);
    } catch {
      toast.error("Không thể gửi tin nhắn phản hồi. Vui lòng thử lại.");
    } finally {
      setSendingReply(false);
    }
  };

  return (
    <>
      <Header />

      <div className="grow overflow-y-auto p-2 lg:p-10 space-y-6">
        {/* Page Header chuẩn giống Contract Detail */}
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

          <div className="flex flex-wrap gap-2">
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
              onClick={() => router.push("/support-tickets/create")}
              className="h-9 shadow-sm"
            >
              <Plus className="size-4 mr-2" />
              Tạo yêu cầu mới
            </Button>
          </div>
        </div>

        {loading && !ticket ? (
          <Card className="border-border shadow-sm p-12 text-center text-sm text-muted-foreground">
            <Loader2 className="size-6 animate-spin mx-auto mb-2 text-primary" />
            Đang tải chi tiết yêu cầu hỗ trợ...
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
            {/* Cột trái (8/12): Luồng trao đổi & Phản hồi */}
            <div className="space-y-6 lg:col-span-7 xl:col-span-8">
              {ticket.ticketResolution && (
                <div className="rounded-xl border border-emerald-500/30 bg-emerald-500/10 p-4 text-sm text-emerald-800 dark:text-emerald-300 flex items-start gap-3">
                  <CheckCircle2 className="size-5 shrink-0 mt-0.5 text-emerald-600 dark:text-emerald-400" />
                  <div>
                    <p className="font-semibold">
                      Kết quả xử lý từ bộ phận hỗ trợ ConTrack
                    </p>
                    <p className="mt-1 leading-relaxed">
                      {ticket.ticketResolution}
                    </p>
                  </div>
                </div>
              )}

              <Card className="border-border shadow-sm">
                <CardHeader className="border-b pb-4">
                  <div className="flex items-center justify-between">
                    <div className="flex items-center gap-2.5">
                      <div className="flex size-9 items-center justify-center rounded-lg bg-primary/10 text-primary">
                        <MessageSquare className="size-5" />
                      </div>
                      <div>
                        <CardTitle className="text-base font-semibold">
                          Lịch sử trao đổi & Phản hồi
                        </CardTitle>
                        <CardDescription className="text-xs">
                          Trao đổi trực tiếp với chuyên viên xử lý yêu cầu hỗ
                          trợ
                        </CardDescription>
                      </div>
                    </div>
                    <Badge
                      variant="outline"
                      className="text-[11px] font-normal"
                    >
                      {ticket.messages?.length ?? 0} tin nhắn
                    </Badge>
                  </div>
                </CardHeader>

                <CardContent className="space-y-4 pt-5 max-h-[500px] overflow-y-auto">
                  {ticket.messages?.map((msg) => (
                    <div
                      key={msg.ticketMessageId}
                      className={cn(
                        "rounded-xl border p-4 text-sm max-w-[88%]",
                        msg.isFromEmployee
                          ? "mr-auto bg-primary/5 border-primary/20 text-foreground"
                          : "ml-auto bg-secondary/50 border-border text-foreground",
                      )}
                    >
                      <div className="flex items-center justify-between gap-4 mb-1.5">
                        <span className="font-semibold text-xs flex items-center gap-1.5 text-primary">
                          {msg.isFromEmployee ? (
                            <>
                              <ShieldCheck className="size-4" />
                              <span>{msg.senderName} (Chuyên viên Hỗ trợ)</span>
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
                    {replyAttachmentName && (
                      <div className="inline-flex items-center gap-2 rounded-lg border bg-background px-3 py-1.5 text-xs">
                        <Paperclip className="size-3.5 text-primary" />
                        <span className="font-medium">
                          {replyAttachmentName}
                        </span>
                        <button
                          type="button"
                          onClick={() => setReplyAttachmentName(undefined)}
                          className="text-muted-foreground hover:text-destructive"
                        >
                          <Trash2 className="size-3.5" />
                        </button>
                      </div>
                    )}

                    <Textarea
                      value={replyMessage}
                      onChange={(e) => setReplyMessage(e.target.value)}
                      rows={3}
                      placeholder="Nhập nội dung phản hồi bổ sung gửi tới chuyên viên xử lý hỗ trợ..."
                      className="resize-y bg-background text-sm"
                    />

                    <div className="flex items-center justify-between gap-2">
                      <div>
                        <input
                          ref={replyFileInputRef}
                          type="file"
                          className="hidden"
                          onChange={(e) => {
                            const file = e.target.files?.[0];
                            if (file) setReplyAttachmentName(file.name);
                            e.target.value = "";
                          }}
                        />
                        <Button
                          type="button"
                          variant="outline"
                          size="sm"
                          className="h-9 gap-1.5 text-xs"
                          onClick={() => replyFileInputRef.current?.click()}
                        >
                          <Paperclip className="size-3.5" />
                          <span>Đính kèm tệp</span>
                        </Button>
                      </div>

                      <Button
                        type="button"
                        size="sm"
                        disabled={!replyMessage.trim() || sendingReply}
                        onClick={handleSendCustomerReply}
                        className="h-9 gap-1.5 px-5 shadow-sm"
                      >
                        {sendingReply ? (
                          <Loader2 className="size-4 animate-spin" />
                        ) : (
                          <Send className="size-4" />
                        )}
                        <span>Gửi phản hồi</span>
                      </Button>
                    </div>
                </div>
              </Card>
            </div>

            {/* Cột phải (4/12): Thông tin hồ sơ Ticket */}
            <div className="space-y-6 lg:col-span-5 xl:col-span-4">
              <Card className="border-border shadow-sm">
                <CardHeader className="border-b pb-4">
                  <div className="flex items-center gap-2.5">
                    <div className="flex size-9 items-center justify-center rounded-lg bg-secondary text-foreground">
                      <Building2 className="size-5" />
                    </div>
                    <div>
                      <CardTitle className="text-base font-semibold">
                        Thông tin Yêu cầu
                      </CardTitle>
                      <CardDescription className="text-xs">
                        Chi tiết mã hồ sơ và điều phối xử lý
                      </CardDescription>
                    </div>
                  </div>
                </CardHeader>
                <CardContent className="space-y-3 pt-5 text-sm">
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

                    <div className="flex items-center justify-between">
                      <span className="text-muted-foreground">Trạng thái:</span>
                      <span className="font-semibold text-foreground">
                        {TICKET_STATUS_LABELS[ticket.ticketStatus]}
                      </span>
                    </div>

                    <div className="flex items-center justify-between">
                      <span className="text-muted-foreground">
                        Mức độ ưu tiên:
                      </span>
                      <Badge
                        variant="outline"
                        className={cn(
                          "text-[11px]",
                          ticket.ticketPriority === TicketPriority.Urgent &&
                            "border-rose-500/30 bg-rose-500/10 text-rose-600",
                        )}
                      >
                        {TICKET_PRIORITY_LABELS[ticket.ticketPriority]}
                      </Badge>
                    </div>

                    <div className="flex items-center justify-between">
                      <span className="text-muted-foreground">
                        Phòng ban tiếp nhận:
                      </span>
                      <span className="font-medium text-foreground">
                        {ticket.department}
                      </span>
                    </div>

                    <div className="flex items-center justify-between gap-2">
                      <span className="text-muted-foreground shrink-0">
                        Phân hệ:
                      </span>
                      <span className="font-medium text-foreground text-right">
                        {ticket.module || "Hỗ trợ chung"}
                      </span>
                    </div>

                    <div className="flex items-center justify-between gap-2">
                      <span className="text-muted-foreground shrink-0">
                        Chuyên viên phụ trách:
                      </span>
                      <span className="font-semibold text-primary text-right">
                        {ticket.assignedEmployeeName ||
                          "Đang điều phối chuyên viên"}
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
            </div>
          </div>
        )}
      </div>
    </>
  );
}

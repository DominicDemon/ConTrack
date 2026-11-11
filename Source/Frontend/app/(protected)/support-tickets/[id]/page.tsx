"use client";

import { use, useCallback, useEffect, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import {
  ArrowLeft,
  Building2,
  Calendar,
  CheckCheck,
  CheckCircle2,
  Clock,
  FileText,
  Layers,
  Loader2,
  MessageCircle,
  Paperclip,
  Send,
  UserCheck,
} from "lucide-react";
import { toast } from "@/components/ui/sonner";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Header } from "@/components/ui/custom/header";
import { cn } from "@/lib/utils";
import {
  customerTicketApi,
  TicketDto,
  TicketStatus,
} from "@/services/ticket-api";

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
  const [activeTab, setActiveTab] = useState<"chat" | "info">("chat");

  const [replyText, setReplyText] = useState("");
  const [attachmentName, setAttachmentName] = useState("");
  const [sending, setSending] = useState(false);

  const fileInputRef = useRef<HTMLInputElement | null>(null);
  const chatBottomRef = useRef<HTMLDivElement | null>(null);
  const prevMsgCountRef = useRef<number>(0);

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

  // Tự động đồng bộ tin nhắn mới theo thời gian thực (mỗi 2 giây) mà không cần bấm Làm mới
  useEffect(() => {
    const timer = setInterval(() => {
      void loadDetail(true);
    }, 2000);
    return () => clearInterval(timer);
  }, [loadDetail]);

  // Tự động cuộn xuống cuối khung chat khi có tin nhắn mới
  const chatMessages = (ticket?.messages ?? []).filter(
    (m) => m.ticketMessageType !== "STAFF_REPORT" && m.ticketMessageType !== "ASSIGNMENT",
  );

  useEffect(() => {
    if (chatMessages.length !== prevMsgCountRef.current) {
      prevMsgCountRef.current = chatMessages.length;
      setTimeout(() => {
        chatBottomRef.current?.scrollIntoView({ behavior: "smooth" });
      }, 60);
    }
  }, [chatMessages.length]);

  const handleSendReply = async (e?: React.FormEvent) => {
    e?.preventDefault();
    const content = replyText.trim();
    if (!content || !ticket) return;

    setSending(true);
    try {
      const createdMsg = await customerTicketApi.sendMessage(
        ticket.ticketSupportId,
        {
          messageContent: content,
          attachmentName: attachmentName || undefined,
        },
      );

      // Hiển thị tin nhắn ngay lập tức như Zalo
      setTicket((prev) =>
        prev
          ? {
              ...prev,
              messages: [...(prev.messages ?? []), createdMsg],
            }
          : prev,
      );
      setReplyText("");
      setAttachmentName("");
      setTimeout(() => {
        chatBottomRef.current?.scrollIntoView({ behavior: "smooth" });
      }, 50);
    } catch {
      toast.error("Gửi tin nhắn thất bại");
    } finally {
      setSending(false);
    }
  };

  if (loading) {
    return (
      <>
        <Header />
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
        <Header />
        <div className="grow p-2 lg:p-10">
          <Card className="mx-auto max-w-xl border-dashed">
            <CardContent className="py-12 text-center">
              <p className="font-semibold text-foreground">
                Không tìm thấy yêu cầu hỗ trợ hoặc bạn không có quyền truy cập
              </p>
              <Button
                className="mt-4"
                variant="outline"
                onClick={() => router.push("/support-tickets")}
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

  return (
    <>
      <Header />

      <div className="grow overflow-y-auto p-2 lg:p-10 space-y-6">
        <div className="space-y-6">
          {/* Page Header đồng bộ với các trang khác */}
          <div className="flex flex-col gap-4 md:flex-row md:items-start md:justify-between">
            <div>
              <Button
                variant="ghost"
                className="-ml-3 mb-2 text-muted-foreground"
                onClick={() => router.push("/support-tickets")}
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

            {/* Chuyển trang con (Tabs tách biệt) */}
            <div className="inline-flex rounded-lg border bg-muted p-1">
              <button
                type="button"
                onClick={() => setActiveTab("chat")}
                className={cn(
                  "inline-flex items-center gap-2 rounded-md px-4 py-2 text-xs font-semibold transition-all",
                  activeTab === "chat"
                    ? "bg-background text-primary shadow-xs"
                    : "text-muted-foreground hover:text-foreground",
                )}
              >
                <MessageCircle className="size-4" />
                <span>Trao đổi trực tuyến</span>
                <span className="rounded-full bg-primary/10 px-1.5 py-0.5 text-[10px] font-bold text-primary">
                  {chatMessages.length}
                </span>
              </button>
              <button
                type="button"
                onClick={() => setActiveTab("info")}
                className={cn(
                  "inline-flex items-center gap-2 rounded-md px-4 py-2 text-xs font-semibold transition-all",
                  activeTab === "info"
                    ? "bg-background text-primary shadow-xs"
                    : "text-muted-foreground hover:text-foreground",
                )}
              >
                <FileText className="size-4" />
                <span>Thông tin & Kết quả xử lý</span>
              </button>
            </div>
          </div>

          {/* KẾT QUẢ XỬ LÝ NỔI BẬT NẾU ĐÃ CÓ */}
          {ticket.ticketResolution && (
            <div className="flex items-start gap-3 rounded-xl border border-emerald-200 bg-emerald-50/90 p-4 text-emerald-950 shadow-xs dark:border-emerald-500/30 dark:bg-emerald-950/30 dark:text-emerald-200">
              <CheckCircle2 className="mt-0.5 size-5 shrink-0 text-emerald-600" />
              <div className="space-y-1 text-sm">
                <p className="font-bold">
                  Phản hồi kết quả chính thức từ Trưởng phòng {ticket.department}:
                </p>
                <p className="whitespace-pre-line leading-relaxed">
                  {ticket.ticketResolution}
                </p>
              </div>
            </div>
          )}

          {/* TAB 1: KHUNG CHAT ZALO REALTIME */}
          {activeTab === "chat" && (
            <div className="flex h-[620px] flex-col overflow-hidden rounded-xl border border-slate-200 bg-card shadow-sm dark:border-slate-800">
              {/* Header Zalo Chat */}
              <div className="flex items-center justify-between border-b bg-white px-5 py-3.5 dark:bg-slate-900">
                <div className="flex items-center gap-3">
                  <div className="relative flex size-10 items-center justify-center rounded-full bg-blue-600 font-bold text-white">
                    TP
                    <span className="absolute right-0 bottom-0 size-2.5 rounded-full bg-emerald-500 ring-2 ring-white" />
                  </div>
                  <div>
                    <p className="text-sm font-bold text-foreground">
                      {ticket.departmentHeadName
                        ? `Trưởng phòng: ${ticket.departmentHeadName}`
                        : `Trưởng bộ phận ${ticket.department}`}
                    </p>
                    <p className="flex items-center gap-1.5 text-xs text-emerald-600 dark:text-emerald-400">
                      <span className="size-1.5 rounded-full bg-emerald-500" />
                      Trực tuyến • Tự động cập nhật tin nhắn tức thì
                    </p>
                  </div>
                </div>

                <div className="hidden text-right text-xs text-muted-foreground sm:block">
                  <p className="font-semibold text-foreground">{serviceLabel}</p>
                  <p>{ticket.department}</p>
                </div>
              </div>

              {/* Message Stream kiểu Zalo */}
              <div className="flex-1 space-y-4 overflow-y-auto bg-[#eef0f5] p-5 dark:bg-slate-950">
                {chatMessages.map((msg) => {
                  const isMine = !msg.isFromEmployee;
                  return (
                    <div
                      key={msg.ticketMessageId}
                      className={cn(
                        "flex items-end gap-2.5",
                        isMine ? "justify-end" : "justify-start",
                      )}
                    >
                      {!isMine && (
                        <div className="flex size-8 shrink-0 items-center justify-center rounded-full bg-blue-600 text-xs font-bold text-white shadow-xs">
                          TP
                        </div>
                      )}

                      <div
                        className={cn(
                          "max-w-[75%] rounded-2xl px-4 py-2.5 shadow-xs",
                          isMine
                            ? "rounded-br-xs bg-[#e5efff] text-slate-900 border border-blue-200/80 dark:bg-blue-600 dark:text-white dark:border-blue-500"
                            : "rounded-bl-xs bg-white text-slate-900 border border-slate-200/80 dark:bg-slate-800 dark:text-slate-100 dark:border-slate-700",
                        )}
                      >
                        {!isMine && (
                          <p className="mb-1 text-[11px] font-bold text-blue-600 dark:text-blue-400">
                            {msg.senderName} ({ticket.department})
                          </p>
                        )}

                        <p className="whitespace-pre-line text-sm leading-relaxed">
                          {msg.ticketMessageContent}
                        </p>

                        {msg.ticketMessageAttachmentName && (
                          <div className="mt-2 inline-flex items-center gap-1.5 rounded-md bg-black/5 px-2.5 py-1 text-xs font-medium">
                            <Paperclip className="size-3.5" />
                            <span>{msg.ticketMessageAttachmentName}</span>
                          </div>
                        )}

                        <div
                          className={cn(
                            "mt-1.5 flex items-center gap-1 text-[11px]",
                            isMine
                              ? "justify-end text-slate-500 dark:text-blue-100"
                              : "justify-start text-slate-400",
                          )}
                        >
                          <span>{formatZaloTime(msg.createdAt)}</span>
                          {isMine && <CheckCheck className="size-3.5 text-blue-600 dark:text-blue-200" />}
                        </div>
                      </div>
                    </div>
                  );
                })}
                <div ref={chatBottomRef} />
              </div>

              {/* Input gửi tin nhắn kiểu Zalo */}
              <form
                onSubmit={handleSendReply}
                className="border-t bg-white p-3.5 dark:bg-slate-900"
              >
                {attachmentName && (
                  <div className="mb-2 inline-flex items-center gap-2 rounded-md bg-blue-50 px-3 py-1 text-xs text-blue-700">
                    <Paperclip className="size-3.5" />
                    <span>{attachmentName}</span>
                    <button
                      type="button"
                      onClick={() => setAttachmentName("")}
                      className="font-bold hover:text-rose-600"
                    >
                      ×
                    </button>
                  </div>
                )}

                <div className="flex items-center gap-2">
                  <input
                    ref={fileInputRef}
                    type="file"
                    className="hidden"
                    onChange={(e) => {
                      const f = e.target.files?.[0];
                      if (f) setAttachmentName(f.name);
                      e.target.value = "";
                    }}
                  />
                  <Button
                    type="button"
                    variant="ghost"
                    size="icon"
                    onClick={() => fileInputRef.current?.click()}
                    title="Đính kèm tệp"
                    className="shrink-0 text-slate-500 hover:text-blue-600"
                  >
                    <Paperclip className="size-5" />
                  </Button>

                  <input
                    type="text"
                    value={replyText}
                    onChange={(e) => setReplyText(e.target.value)}
                    placeholder="Nhập tin nhắn trao đổi với Trưởng phòng... (Nhấn Enter để gửi ngay)"
                    className="h-10 flex-1 rounded-full border border-slate-300 bg-slate-50 px-4 text-sm focus:border-blue-500 focus:bg-white focus:outline-none dark:border-slate-700 dark:bg-slate-800"
                  />

                  <Button
                    type="submit"
                    disabled={sending || !replyText.trim()}
                    className="h-10 rounded-full bg-blue-600 px-5 text-white hover:bg-blue-700"
                  >
                    {sending ? (
                      <Loader2 className="size-4 animate-spin" />
                    ) : (
                      <Send className="size-4" />
                    )}
                    <span className="ml-1.5 hidden sm:inline">Gửi ngay</span>
                  </Button>
                </div>
              </form>
            </div>
          )}

          {/* TAB 2: THÔNG TIN CHI TIẾT YÊU CẦU & KẾT QUẢ */}
          {activeTab === "info" && (
            <div className="grid grid-cols-1 gap-5 md:grid-cols-3">
              <Card className="md:col-span-2 shadow-xs">
                <CardHeader className="border-b pb-3">
                  <CardTitle className="text-base font-bold">
                    Nội dung câu hỏi ban đầu
                  </CardTitle>
                </CardHeader>
                <CardContent className="space-y-4 pt-4">
                  <div className="rounded-lg border bg-slate-50/70 p-4 text-sm leading-relaxed whitespace-pre-line dark:bg-slate-900/50">
                    {ticket.ticketDescription}
                  </div>
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
          )}
        </div>
      </div>
    </>
  );
}

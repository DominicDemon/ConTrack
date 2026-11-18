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
  ImagePlus,
  Layers,
  Loader2,
  MessageCircle,
  Send,
  Trash2,
  UserCheck,
  X,
  ZoomIn,
  ZoomOut,
} from "lucide-react";
import { toast } from "@/components/ui/sonner";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import PortalHeader from "@/components/portal/portal-header";
import { cn } from "@/lib/utils";
import {
  customerTicketApi,
  TicketDto,
  TicketMessageDto,
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

function readImageAsDataUrl(file: File): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => {
      const result = typeof reader.result === "string" ? reader.result : "";
      if (!result) {
        reject(new Error("Empty image"));
        return;
      }
      const img = new window.Image();
      img.onload = () => {
        const maxDim = 1600;
        let { width, height } = img;
        if (width > maxDim || height > maxDim) {
          if (width > height) {
            height = Math.round((height * maxDim) / width);
            width = maxDim;
          } else {
            width = Math.round((width * maxDim) / height);
            height = maxDim;
          }
          const canvas = document.createElement("canvas");
          canvas.width = width;
          canvas.height = height;
          const ctx = canvas.getContext("2d");
          if (ctx) {
            ctx.drawImage(img, 0, 0, width, height);
            resolve(canvas.toDataURL("image/jpeg", 0.88));
            return;
          }
        }
        resolve(result);
      };
      img.onerror = () => resolve(result);
      img.src = result;
    };
    reader.onerror = () => reject(new Error("Failed to read file"));
    reader.readAsDataURL(file);
  });
}

function getMessageImages(msg: TicketMessageDto): string[] {
  const urls: string[] = [];
  if (Array.isArray(msg.attachmentUrls)) {
    for (const u of msg.attachmentUrls) {
      if (
        u &&
        (u.startsWith("data:image") ||
          u.startsWith("http") ||
          u.startsWith("/"))
      ) {
        urls.push(u);
      }
    }
  }
  if (
    msg.ticketMessageAttachmentUrl &&
    (msg.ticketMessageAttachmentUrl.startsWith("data:image") ||
      msg.ticketMessageAttachmentUrl.startsWith("http") ||
      msg.ticketMessageAttachmentUrl.startsWith("/")) &&
    !urls.includes(msg.ticketMessageAttachmentUrl)
  ) {
    urls.push(msg.ticketMessageAttachmentUrl);
  }
  return urls;
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
  const [attachedImage, setAttachedImage] = useState<{
    name: string;
    dataUrl: string;
  } | null>(null);
  const [sending, setSending] = useState(false);

  const [previewImage, setPreviewImage] = useState<string | null>(null);
  const [zoomScale, setZoomScale] = useState(1);

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

  useEffect(() => {
    const timer = setInterval(() => {
      void loadDetail(true);
    }, 2000);
    return () => clearInterval(timer);
  }, [loadDetail]);

  const chatMessages = (ticket?.messages ?? []).filter(
    (m) =>
      m.ticketMessageType !== "STAFF_REPORT" &&
      m.ticketMessageType !== "STAFF_COMPLETION" &&
      m.ticketMessageType !== "ASSIGNMENT",
  );

  useEffect(() => {
    if (chatMessages.length !== prevMsgCountRef.current) {
      prevMsgCountRef.current = chatMessages.length;
      setTimeout(() => {
        chatBottomRef.current?.scrollIntoView({ behavior: "smooth" });
      }, 60);
    }
  }, [chatMessages.length]);

  const handleSelectImage = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    e.target.value = "";
    if (!file) return;
    if (!file.type.startsWith("image/")) {
      toast.warning("Chỉ hỗ trợ gửi hình ảnh (PNG, JPG, JPEG, WEBP, GIF).");
      return;
    }
    try {
      const dataUrl = await readImageAsDataUrl(file);
      setAttachedImage({ name: file.name, dataUrl });
    } catch {
      toast.error("Không thể đọc hình ảnh này");
    }
  };

  const handlePasteImage = async (e: React.ClipboardEvent<HTMLInputElement>) => {
    const items = e.clipboardData?.items;
    if (!items) return;
    for (let i = 0; i < items.length; i++) {
      if (items[i].type.startsWith("image/")) {
        const file = items[i].getAsFile();
        if (file) {
          try {
            const dataUrl = await readImageAsDataUrl(file);
            setAttachedImage({ name: file.name || "pasted-image.png", dataUrl });
            toast.success("Đã đính kèm ảnh từ bộ nhớ tạm!");
          } catch {
            toast.error("Không thể đọc ảnh từ bộ nhớ tạm");
          }
          break;
        }
      }
    }
  };

  const handleSendReply = async (e?: React.FormEvent) => {
    e?.preventDefault();
    const content = replyText.trim();
    if ((!content && !attachedImage) || !ticket) return;

    setSending(true);
    try {
      const createdMsg = await customerTicketApi.sendMessage(
        ticket.ticketSupportId,
        {
          messageContent: content,
          attachmentName: attachedImage?.name,
          attachmentUrl: attachedImage?.dataUrl,
          attachmentNames: attachedImage ? [attachedImage.name] : undefined,
          attachmentUrls: attachedImage ? [attachedImage.dataUrl] : undefined,
        },
      );

      setTicket((prev) =>
        prev
          ? {
              ...prev,
              messages: [...(prev.messages ?? []), createdMsg],
            }
          : prev,
      );
      setReplyText("");
      setAttachedImage(null);
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
      : chatMessages.length > 0
        ? getMessageImages(chatMessages[0])
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

          {activeTab === "chat" && (
            <div className="flex h-[640px] flex-col overflow-hidden rounded-xl border border-slate-200 bg-card shadow-sm dark:border-slate-800">
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
                      Trực tuyến • Tự động cập nhật tin nhắn & hình ảnh tức thì
                    </p>
                  </div>
                </div>

                <div className="hidden text-right text-xs text-muted-foreground sm:block">
                  <p className="font-semibold text-foreground">{serviceLabel}</p>
                  <p>{ticket.department}</p>
                </div>
              </div>

              <div className="flex-1 space-y-4 overflow-y-auto bg-[#eef0f5] p-5 dark:bg-slate-950">
                {chatMessages.map((msg) => {
                  const isMine = !msg.isFromEmployee;
                  const msgImages = getMessageImages(msg);
                  const showText =
                    msg.ticketMessageContent &&
                    msg.ticketMessageContent !== "[Hình ảnh đính kèm]";

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

                        {showText && (
                          <p className="whitespace-pre-line text-sm leading-relaxed">
                            {msg.ticketMessageContent}
                          </p>
                        )}

                        {msgImages.length > 0 && (
                          <div
                            className={cn(
                              "grid gap-2",
                              showText ? "mt-2.5" : "mt-0.5",
                              msgImages.length > 1
                                ? "grid-cols-2"
                                : "grid-cols-1",
                            )}
                          >
                            {msgImages.map((imgUrl, idx) => (
                              <button
                                key={idx}
                                type="button"
                                onClick={() => {
                                  setPreviewImage(imgUrl);
                                  setZoomScale(1);
                                }}
                                className="group relative block overflow-hidden rounded-xl border border-black/10 bg-black/5 cursor-zoom-in"
                                title="Bấm vào để xem ảnh lớn"
                              >
                                <img
                                  src={imgUrl}
                                  alt="Hình ảnh trao đổi"
                                  className="max-h-64 w-auto max-w-full rounded-xl object-contain transition-transform duration-200 group-hover:scale-[1.02]"
                                />
                                <span className="absolute right-2 bottom-2 flex items-center gap-1 rounded-full bg-black/65 px-2 py-0.5 text-[10px] font-medium text-white opacity-0 transition-opacity group-hover:opacity-100">
                                  <ZoomIn className="size-3" />
                                  Phóng to
                                </span>
                              </button>
                            ))}
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
                          {isMine && (
                            <CheckCheck className="size-3.5 text-blue-600 dark:text-blue-200" />
                          )}
                        </div>
                      </div>
                    </div>
                  );
                })}
                <div ref={chatBottomRef} />
              </div>

              {isResolved ? (
                <div className="flex items-center justify-center gap-2 border-t bg-slate-100 px-4 py-3.5 text-xs font-medium text-slate-600 dark:bg-slate-900 dark:text-slate-400">
                  <CheckCircle2 className="size-4 text-emerald-600" />
                  <span>
                    Yêu cầu hỗ trợ đã hoàn thành và đóng. Bạn chỉ có thể xem lại
                    lịch sử tin nhắn và kết quả xử lý, không thể nhắn thêm.
                  </span>
                </div>
              ) : (
                <form
                  onSubmit={handleSendReply}
                  className="border-t bg-white p-3.5 dark:bg-slate-900"
                >
                  {attachedImage && (
                    <div className="mb-3 flex items-center gap-3 rounded-xl border border-blue-200 bg-blue-50/60 p-2.5 dark:border-blue-900 dark:bg-blue-950/30">
                      <button
                        type="button"
                        onClick={() => {
                          setPreviewImage(attachedImage.dataUrl);
                          setZoomScale(1);
                        }}
                        className="group relative h-16 w-20 shrink-0 overflow-hidden rounded-lg border bg-white cursor-zoom-in"
                        title="Bấm vào để xem ảnh lớn"
                      >
                        <img
                          src={attachedImage.dataUrl}
                          alt={attachedImage.name}
                          className="h-full w-full object-cover"
                        />
                      </button>
                      <div className="min-w-0 flex-1 text-xs">
                        <p className="font-semibold text-blue-900 dark:text-blue-200">
                          Ảnh đính kèm sẵn sàng gửi
                        </p>
                        <p className="text-[11px] text-muted-foreground">
                          Bấm vào ảnh để xem phóng to hoặc nhấn Gửi ngay
                        </p>
                      </div>
                      <Button
                        type="button"
                        variant="ghost"
                        size="icon"
                        onClick={() => setAttachedImage(null)}
                        className="size-8 text-muted-foreground hover:text-rose-600"
                        title="Xóa ảnh"
                      >
                        <Trash2 className="size-4" />
                      </Button>
                    </div>
                  )}

                  <div className="flex items-center gap-2">
                    <input
                      ref={fileInputRef}
                      type="file"
                      accept="image/png,image/jpeg,image/jpg,image/webp,image/gif"
                      className="hidden"
                      onChange={handleSelectImage}
                    />
                    <Button
                      type="button"
                      variant="outline"
                      size="icon"
                      onClick={() => fileInputRef.current?.click()}
                      title="Gửi hình ảnh minh họa"
                      className="size-10 shrink-0 rounded-full border-slate-300 text-blue-600 hover:bg-blue-50 hover:text-blue-700"
                    >
                      <ImagePlus className="size-5" />
                    </Button>

                    <input
                      type="text"
                      value={replyText}
                      onChange={(e) => setReplyText(e.target.value)}
                      onPaste={handlePasteImage}
                      placeholder="Nhập tin nhắn hoặc nhấn Ctrl+V để dán ảnh gửi Trưởng phòng..."
                      className="h-10 flex-1 rounded-full border border-slate-300 bg-slate-50 px-4 text-sm focus:border-blue-500 focus:bg-white focus:outline-none dark:border-slate-700 dark:bg-slate-800"
                    />

                    <Button
                      type="submit"
                      disabled={sending || (!replyText.trim() && !attachedImage)}
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
              )}
            </div>
          )}

          {activeTab === "info" && (
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
          )}
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

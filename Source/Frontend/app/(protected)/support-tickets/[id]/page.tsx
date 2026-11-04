"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import {
  ArrowLeft,
  Building2,
  CheckCircle2,
  Clock,
  Copy,
  FolderKanban,
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
  UserCheck,
  UserPlus,
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
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Header } from "@/components/ui/custom/header";
import { cn } from "@/lib/utils";
import {
  customerTicketApi,
  DepartmentStaffDto,
  TicketDto,
  TicketPriority,
  TicketStatus,
  TICKET_PRIORITY_LABELS,
  TICKET_STATUS_LABELS,
} from "@/services/ticket-api";

function TicketStatusBadge({ status }: { status: TicketStatus | string }) {
  const config: Record<string, { label: string; className: string }> = {
    [TicketStatus.Open]: {
      label: "Chờ Trưởng phòng tiếp nhận",
      className:
        "bg-amber-50 text-amber-700 border-amber-200 dark:bg-amber-500/10 dark:text-amber-400 dark:border-amber-500/20",
    },
    [TicketStatus.InProgress]: {
      label: "Đang xử lý (Đã giao nhân viên)",
      className:
        "bg-sky-50 text-sky-700 border-sky-200 dark:bg-sky-500/10 dark:text-sky-400 dark:border-sky-500/20",
    },
    [TicketStatus.PendingHeadApproval]: {
      label: "Chờ Trưởng phòng duyệt kết quả",
      className:
        "bg-purple-50 text-purple-700 border-purple-200 dark:bg-purple-500/10 dark:text-purple-400 dark:border-purple-500/20",
    },
    [TicketStatus.Resolved]: {
      label: "Đã xử lý xong",
      className:
        "bg-emerald-50 text-emerald-700 border-emerald-200 dark:bg-emerald-500/10 dark:text-emerald-400 dark:border-emerald-500/20",
    },
  };
  const current = config[status] ?? {
    label: status,
    className: "bg-muted text-muted-foreground",
  };
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
  const [replyAttachmentName, setReplyAttachmentName] = useState<string | undefined>(undefined);
  const [sendingReply, setSendingReply] = useState(false);
  const replyFileInputRef = useRef<HTMLInputElement | null>(null);

  // Workflow states
  const [departmentStaff, setDepartmentStaff] = useState<DepartmentStaffDto[]>([]);
  const [loadingStaff, setLoadingStaff] = useState(false);
  const [selectedStaffId, setSelectedStaffId] = useState<string>("");
  const [assignNote, setAssignNote] = useState("");
  const [assigning, setAssigning] = useState(false);

  const [staffResolution, setStaffResolution] = useState("");
  const [staffCompleting, setStaffCompleting] = useState(false);

  const [headResolution, setHeadResolution] = useState("");
  const [headCustomerNotice, setHeadCustomerNotice] = useState("");
  const [resolving, setResolving] = useState(false);

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

  const loadStaffList = async () => {
    if (!ticketId) return;
    setLoadingStaff(true);
    try {
      const staff = await customerTicketApi.getDepartmentStaff(ticketId);
      setDepartmentStaff(staff);
    } catch {
      toast.error("Không thể tải danh sách nhân viên phòng ban");
    } finally {
      setLoadingStaff(false);
    }
  };

  const handleCopy = (text: string, label: string) => {
    navigator.clipboard.writeText(text);
    toast.success(`Đã sao chép ${label}: ${text}`);
  };

  const handleSendCustomerReply = async () => {
    if (!ticket || !replyMessage.trim()) return;

    setSendingReply(true);
    try {
      const newMsg = await customerTicketApi.sendMessage(ticket.ticketSupportId, {
        messageContent: replyMessage.trim(),
        attachmentName: replyAttachmentName,
      });

      toast.success("Đã gửi phản hồi thành công.");
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

  const handleAssignToStaff = async () => {
    if (!selectedStaffId) {
      toast.error("Vui lòng chọn nhân viên trong phòng ban để phân công.");
      return;
    }
    setAssigning(true);
    try {
      const updated = await customerTicketApi.assignToStaff(ticketId, {
        staffEmployeeId: Number(selectedStaffId),
        note: assignNote.trim(),
      });
      toast.success("Đã phân công việc cho nhân viên phòng ban thành công!");
      setTicket(updated);
      setAssignNote("");
    } catch {
      toast.error("Lỗi khi phân công nhân viên xử lý.");
    } finally {
      setAssigning(false);
    }
  };

  const handleStaffComplete = async () => {
    if (!staffResolution.trim()) {
      toast.error("Vui lòng nhập kết quả xử lý trước khi báo cáo.");
      return;
    }
    setStaffCompleting(true);
    try {
      const updated = await customerTicketApi.staffComplete(ticketId, {
        resolution: staffResolution.trim(),
      });
      toast.success("Đã báo cáo hoàn tất xử lý cho Trưởng phòng duyệt!");
      setTicket(updated);
      setStaffResolution("");
    } catch {
      toast.error("Lỗi khi báo cáo hoàn tất xử lý.");
    } finally {
      setStaffCompleting(false);
    }
  };

  const handleHeadResolve = async () => {
    if (!headResolution.trim()) {
      toast.error("Vui lòng nhập kết quả xử lý để thông báo tới khách hàng.");
      return;
    }
    setResolving(true);
    try {
      const updated = await customerTicketApi.headResolve(ticketId, {
        resolution: headResolution.trim(),
        messageToCustomer: headCustomerNotice.trim() || undefined,
        closeTicket: true,
      });
      toast.success("Đã gửi thông báo kết quả cho khách hàng và đóng ticket!");
      setTicket(updated);
      setHeadResolution("");
      setHeadCustomerNotice("");
    } catch {
      toast.error("Lỗi khi thông báo kết quả cho khách hàng.");
    } finally {
      setResolving(false);
    }
  };

  return (
    <>
      <Header />

      <div className="grow overflow-y-auto p-2 lg:p-10 space-y-6">
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
            {/* Cột trái (8/12): Luồng trao đổi & Phản hồi & Nghiệp vụ điều phối */}
            <div className="space-y-6 lg:col-span-7 xl:col-span-8">
              {/* Kết quả xử lý chính thức thông báo cho khách hàng */}
              {ticket.ticketResolution && (
                <div className="rounded-xl border border-emerald-500/30 bg-emerald-500/10 p-5 text-sm text-emerald-800 dark:text-emerald-300 flex items-start gap-3">
                  <CheckCircle2 className="size-5 shrink-0 mt-0.5 text-emerald-600 dark:text-emerald-400" />
                  <div className="space-y-1">
                    <p className="font-semibold text-base">
                      Kết quả giải quyết từ Trưởng phòng ban ({ticket.department}):
                    </p>
                    <p className="leading-relaxed whitespace-pre-wrap">
                      {ticket.ticketResolution}
                    </p>
                    {ticket.ticketResolvedAt && (
                      <p className="text-xs text-muted-foreground pt-1">
                        Thời gian hoàn tất: {new Date(ticket.ticketResolvedAt).toLocaleString("vi-VN")}
                      </p>
                    )}
                  </div>
                </div>
              )}

              {/* KHỐI NGHIỆP VỤ 1: TRƯỞNG PHÒNG PHÂN VIỆC CHO NHÂN VIÊN */}
              <Card className="border-border shadow-sm border-blue-500/30 bg-blue-500/5">
                <CardHeader className="pb-3">
                  <div className="flex items-center gap-2">
                    <UserPlus className="size-5 text-blue-600 dark:text-blue-400" />
                    <div>
                      <CardTitle className="text-base font-semibold text-blue-900 dark:text-blue-200">
                        Nghiệp vụ Trưởng phòng: Phân công nhân viên xử lý
                      </CardTitle>
                      <CardDescription className="text-xs">
                        Người đứng đầu phòng ban {ticket.department} điều phối giao việc cho nhân viên trong phòng
                      </CardDescription>
                    </div>
                  </div>
                </CardHeader>
                <CardContent className="space-y-4 text-sm">
                  <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
                    <div className="space-y-1.5">
                      <Label className="text-xs font-medium">Chọn nhân viên phòng ban:</Label>
                      <Select
                        value={selectedStaffId}
                        onValueChange={setSelectedStaffId}
                        onOpenChange={(open) => {
                          if (open && departmentStaff.length === 0) void loadStaffList();
                        }}
                      >
                        <SelectTrigger className="bg-background text-sm">
                          <SelectValue placeholder={loadingStaff ? "Đang tải nhân sự..." : "Chọn nhân viên phụ trách"} />
                        </SelectTrigger>
                        <SelectContent>
                          {departmentStaff.map((staff) => (
                            <SelectItem key={staff.employeeId} value={String(staff.employeeId)}>
                              {staff.employeeName} ({staff.employeeCode ?? "NV"}) {staff.isManager ? " - Trưởng phòng" : ""}
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    </div>
                    <div className="space-y-1.5">
                      <Label className="text-xs font-medium">Ghi chú phân công:</Label>
                      <Input
                        placeholder="Nội dung nhắc nhở / hạn chót xử lý..."
                        value={assignNote}
                        onChange={(e) => setAssignNote(e.target.value)}
                        className="bg-background text-sm"
                      />
                    </div>
                  </div>
                  <div className="flex justify-end">
                    <Button
                      size="sm"
                      onClick={handleAssignToStaff}
                      disabled={assigning || !selectedStaffId}
                      className="gap-1.5 bg-blue-600 hover:bg-blue-700 text-white"
                    >
                      {assigning ? <Loader2 className="size-4 animate-spin" /> : <UserCheck className="size-4" />}
                      <span>Xác nhận phân việc</span>
                    </Button>
                  </div>
                </CardContent>
              </Card>

              {/* KHỐI NGHIỆP VỤ 2: NHÂN VIÊN BÁO CÁO HOÀN TẤT & TRƯỞNG PHÒNG THÔNG BÁO CHO KHÁCH HÀNG */}
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                {/* 2.1 Dành cho nhân viên được giao việc */}
                <Card className="border-border shadow-sm border-purple-500/30 bg-purple-500/5">
                  <CardHeader className="pb-3">
                    <div className="flex items-center gap-2">
                      <FolderKanban className="size-5 text-purple-600 dark:text-purple-400" />
                      <div>
                        <CardTitle className="text-sm font-semibold text-purple-900 dark:text-purple-200">
                          Nhân viên: Báo cáo hoàn tất
                        </CardTitle>
                        <CardDescription className="text-xs">
                          Nhân viên xử lý xong và gửi kết quả lên Trưởng phòng
                        </CardDescription>
                      </div>
                    </div>
                  </CardHeader>
                  <CardContent className="space-y-3">
                    <Textarea
                      rows={2}
                      placeholder="Nhập chi tiết các bước đã xử lý xong để báo cáo Trưởng phòng..."
                      value={staffResolution}
                      onChange={(e) => setStaffResolution(e.target.value)}
                      className="bg-background text-xs"
                    />
                    <Button
                      size="sm"
                      variant="outline"
                      disabled={staffCompleting || !staffResolution.trim()}
                      onClick={handleStaffComplete}
                      className="w-full text-xs gap-1.5 border-purple-300 text-purple-700 hover:bg-purple-100 dark:border-purple-800 dark:text-purple-300"
                    >
                      {staffCompleting && <Loader2 className="size-3.5 animate-spin" />}
                      <span>Báo cáo hoàn tất tới Trưởng phòng</span>
                    </Button>
                  </CardContent>
                </Card>

                {/* 2.2 Dành cho Trưởng phòng thông báo cho khách hàng */}
                <Card className="border-border shadow-sm border-emerald-500/30 bg-emerald-500/5">
                  <CardHeader className="pb-3">
                    <div className="flex items-center gap-2">
                      <CheckCircle2 className="size-5 text-emerald-600 dark:text-emerald-400" />
                      <div>
                        <CardTitle className="text-sm font-semibold text-emerald-900 dark:text-emerald-200">
                          Trưởng phòng: Thông báo kết quả cho khách
                        </CardTitle>
                        <CardDescription className="text-xs">
                          Duyệt kết quả và chính thức phản hồi lại cho khách hàng
                        </CardDescription>
                      </div>
                    </div>
                  </CardHeader>
                  <CardContent className="space-y-3">
                    <Textarea
                      rows={2}
                      placeholder="Phương án / Kết quả giải quyết gửi khách hàng..."
                      value={headResolution}
                      onChange={(e) => setHeadResolution(e.target.value)}
                      className="bg-background text-xs"
                    />
                    <Button
                      size="sm"
                      disabled={resolving || !headResolution.trim()}
                      onClick={handleHeadResolve}
                      className="w-full text-xs gap-1.5 bg-emerald-600 hover:bg-emerald-700 text-white"
                    >
                      {resolving ? <Loader2 className="size-3.5 animate-spin" /> : <Send className="size-3.5" />}
                      <span>Thông báo kết quả & Đóng ticket</span>
                    </Button>
                  </CardContent>
                </Card>
              </div>

              {/* Lịch sử trao đổi tin nhắn */}
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
                          Trao đổi giữa Khách hàng, Trưởng phòng ({ticket.department}) và Nhân viên xử lý
                        </CardDescription>
                      </div>
                    </div>
                    <Badge variant="outline" className="text-[11px] font-normal">
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
                              <span>{msg.senderName}</span>
                            </>
                          ) : (
                            <>
                              <User className="size-4 text-muted-foreground" />
                              <span>{msg.senderName} (Khách hàng)</span>
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
                      <span className="font-medium">{replyAttachmentName}</span>
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
                    placeholder="Nhập nội dung phản hồi trao đổi thêm..."
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

            {/* Cột phải (4/12): Thông tin điều phối, phòng ban, nhân viên */}
            <div className="space-y-6 lg:col-span-5 xl:col-span-4">
              <Card className="border-border shadow-sm">
                <CardHeader className="border-b pb-4">
                  <div className="flex items-center gap-2.5">
                    <div className="flex size-9 items-center justify-center rounded-lg bg-secondary text-foreground">
                      <Building2 className="size-5" />
                    </div>
                    <div>
                      <CardTitle className="text-base font-semibold">
                        Thông tin Điều phối
                      </CardTitle>
                      <CardDescription className="text-xs">
                        Chi tiết người tiếp nhận và phân công xử lý
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
                          onClick={() => handleCopy(ticket.ticketCode, "mã ticket")}
                          className="p-0.5 rounded hover:bg-muted text-muted-foreground hover:text-foreground"
                        >
                          <Copy className="size-3" />
                        </button>
                      </div>
                    </div>

                    <div className="flex items-center justify-between">
                      <span className="text-muted-foreground">Trạng thái:</span>
                      <span className="font-semibold text-foreground">
                        {TICKET_STATUS_LABELS[ticket.ticketStatus] ?? ticket.ticketStatus}
                      </span>
                    </div>

                    <div className="flex items-center justify-between">
                      <span className="text-muted-foreground">Mức độ ưu tiên:</span>
                      <Badge
                        variant="outline"
                        className={cn(
                          "text-[11px]",
                          ticket.ticketPriority === TicketPriority.Urgent &&
                            "border-rose-500/30 bg-rose-500/10 text-rose-600",
                        )}
                      >
                        {TICKET_PRIORITY_LABELS[ticket.ticketPriority] ?? ticket.ticketPriority}
                      </Badge>
                    </div>

                    <div className="flex items-center justify-between">
                      <span className="text-muted-foreground">Phòng ban tiếp nhận:</span>
                      <span className="font-medium text-foreground">{ticket.department}</span>
                    </div>

                    <div className="flex items-center justify-between">
                      <span className="text-muted-foreground">Trưởng phòng (Bên nhận ban đầu):</span>
                      <span className="font-semibold text-foreground text-right text-blue-600 dark:text-blue-400">
                        {ticket.departmentHeadName || "Trưởng phòng ban"}
                      </span>
                    </div>

                    <div className="flex items-center justify-between gap-2">
                      <span className="text-muted-foreground shrink-0">Nhân viên được phân công:</span>
                      <span className="font-semibold text-primary text-right">
                        {ticket.assignedEmployeeName || "Chưa giao (Trưởng phòng đang giữ)"}
                      </span>
                    </div>

                    <div className="flex items-center justify-between">
                      <span className="text-muted-foreground">Thời gian tạo:</span>
                      <span className="text-foreground flex items-center gap-1">
                        <Clock className="size-3 text-muted-foreground" />
                        {new Date(ticket.createdAt).toLocaleString("vi-VN")}
                      </span>
                    </div>
                  </div>

                  {/* Lịch sử phân công (TICKET_SUPPORT_EMPLOYEE) */}
                  {ticket.assignments && ticket.assignments.length > 0 && (
                    <div className="mt-4 pt-3 border-t">
                      <p className="font-semibold text-xs text-foreground mb-2 flex items-center gap-1.5">
                        <UserCheck className="size-3.5 text-primary" />
                        Lịch sử phân công (TICKET_SUPPORT_EMPLOYEE):
                      </p>
                      <div className="space-y-2">
                        {ticket.assignments.map((asg) => (
                          <div
                            key={asg.ticketSupportEmployeeId}
                            className="p-2.5 rounded-lg border bg-background/50 text-[11px] space-y-1"
                          >
                            <div className="flex items-center justify-between">
                              <span className="font-medium text-foreground">
                                {asg.employeeName}
                              </span>
                              <Badge variant="secondary" className="text-[10px]">
                                {asg.assignmentRole === "DEPARTMENT_HEAD"
                                  ? "Trưởng phòng tiếp nhận"
                                  : "Nhân viên xử lý"}
                              </Badge>
                            </div>
                            {asg.assignedByName && (
                              <p className="text-muted-foreground">
                                Phân công bởi: {asg.assignedByName}
                              </p>
                            )}
                            {asg.assignedAt && (
                              <p className="text-muted-foreground text-[10px]">
                                Thời gian: {new Date(asg.assignedAt).toLocaleString("vi-VN")}
                              </p>
                            )}
                          </div>
                        ))}
                      </div>
                    </div>
                  )}
                </CardContent>
              </Card>
            </div>
          </div>
        )}
      </div>
    </>
  );
}

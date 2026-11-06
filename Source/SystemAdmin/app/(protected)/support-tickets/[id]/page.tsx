"use client";

import { useCallback, useEffect, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import {
  ArrowLeft,
  Building2,
  CheckCircle2,
  Clock,
  Copy,
  FileText,
  FolderKanban,
  Loader2,
  MessageSquare,
  Paperclip,
  RefreshCw,
  Send,
  ShieldCheck,
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
import { useAuthStore } from "@/hooks/use-auth-store";
import { cn } from "@/lib/utils";
import {
  DepartmentStaffDto,
  ticketApi,
  TicketDto,
  TicketPriority,
  TicketStatus,
  TICKET_PRIORITY_LABELS,
} from "@/services/ticket-api";

const QUICK_RESPONSES = [
  "Chào anh/chị, phòng ban chuyên môn đã tiếp nhận yêu cầu và đang tiến hành kiểm tra xử lý.",
  "Yêu cầu của anh/chị đã được xử lý hoàn tất trên hệ thống. Anh/chị vui lòng kiểm tra lại giúp em nhé.",
  "Anh/chị vui lòng cung cấp thêm ảnh chụp màn hình hoặc mã hợp đồng liên quan để bộ phận chuyên môn kiểm tra chi tiết hơn.",
];

function TicketStatusBadge({ status }: { status: TicketStatus | string }) {
  const config: Record<string, { label: string; className: string }> = {
    [TicketStatus.Open]: {
      label: "Chờ Trưởng phòng tiếp nhận",
      className:
        "bg-amber-50 text-amber-700 border-amber-200 dark:bg-amber-500/10 dark:text-amber-400 dark:border-amber-500/20",
    },
    [TicketStatus.InProgress]: {
      label: "Đang xử lý (Đã phân công nhân viên)",
      className:
        "bg-cyan-50 text-cyan-700 border-cyan-200 dark:bg-cyan-500/10 dark:text-cyan-400 dark:border-cyan-500/20",
    },
    [TicketStatus.PendingHeadApproval]: {
      label: "Chờ Trưởng phòng duyệt kết quả",
      className:
        "bg-purple-50 text-purple-700 border-purple-200 dark:bg-purple-500/10 dark:text-purple-400 dark:border-purple-500/20",
    },
    [TicketStatus.Resolved]: {
      label: "Đã xử lý hoàn tất",
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
  const user = useAuthStore((state) => state.user);

  const [ticket, setTicket] = useState<TicketDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [replyMessage, setReplyMessage] = useState("");
  const [sendingReply, setSendingReply] = useState(false);

  const [departmentStaff, setDepartmentStaff] = useState<DepartmentStaffDto[]>(
    [],
  );
  const [loadingStaff, setLoadingStaff] = useState(false);
  const [selectedStaffId, setSelectedStaffId] = useState<string>("");
  const [assignNote, setAssignNote] = useState("");
  const [assigning, setAssigning] = useState(false);

  const [staffResolution, setStaffResolution] = useState("");
  const [staffCompleting, setStaffCompleting] = useState(false);

  const [headResolution, setHeadResolution] = useState("");
  const [resolving, setResolving] = useState(false);

  const isSysAdmin =
    user?.supportRole === "SYSADMIN" ||
    user?.username?.toLowerCase() === "sysadmin" ||
    user?.username?.toLowerCase() === "admin";

  useEffect(() => {
    if (isSysAdmin) {
      router.replace("/dashboard");
    }
  }, [isSysAdmin, router]);

  const loadStaffList = useCallback(async () => {
    if (!ticketId || isSysAdmin) return;
    setLoadingStaff(true);
    try {
      const staff = await ticketApi.getDepartmentStaff(ticketId);
      setDepartmentStaff(staff);
    } catch {
      toast.error("Không thể tải danh sách nhân viên trong phòng ban");
    } finally {
      setLoadingStaff(false);
    }
  }, [ticketId, isSysAdmin]);

  const loadTicket = useCallback(async () => {
    if (!Number.isFinite(ticketId) || ticketId <= 0 || isSysAdmin) return;
    setLoading(true);
    try {
      const data = await ticketApi.getById(ticketId);
      setTicket(data);
      if (data) {
        if (data.assignedEmployeeId) {
          setSelectedStaffId(String(data.assignedEmployeeId));
        }
        if (data.ticketResolution) {
          setHeadResolution(data.ticketResolution);
          setStaffResolution(data.ticketResolution);
        }
      }
    } catch {
      toast.error(
        "Không tìm thấy yêu cầu hỗ trợ hoặc bạn không có quyền xem yêu cầu này",
      );
      setTicket(null);
    } finally {
      setLoading(false);
    }
  }, [ticketId, isSysAdmin]);

  useEffect(() => {
    void loadTicket();
  }, [loadTicket]);

  const effectiveRole =
    ticket?.viewerRole || user?.supportRole || "STAFF";
  const isDepartmentHead = effectiveRole === "DEPARTMENT_HEAD";
  const isStaff = effectiveRole === "STAFF";

  useEffect(() => {
    if (ticket && isDepartmentHead) {
      void loadStaffList();
    }
  }, [ticket, isDepartmentHead, loadStaffList]);

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

  const handleAssignToStaff = async () => {
    if (!selectedStaffId) {
      toast.error("Vui lòng chọn nhân viên trong phòng ban để phân công.");
      return;
    }
    setAssigning(true);
    try {
      const updated = await ticketApi.assignToStaff(ticketId, {
        staffEmployeeId: Number(selectedStaffId),
        note: assignNote.trim(),
      });
      toast.success("Đã phân công yêu cầu cho nhân viên trong phòng ban!");
      setTicket(updated);
      setAssignNote("");
    } catch {
      toast.error("Không thể phân công nhân viên xử lý.");
    } finally {
      setAssigning(false);
    }
  };

  const handleStaffSendToHead = async () => {
    if (!staffResolution.trim()) {
      toast.error("Vui lòng nhập nội dung kết quả xử lý trước khi gửi.");
      return;
    }
    setStaffCompleting(true);
    try {
      const updated = await ticketApi.staffComplete(ticketId, {
        resolution: staffResolution.trim(),
      });
      toast.success("Đã gửi báo cáo kết quả xử lý lên Trưởng phòng!");
      setTicket(updated);
    } catch {
      toast.error("Lỗi khi gửi kết quả xử lý lên Trưởng phòng.");
    } finally {
      setStaffCompleting(false);
    }
  };

  const handleSendResultToCustomer = async (contentText: string) => {
    if (!contentText.trim()) {
      toast.error("Vui lòng nhập nội dung kết quả xử lý để gửi cho khách hàng.");
      return;
    }
    setResolving(true);
    try {
      const updated = await ticketApi.headResolve(ticketId, {
        resolution: contentText.trim(),
        closeTicket: true,
      });
      toast.success("Đã gửi kết quả xử lý cho khách hàng và hoàn tất yêu cầu!");
      setTicket(updated);
    } catch {
      toast.error("Lỗi khi gửi kết quả xử lý cho khách hàng.");
    } finally {
      setResolving(false);
    }
  };

  const handleSendReply = async () => {
    if (!ticket || !replyMessage.trim()) return;

    setSendingReply(true);
    try {
      const newMsg = await ticketApi.replyMessage(ticket.ticketSupportId, {
        messageContent: replyMessage.trim(),
      });
      toast.success("Đã gửi tin nhắn trao đổi với khách hàng");
      setTicket((prev) => {
        if (!prev) return null;
        return {
          ...prev,
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

  return (
    <>
      <Header title="Chi tiết & Xử lý Yêu cầu Hỗ trợ" />

      <div className="grow overflow-y-auto p-2 lg:p-10 space-y-6">
        {/* Page Header */}
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
            <div className="flex flex-wrap items-center gap-2.5">
              <h1 className="text-2xl font-bold tracking-tight text-foreground font-mono">
                {ticket ? ticket.ticketCode : "Đang tải..."}
              </h1>
              {ticket && <TicketStatusBadge status={ticket.ticketStatus} />}
              {ticket && (
                <Badge variant="secondary" className="font-medium">
                  {ticket.department}
                </Badge>
              )}
            </div>
            {ticket && (
              <p className="mt-2 max-w-3xl text-base font-medium text-foreground">
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
          </div>
        </div>

        {loading && !ticket ? (
          <Card className="border-border shadow-sm p-12 text-center text-sm text-muted-foreground">
            <Loader2 className="size-6 animate-spin mx-auto mb-2 text-primary" />
            Đang tải dữ liệu yêu cầu hỗ trợ...
          </Card>
        ) : !ticket ? (
          <Card className="border-border shadow-sm p-12 text-center space-y-3">
            <p className="text-sm text-muted-foreground">
              Không tìm thấy yêu cầu hỗ trợ này hoặc yêu cầu không thuộc phạm vi
              phụ trách của bạn.
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
            {/* Cột chính (8/12): Nội dung yêu cầu, Khối nghiệp vụ theo đúng vai trò & Hội thoại */}
            <div className="space-y-6 lg:col-span-7 xl:col-span-8">
              {/* Nội dung yêu cầu ban đầu từ Khách hàng */}
              <Card className="border-border shadow-sm">
                <CardHeader className="border-b pb-3">
                  <div className="flex items-center justify-between gap-2">
                    <div className="flex items-center gap-2.5">
                      <div className="flex size-9 items-center justify-center rounded-lg bg-primary/10 text-primary">
                        <FileText className="size-5" />
                      </div>
                      <div>
                        <CardTitle className="text-base font-semibold">
                          Nội dung yêu cầu hỗ trợ từ Khách hàng
                        </CardTitle>
                        <CardDescription className="text-xs">
                          Gửi bởi {ticket.createdByName || ticket.tenantName} •{" "}
                          {new Date(ticket.createdAt).toLocaleString("vi-VN")}
                        </CardDescription>
                      </div>
                    </div>
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
                </CardHeader>
                <CardContent className="pt-4">
                  <div className="rounded-xl border bg-muted/20 p-4 text-sm leading-relaxed whitespace-pre-wrap text-foreground">
                    {ticket.ticketDescription}
                  </div>
                </CardContent>
              </Card>

              {/* VAI TRÒ 1: TRƯỞNG PHÒNG (DEPARTMENT_HEAD) -> Phân công nhân viên & Duyệt gửi kết quả cho khách hàng */}
              {isDepartmentHead && (
                <div className="space-y-6">
                  {/* Khối 1 của Trưởng phòng: Phân công nhân viên trong phòng ban */}
                  <Card className="border-blue-500/30 bg-blue-500/5 shadow-sm">
                    <CardHeader className="pb-3">
                      <div className="flex items-center gap-2.5">
                        <div className="flex size-9 items-center justify-center rounded-lg bg-blue-600/10 text-blue-600 dark:text-blue-400">
                          <UserPlus className="size-5" />
                        </div>
                        <div>
                          <CardTitle className="text-base font-semibold text-blue-950 dark:text-blue-200">
                            Phân công nhân viên trong phòng xử lý ({ticket.department})
                          </CardTitle>
                          <CardDescription className="text-xs">
                            Chọn nhân viên thuộc {ticket.department} để tiếp nhận và xử lý yêu cầu này
                          </CardDescription>
                        </div>
                      </div>
                    </CardHeader>
                    <CardContent className="space-y-4 text-sm">
                      <div className="space-y-2">
                        <Label className="text-sm font-medium">
                          Nhân viên phụ trách xử lý:
                        </Label>
                        <Select
                          value={selectedStaffId}
                          onValueChange={setSelectedStaffId}
                          onOpenChange={(open) => {
                            if (open && departmentStaff.length === 0) {
                              void loadStaffList();
                            }
                          }}
                        >
                          <SelectTrigger className="h-10 bg-background text-sm">
                            <SelectValue
                              placeholder={
                                loadingStaff
                                  ? "Đang tải danh sách nhân viên trong phòng..."
                                  : "Chọn nhân viên trong phòng ban để giao việc"
                              }
                            />
                          </SelectTrigger>
                          <SelectContent>
                            {departmentStaff.map((staff) => (
                              <SelectItem
                                key={staff.employeeId}
                                value={String(staff.employeeId)}
                              >
                                {staff.employeeName}{" "}
                                {staff.employeeCode
                                  ? `(${staff.employeeCode})`
                                  : ""}{" "}
                                {staff.employeeEmail
                                  ? `— ${staff.employeeEmail}`
                                  : ""}
                              </SelectItem>
                            ))}
                          </SelectContent>
                        </Select>
                      </div>

                      <div className="space-y-2">
                        <Label className="text-sm font-medium">
                          Nội dung chỉ đạo / Ghi chú giao việc cho nhân viên:
                        </Label>
                        <Textarea
                          rows={4}
                          maxLength={2000}
                          placeholder="Nhập hướng dẫn xử lý, yêu cầu chi tiết hoặc thời hạn hoàn thành cho nhân viên..."
                          value={assignNote}
                          onChange={(e) => setAssignNote(e.target.value)}
                          className="min-h-[110px] resize-y bg-background text-sm leading-relaxed"
                        />
                      </div>

                      <div className="flex justify-end">
                        <Button
                          onClick={handleAssignToStaff}
                          disabled={assigning || !selectedStaffId}
                          className="h-10 px-5 gap-2 bg-blue-600 hover:bg-blue-700 text-white shadow-sm"
                        >
                          {assigning ? (
                            <Loader2 className="size-4 animate-spin" />
                          ) : (
                            <UserCheck className="size-4" />
                          )}
                          <span>Xác nhận phân công nhân viên</span>
                        </Button>
                      </div>
                    </CardContent>
                  </Card>

                  {/* Khối 2 của Trưởng phòng: Gửi kết quả xử lý cho Khách hàng */}
                  <Card className="border-emerald-500/30 bg-emerald-500/5 shadow-sm">
                    <CardHeader className="pb-3">
                      <div className="flex items-center gap-2.5">
                        <div className="flex size-9 items-center justify-center rounded-lg bg-emerald-600/10 text-emerald-600 dark:text-emerald-400">
                          <CheckCircle2 className="size-5" />
                        </div>
                        <div>
                          <CardTitle className="text-base font-semibold text-emerald-950 dark:text-emerald-200">
                            Phản hồi kết quả giải quyết cho Khách hàng
                          </CardTitle>
                          <CardDescription className="text-xs">
                            Duyệt báo cáo xử lý từ nhân viên hoặc trực tiếp gửi kết quả phản hồi cho khách hàng và đóng yêu cầu
                          </CardDescription>
                        </div>
                      </div>
                    </CardHeader>
                    <CardContent className="space-y-4 text-sm">
                      <div className="space-y-2">
                        <Label className="text-sm font-medium">
                          Nội dung kết quả giải quyết gửi tới Khách hàng:
                        </Label>
                        <Textarea
                          rows={5}
                          maxLength={3000}
                          placeholder="Nhập chi tiết phương án và kết quả giải quyết để thông báo cho khách hàng..."
                          value={headResolution}
                          onChange={(e) => setHeadResolution(e.target.value)}
                          className="min-h-[140px] resize-y bg-background text-sm leading-relaxed"
                        />
                      </div>
                      <div className="flex justify-end">
                        <Button
                          disabled={resolving || !headResolution.trim()}
                          onClick={() =>
                            handleSendResultToCustomer(headResolution)
                          }
                          className="h-10 px-5 gap-2 bg-emerald-600 hover:bg-emerald-700 text-white shadow-sm"
                        >
                          {resolving ? (
                            <Loader2 className="size-4 animate-spin" />
                          ) : (
                            <Send className="size-4" />
                          )}
                          <span>Gửi kết quả cho Khách hàng & Hoàn tất</span>
                        </Button>
                      </div>
                    </CardContent>
                  </Card>
                </div>
              )}

              {/* VAI TRÒ 2: NHÂN VIÊN ĐƯỢC PHÂN CÔNG (STAFF) -> Tiếp nhận xử lý & Gửi kết quả cho Trưởng phòng hoặc Khách hàng */}
              {isStaff && (
                <Card className="border-primary/30 bg-primary/5 shadow-sm">
                  <CardHeader className="pb-3">
                    <div className="flex items-center gap-2.5">
                      <div className="flex size-9 items-center justify-center rounded-lg bg-primary/15 text-primary">
                        <FolderKanban className="size-5" />
                      </div>
                      <div>
                        <CardTitle className="text-base font-semibold text-foreground">
                          Xử lý yêu cầu & Gửi kết quả ({ticket.department})
                        </CardTitle>
                        <CardDescription className="text-xs">
                          Sau khi xử lý xong yêu cầu được Trưởng phòng giao, bạn có thể gửi kết quả lên Trưởng phòng hoặc gửi trực tiếp cho Khách hàng
                        </CardDescription>
                      </div>
                    </div>
                  </CardHeader>
                  <CardContent className="space-y-4 text-sm">
                    <div className="space-y-2">
                      <Label className="text-sm font-medium">
                        Nội dung kết quả xử lý yêu cầu:
                      </Label>
                      <Textarea
                        rows={6}
                        maxLength={3000}
                        placeholder="Nhập đầy đủ nguyên nhân, các bước đã xử lý và kết quả khắc phục yêu cầu hỗ trợ..."
                        value={staffResolution}
                        onChange={(e) => setStaffResolution(e.target.value)}
                        className="min-h-[160px] resize-y bg-background text-sm leading-relaxed"
                      />
                    </div>

                    <div className="flex flex-col sm:flex-row items-stretch sm:items-center justify-end gap-3 pt-1">
                      <Button
                        variant="outline"
                        disabled={
                          staffCompleting ||
                          resolving ||
                          !staffResolution.trim()
                        }
                        onClick={handleStaffSendToHead}
                        className="h-10 px-4 gap-2 border-primary/40 text-primary hover:bg-primary/10"
                      >
                        {staffCompleting ? (
                          <Loader2 className="size-4 animate-spin" />
                        ) : (
                          <UserCheck className="size-4" />
                        )}
                        <span>Gửi kết quả cho Trưởng phòng</span>
                      </Button>

                      <Button
                        disabled={
                          resolving ||
                          staffCompleting ||
                          !staffResolution.trim()
                        }
                        onClick={() =>
                          handleSendResultToCustomer(staffResolution)
                        }
                        className="h-10 px-5 gap-2 bg-emerald-600 hover:bg-emerald-700 text-white shadow-sm"
                      >
                        {resolving ? (
                          <Loader2 className="size-4 animate-spin" />
                        ) : (
                          <Send className="size-4" />
                        )}
                        <span>Gửi kết quả cho Khách hàng & Hoàn tất</span>
                      </Button>
                    </div>
                  </CardContent>
                </Card>
              )}

              {/* Luồng trao đổi tin nhắn với Khách hàng */}
              <Card className="border-border shadow-sm">
                <CardHeader className="border-b pb-4">
                  <div className="flex flex-wrap items-center justify-between gap-2">
                    <div className="flex items-center gap-2.5">
                      <div className="flex size-9 items-center justify-center rounded-lg bg-primary/10 text-primary">
                        <MessageSquare className="size-5" />
                      </div>
                      <div>
                        <CardTitle className="text-base font-semibold">
                          Lịch sử xử lý & Trao đổi thông tin
                        </CardTitle>
                        <CardDescription className="text-xs">
                          Toàn bộ nhật ký phân công, báo cáo hoàn tất và tin nhắn trao đổi với Khách hàng
                        </CardDescription>
                      </div>
                    </div>

                    <Badge variant="outline" className="text-xs font-normal">
                      {ticket.messages?.length ?? 0} phản hồi
                    </Badge>
                  </div>
                </CardHeader>

                <CardContent className="space-y-4 pt-5 max-h-[500px] overflow-y-auto">
                  {ticket.messages?.map((msg) => (
                    <div
                      key={msg.ticketMessageId}
                      className={cn(
                        "rounded-xl border p-4 text-sm max-w-[90%]",
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

                <div className="border-t p-5 space-y-4 bg-muted/10">
                  <div className="space-y-1.5">
                    <span className="text-xs font-medium text-muted-foreground">
                      Mẫu câu trao đổi nhanh:
                    </span>
                    <div className="flex flex-wrap gap-1.5">
                      {QUICK_RESPONSES.map((tpl, i) => (
                        <button
                          key={i}
                          type="button"
                          onClick={() => setReplyMessage(tpl)}
                          className="text-xs bg-background hover:bg-secondary text-foreground px-3 py-1.5 rounded-lg transition-colors truncate max-w-[360px] border border-border"
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
                    rows={4}
                    maxLength={3000}
                    placeholder="Nhập tin nhắn trao đổi thêm với khách hàng hoặc ghi chú tiến độ xử lý..."
                    className="min-h-[120px] resize-y bg-background text-sm leading-relaxed"
                  />

                  <div className="flex justify-end">
                    <Button
                      disabled={!replyMessage.trim() || sendingReply}
                      onClick={handleSendReply}
                      className="h-10 gap-2 px-5 shadow-sm"
                    >
                      {sendingReply ? (
                        <Loader2 className="size-4 animate-spin" />
                      ) : (
                        <Send className="size-4" />
                      )}
                      <span>Gửi tin nhắn trao đổi</span>
                    </Button>
                  </div>
                </div>
              </Card>
            </div>

            {/* Cột phải (4/12): Thông tin Tổ chức & Điều phối */}
            <div className="space-y-6 lg:col-span-5 xl:col-span-4">
              <Card className="border-border shadow-sm">
                <CardHeader className="border-b pb-4">
                  <div className="flex items-center gap-2.5">
                    <div className="flex size-9 items-center justify-center rounded-lg bg-secondary text-foreground">
                      <Building2 className="size-5" />
                    </div>
                    <div>
                      <CardTitle className="text-base font-semibold">
                        Thông tin Khách hàng & Điều phối
                      </CardTitle>
                      <CardDescription className="text-xs">
                        Chi tiết phòng ban tiếp nhận và nhân sự xử lý
                      </CardDescription>
                    </div>
                  </div>
                </CardHeader>
                <CardContent className="pt-5">
                  <div className="rounded-xl border bg-muted/30 p-4 space-y-3 text-xs">
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
                        Khách hàng / Tổ chức:
                      </span>
                      <span className="font-semibold text-foreground text-right">
                        {ticket.tenantName}
                      </span>
                    </div>

                    <div className="flex items-center justify-between">
                      <span className="text-muted-foreground">Mã định danh:</span>
                      <div className="flex items-center gap-1.5">
                        <span className="font-mono bg-background px-2 py-0.5 rounded border">
                          {ticket.tenantCode}
                        </span>
                        <button
                          type="button"
                          onClick={() =>
                            handleCopy(ticket.tenantCode, "mã khách hàng")
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
                          Người gửi yêu cầu:
                        </span>
                        <span className="font-medium text-foreground text-right">
                          {ticket.createdByName}
                        </span>
                      </div>
                    )}

                    <div className="flex items-center justify-between gap-2">
                      <span className="text-muted-foreground shrink-0">
                        Phòng ban tiếp nhận:
                      </span>
                      <span className="font-semibold text-foreground text-right">
                        {ticket.department}
                      </span>
                    </div>

                    <div className="flex items-center justify-between gap-2">
                      <span className="text-muted-foreground shrink-0">
                        Trưởng phòng phụ trách:
                      </span>
                      <span className="font-semibold text-blue-600 dark:text-blue-400 text-right">
                        {ticket.departmentHeadName || "Trưởng phòng ban"}
                      </span>
                    </div>

                    <div className="flex items-center justify-between gap-2">
                      <span className="text-muted-foreground shrink-0">
                        Nhân viên được giao:
                      </span>
                      <span className="font-semibold text-primary text-right">
                        {ticket.assignedEmployeeName || "Chưa phân công"}
                      </span>
                    </div>

                    <div className="flex items-center justify-between">
                      <span className="text-muted-foreground">
                        Thời gian gửi:
                      </span>
                      <span className="text-foreground flex items-center gap-1">
                        <Clock className="size-3 text-muted-foreground" />
                        {new Date(ticket.createdAt).toLocaleString("vi-VN")}
                      </span>
                    </div>
                  </div>

                  {ticket.assignments && ticket.assignments.length > 0 && (
                    <div className="mt-4 pt-3 border-t">
                      <p className="font-semibold text-xs text-foreground mb-2 flex items-center gap-1.5">
                        <UserCheck className="size-3.5 text-primary" />
                        Lịch sử điều phối phòng ban:
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
                                Thời gian:{" "}
                                {new Date(asg.assignedAt).toLocaleString(
                                  "vi-VN",
                                )}
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

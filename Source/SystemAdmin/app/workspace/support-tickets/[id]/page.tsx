"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import {
  ArrowLeft,
  Building2,
  Calendar,
  CheckCheck,
  CheckCircle2,
  ClipboardCheck,
  Clock,
  Copy,
  FileText,
  History,
  Layers,
  Loader2,
  MessageCircle,
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
import { useTicketUser } from "@/components/internal/ticket-user";
import { cn } from "@/lib/utils";
import {
  DepartmentStaffDto,
  ticketApi,
  TicketDto,
  TicketPriority,
  TicketStatus,
  TICKET_PRIORITY_LABELS,
} from "@/services/internal-ticket-api";

const QUICK_RESPONSES = [
  "Chào anh/chị, phòng ban chuyên môn đã tiếp nhận yêu cầu và đang tiến hành kiểm tra xử lý.",
  "Yêu cầu của anh/chị đã được xử lý hoàn tất trên hệ thống. Anh/chị vui lòng kiểm tra lại giúp em nhé.",
  "Anh/chị vui lòng cung cấp thêm ảnh chụp màn hình hoặc mã hợp đồng liên quan để bộ phận chuyên môn kiểm tra chi tiết hơn.",
];

function formatZaloTime(dateStr?: string | null) {
  if (!dateStr) return "--:--";
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
    <Badge variant="outline" className={cn("font-semibold", current.className)}>
      {current.label}
    </Badge>
  );
}

function TicketPriorityBadge({
  priority,
}: {
  priority: TicketPriority | string;
}) {
  const config: Record<string, { label: string; className: string }> = {
    [TicketPriority.Urgent]: {
      label: "Ưu tiên: Khẩn cấp",
      className:
        "bg-rose-50 text-rose-700 border-rose-200 dark:bg-rose-500/15 dark:text-rose-300 dark:border-rose-500/30",
    },
    [TicketPriority.High]: {
      label: "Ưu tiên: Cao",
      className:
        "bg-amber-50 text-amber-700 border-amber-200 dark:bg-amber-500/15 dark:text-amber-300 dark:border-amber-500/30",
    },
    [TicketPriority.Medium]: {
      label: "Ưu tiên: Bình thường",
      className: "bg-primary/10 text-primary border-primary/20",
    },
    [TicketPriority.Low]: {
      label: "Ưu tiên: Thấp",
      className: "bg-muted text-muted-foreground border-border",
    },
  };
  const current = config[priority] ?? config[TicketPriority.Medium];
  return (
    <Badge variant="outline" className={cn("font-semibold", current.className)}>
      {current.label}
    </Badge>
  );
}

export default function AdminSupportTicketDetailPage() {
  const params = useParams<{ id: string }>();
  const router = useRouter();
  const ticketId = Number(params?.id);
  const user = useTicketUser();

  const [ticket, setTicket] = useState<TicketDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [activeTab, setActiveTab] = useState<"workflow" | "chat" | "history">(
    "workflow",
  );

  const [replyMessage, setReplyMessage] = useState("");
  const [sendingReply, setSendingReply] = useState(false);

  const [departmentStaff, setDepartmentStaff] = useState<DepartmentStaffDto[]>(
    [],
  );
  const [loadingStaff, setLoadingStaff] = useState(false);
  const [selectedStaffId, setSelectedStaffId] = useState<string>("");
  const [selectedPriority, setSelectedPriority] = useState<TicketPriority>(
    TicketPriority.Medium,
  );
  const [assignNote, setAssignNote] = useState("");
  const [assigning, setAssigning] = useState(false);

  const [staffResolution, setStaffResolution] = useState("");
  const [staffCompleting, setStaffCompleting] = useState(false);

  const [headResolution, setHeadResolution] = useState("");
  const [resolving, setResolving] = useState(false);

  const chatBottomRef = useRef<HTMLDivElement | null>(null);
  const prevMsgCountRef = useRef<number>(0);
  const initializedResolutionRef = useRef<boolean>(false);

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

  const loadTicket = useCallback(
    async (silent = false) => {
      if (!Number.isFinite(ticketId) || ticketId <= 0 || isSysAdmin) return;
      if (!silent) setLoading(true);
      try {
        const data = await ticketApi.getById(ticketId);
        if (data) {
          setTicket(data);
          if (!silent) {
            if (data.assignedEmployeeId) {
              setSelectedStaffId(String(data.assignedEmployeeId));
            }
            if (data.ticketPriority) {
              setSelectedPriority(data.ticketPriority);
            }
          }
          if (!initializedResolutionRef.current && data.ticketResolution) {
            initializedResolutionRef.current = true;
            setHeadResolution(data.ticketResolution);
            setStaffResolution(data.ticketResolution);
          }
        }
      } catch {
        if (!silent) {
          toast.error(
            "Không tìm thấy yêu cầu hỗ trợ hoặc bạn không có quyền xem yêu cầu này",
          );
          setTicket(null);
        }
      } finally {
        if (!silent) setLoading(false);
      }
    },
    [ticketId, isSysAdmin],
  );

  useEffect(() => {
    void loadTicket(false);
  }, [loadTicket]);

  // Tự động đồng bộ tin nhắn và trạng thái mới mỗi 2 giây (không cần bấm Làm mới)
  useEffect(() => {
    const timer = setInterval(() => {
      void loadTicket(true);
    }, 2000);
    return () => clearInterval(timer);
  }, [loadTicket]);

  const effectiveRole = ticket?.viewerRole || user?.supportRole || "STAFF";
  const isDepartmentHead = effectiveRole === "DEPARTMENT_HEAD";
  const isStaff = !isDepartmentHead;

  // Nếu là nhân viên được phân công (STAFF) thì không bao giờ ở tab chat
  useEffect(() => {
    if (isStaff && activeTab === "chat") {
      setActiveTab("workflow");
    }
  }, [isStaff, activeTab]);

  useEffect(() => {
    if (ticket && isDepartmentHead) {
      void loadStaffList();
    }
  }, [ticket?.ticketSupportId, isDepartmentHead, loadStaffList]);

  const chatMessages = (ticket?.messages ?? []).filter(
    (m) =>
      m.ticketMessageType !== "STAFF_REPORT" &&
      m.ticketMessageType !== "ASSIGNMENT",
  );

  const internalReports = (ticket?.messages ?? []).filter(
    (m) =>
      m.ticketMessageType === "STAFF_REPORT" ||
      m.ticketMessageType === "ASSIGNMENT",
  );

  useEffect(() => {
    if (chatMessages.length !== prevMsgCountRef.current) {
      prevMsgCountRef.current = chatMessages.length;
      if (activeTab === "chat") {
        setTimeout(() => {
          chatBottomRef.current?.scrollIntoView({ behavior: "smooth" });
        }, 60);
      }
    }
  }, [chatMessages.length, activeTab]);

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
        priority: selectedPriority,
        note: assignNote.trim(),
      });
      toast.success(
        `Đã phân công nhân viên xử lý (Độ ưu tiên: ${TICKET_PRIORITY_LABELS[selectedPriority]})!`,
      );
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
      setHeadResolution(staffResolution.trim());
    } catch {
      toast.error("Lỗi khi gửi kết quả xử lý lên Trưởng phòng.");
    } finally {
      setStaffCompleting(false);
    }
  };

  const handleSendResultToCustomer = async (contentText: string) => {
    if (!contentText.trim()) {
      toast.error(
        "Vui lòng nhập nội dung kết quả xử lý để gửi cho khách hàng.",
      );
      return;
    }
    setResolving(true);
    try {
      const updated = await ticketApi.headResolve(ticketId, {
        resolution: contentText.trim(),
        closeTicket: true,
      });
      toast.success(
        "Đã gửi kết quả xử lý cho khách hàng và hoàn tất yêu cầu!",
      );
      setTicket(updated);
    } catch {
      toast.error("Lỗi khi gửi kết quả xử lý cho khách hàng.");
    } finally {
      setResolving(false);
    }
  };

  const handleSendReply = async (e?: React.FormEvent) => {
    e?.preventDefault();
    if (!ticket || !replyMessage.trim() || !isDepartmentHead) return;

    setSendingReply(true);
    try {
      const newMsg = await ticketApi.replyMessage(ticket.ticketSupportId, {
        messageContent: replyMessage.trim(),
      });
      setTicket((prev) => {
        if (!prev) return null;
        return {
          ...prev,
          messages: [...(prev.messages || []), newMsg],
        };
      });
      setReplyMessage("");
      setTimeout(() => {
        chatBottomRef.current?.scrollIntoView({ behavior: "smooth" });
      }, 50);
    } catch {
      toast.error("Chỉ Trưởng phòng mới có quyền nhắn tin với khách hàng.");
    } finally {
      setSendingReply(false);
    }
  };

  const serviceLabel =
    ticket?.serviceName || ticket?.module || "Dịch vụ chung";

  return (
    <>

      <div className="grow overflow-y-auto bg-slate-50/50 p-4 lg:p-8 dark:bg-background">
        <div className="mx-auto max-w-6xl space-y-6">
          {loading && !ticket ? (
            <Card className="border-border p-12 text-center text-sm text-muted-foreground shadow-xs">
              <Loader2 className="mx-auto mb-2 size-6 animate-spin text-primary" />
              Đang tải dữ liệu yêu cầu hỗ trợ...
            </Card>
          ) : !ticket ? (
            <Card className="space-y-3 border-border p-12 text-center shadow-xs">
              <p className="text-sm text-muted-foreground">
                Không tìm thấy yêu cầu hỗ trợ này hoặc yêu cầu không thuộc phạm
                vi phụ trách của bạn.
              </p>
              <Button
                variant="outline"
                size="sm"
                onClick={() => router.push("/workspace/support-tickets")}
              >
                Quay lại danh sách
              </Button>
            </Card>
          ) : (
            <>
              {/* Thanh tiêu đề & Điều hướng tách trang con (Tabs) */}
              <div className="flex flex-col gap-4 rounded-xl border bg-card p-5 shadow-xs lg:flex-row lg:items-center lg:justify-between">
                <div className="space-y-1.5">
                  <button
                    type="button"
                    onClick={() => router.push("/workspace/support-tickets")}
                    className="inline-flex items-center gap-1.5 text-xs font-medium text-muted-foreground hover:text-foreground"
                  >
                    <ArrowLeft className="size-3.5" />
                    Quay lại danh sách yêu cầu
                  </button>

                  <div className="flex flex-wrap items-center gap-2.5">
                    <span className="font-mono text-xl font-bold text-primary">
                      #{ticket.ticketCode}
                    </span>
                    <TicketStatusBadge status={ticket.ticketStatus} />
                    <TicketPriorityBadge priority={ticket.ticketPriority} />
                    <Badge variant="secondary" className="font-medium">
                      {ticket.department}
                    </Badge>
                    <Badge
                      variant="outline"
                      className="border-teal-200 bg-teal-50 text-teal-700 dark:border-teal-800 dark:bg-teal-950/50 dark:text-teal-300"
                    >
                      Dịch vụ: {serviceLabel}
                    </Badge>
                  </div>

                  <h1 className="text-lg font-bold text-foreground">
                    {ticket.ticketSubject}
                  </h1>
                </div>

                {/* Thanh chuyển trang tách biệt (Không dồn tất cả vào 1 màn) */}
                <div className="inline-flex flex-wrap items-center gap-1 rounded-xl border bg-slate-100 p-1.5 dark:bg-slate-800">
                  <button
                    type="button"
                    onClick={() => setActiveTab("workflow")}
                    className={cn(
                      "inline-flex items-center gap-2 rounded-lg px-4 py-2 text-xs font-semibold transition-all",
                      activeTab === "workflow"
                        ? "bg-white text-primary shadow-xs dark:bg-slate-900"
                        : "text-slate-600 hover:text-slate-900 dark:text-slate-300",
                    )}
                  >
                    <ClipboardCheck className="size-4" />
                    <span>
                      {isDepartmentHead
                        ? "1. Xử lý & Phân công"
                        : "1. Nội dung & Báo cáo kết quả"}
                    </span>
                  </button>

                  {/* CHỈ TRƯỞNG PHÒNG MỚI ĐƯỢC NHẮN TIN VỚI KHÁCH HÀNG (Nhân viên được phân công không có tab nhắn tin) */}
                  {isDepartmentHead && (
                    <button
                      type="button"
                      onClick={() => setActiveTab("chat")}
                      className={cn(
                        "inline-flex items-center gap-2 rounded-lg px-4 py-2 text-xs font-semibold transition-all",
                        activeTab === "chat"
                          ? "bg-white text-blue-600 shadow-xs dark:bg-slate-900 dark:text-blue-400"
                          : "text-slate-600 hover:text-slate-900 dark:text-slate-300",
                      )}
                    >
                      <MessageCircle className="size-4" />
                      <span>2. Nhắn tin với Khách hàng</span>
                      <span className="rounded-full bg-blue-100 px-1.5 py-0.5 text-[10px] font-bold text-blue-700 dark:bg-blue-950 dark:text-blue-300">
                        {chatMessages.length}
                      </span>
                    </button>
                  )}

                  <button
                    type="button"
                    onClick={() => setActiveTab("history")}
                    className={cn(
                      "inline-flex items-center gap-2 rounded-lg px-4 py-2 text-xs font-semibold transition-all",
                      activeTab === "history"
                        ? "bg-white text-primary shadow-xs dark:bg-slate-900"
                        : "text-slate-600 hover:text-slate-900 dark:text-slate-300",
                    )}
                  >
                    <History className="size-4" />
                    <span>
                      {isDepartmentHead
                        ? "3. Thông tin & Lịch sử điều phối"
                        : "2. Thông tin phiếu hỗ trợ"}
                    </span>
                  </button>
                </div>
              </div>

              {/* =========================================================
                  TRANG 1: NỘI DUNG YÊU CẦU & XỬ LÝ NGHIỆP VỤ
                 ========================================================= */}
              {activeTab === "workflow" && (
                <div className="space-y-6">
                  {/* Card Nội dung câu hỏi từ Khách hàng */}
                  <Card className="border-border shadow-xs">
                    <CardHeader className="border-b pb-3">
                      <div className="flex flex-wrap items-center justify-between gap-2">
                        <div className="flex items-center gap-2.5">
                          <div className="flex size-9 items-center justify-center rounded-lg bg-primary/10 text-primary">
                            <FileText className="size-5" />
                          </div>
                          <div>
                            <CardTitle className="text-base font-bold">
                              Nội dung yêu cầu hỗ trợ từ Khách hàng
                            </CardTitle>
                            <CardDescription className="text-xs">
                              Khách hàng:{" "}
                              <span className="font-semibold text-foreground">
                                {ticket.createdByName ||
                                  ticket.tenantName ||
                                  "Khách hàng"}
                              </span>{" "}
                              • Dịch vụ:{" "}
                              <span className="font-semibold text-foreground">
                                {serviceLabel}
                              </span>{" "}
                              • Gửi lúc: {formatZaloTime(ticket.createdAt)}
                            </CardDescription>
                          </div>
                        </div>
                      </div>
                    </CardHeader>
                    <CardContent className="pt-4">
                      <div className="rounded-xl border bg-slate-50/80 p-4 text-sm leading-relaxed whitespace-pre-wrap text-foreground dark:bg-slate-900/50">
                        {ticket.ticketDescription}
                      </div>
                    </CardContent>
                  </Card>

                  {/* NẾU LÀ TRƯỞNG PHÒNG: Chia 2 cột gọn gàng: (1) Phân công nhân viên & (2) Duyệt gửi kết quả */}
                  {isDepartmentHead && (
                    <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
                      {/* Bước 1: Phân công nhân viên trong phòng */}
                      <Card className="border-blue-500/30 bg-blue-500/5 shadow-xs">
                        <CardHeader className="border-b border-blue-200/50 pb-3 dark:border-blue-900/50">
                          <div className="flex items-center gap-2.5">
                            <div className="flex size-9 items-center justify-center rounded-lg bg-blue-600/10 text-blue-600 dark:text-blue-400">
                              <UserPlus className="size-5" />
                            </div>
                            <div>
                              <CardTitle className="text-base font-bold text-blue-950 dark:text-blue-200">
                                Bước 1: Phân công nhân viên xử lý
                              </CardTitle>
                              <CardDescription className="text-xs">
                                Giao yêu cầu cho nhân viên thuộc{" "}
                                {ticket.department}
                              </CardDescription>
                            </div>
                          </div>
                        </CardHeader>
                        <CardContent className="space-y-4 pt-4 text-sm">
                          <div className="space-y-2">
                            <Label className="text-xs font-semibold">
                              Nhân viên phụ trách trong phòng ban:
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
                              <SelectTrigger className="h-10 w-full bg-background text-sm">
                                <SelectValue
                                  placeholder={
                                    loadingStaff
                                      ? "Đang tải danh sách nhân viên..."
                                      : "Chọn nhân viên phụ trách"
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
                                      : ""}
                                  </SelectItem>
                                ))}
                              </SelectContent>
                            </Select>
                          </div>

                          <div className="space-y-2">
                            <Label className="text-xs font-semibold">
                              Độ ưu tiên xử lý (gán cho nhân viên):
                            </Label>
                            <Select
                              value={selectedPriority}
                              onValueChange={(val) =>
                                setSelectedPriority(val as TicketPriority)
                              }
                            >
                              <SelectTrigger className="h-10 w-full bg-background text-sm font-medium">
                                <SelectValue placeholder="Chọn độ ưu tiên" />
                              </SelectTrigger>
                              <SelectContent>
                                <SelectItem value={TicketPriority.Urgent}>
                                  🔥 Khẩn cấp — Xử lý ngay lập tức
                                </SelectItem>
                                <SelectItem value={TicketPriority.High}>
                                  ⚡ Cao — Ưu tiên xử lý trước
                                </SelectItem>
                                <SelectItem value={TicketPriority.Medium}>
                                  📌 Bình thường — Xử lý theo thứ tự
                                </SelectItem>
                                <SelectItem value={TicketPriority.Low}>
                                  🕒 Thấp — Xử lý khi hoàn thành việc gấp
                                </SelectItem>
                              </SelectContent>
                            </Select>
                          </div>

                          <div className="space-y-2">
                            <Label className="text-xs font-semibold">
                              Ghi chú chỉ đạo cho nhân viên:
                            </Label>
                            <Textarea
                              value={assignNote}
                              onChange={(e) => setAssignNote(e.target.value)}
                              placeholder="Nhập hướng dẫn hoặc yêu cầu xử lý cho nhân viên..."
                              className="min-h-[110px] resize-y bg-background text-sm"
                              maxLength={500}
                            />
                          </div>

                          {ticket.assignedEmployeeName && (
                            <div className="flex items-center justify-between rounded-lg border border-blue-200 bg-background/80 px-3 py-2 text-xs">
                              <span className="text-muted-foreground">
                                Nhân viên đang phụ trách:
                              </span>
                              <span className="font-bold text-blue-700 dark:text-blue-400">
                                {ticket.assignedEmployeeName}
                              </span>
                            </div>
                          )}

                          <div className="flex justify-end">
                            <Button
                              type="button"
                              onClick={() => void handleAssignToStaff()}
                              disabled={assigning || !selectedStaffId}
                              className="bg-blue-600 text-white hover:bg-blue-700 gap-2"
                            >
                              {assigning ? (
                                <Loader2 className="size-4 animate-spin" />
                              ) : (
                                <UserPlus className="size-4" />
                              )}
                              <span>Xác nhận phân công</span>
                            </Button>
                          </div>
                        </CardContent>
                      </Card>

                      {/* Bước 2: Duyệt báo cáo của nhân viên & Phản hồi kết quả cho khách hàng */}
                      <Card className="border-emerald-500/30 bg-emerald-500/5 shadow-xs">
                        <CardHeader className="border-b border-emerald-200/50 pb-3 dark:border-emerald-900/50">
                          <div className="flex items-center gap-2.5">
                            <div className="flex size-9 items-center justify-center rounded-lg bg-emerald-600/10 text-emerald-600 dark:text-emerald-400">
                              <CheckCircle2 className="size-5" />
                            </div>
                            <div>
                              <CardTitle className="text-base font-bold text-emerald-950 dark:text-emerald-200">
                                Bước 2: Duyệt & Gửi kết quả cho Khách hàng
                              </CardTitle>
                              <CardDescription className="text-xs">
                                Kiểm tra báo cáo của nhân viên và đóng phiếu hỗ
                                trợ
                              </CardDescription>
                            </div>
                          </div>
                        </CardHeader>
                        <CardContent className="space-y-4 pt-4 text-sm">
                          {ticket.ticketResolution && (
                            <div className="rounded-lg border border-purple-200 bg-purple-50/80 p-3 text-xs text-purple-950 dark:border-purple-800 dark:bg-purple-950/40 dark:text-purple-200">
                              <p className="font-bold">
                                Báo cáo kết quả từ nhân viên{" "}
                                {ticket.assignedEmployeeName || ""}:
                              </p>
                              <p className="mt-1 whitespace-pre-wrap">
                                {ticket.ticketResolution}
                              </p>
                            </div>
                          )}

                          <div className="space-y-2">
                            <Label className="text-xs font-semibold">
                              Nội dung kết quả chính thức gửi tới Khách hàng:
                            </Label>
                            <Textarea
                              value={headResolution}
                              onChange={(e) =>
                                setHeadResolution(e.target.value)
                              }
                              placeholder="Nhập phương án / kết quả giải quyết chính thức để thông báo cho khách hàng..."
                              className="min-h-[130px] resize-y bg-background text-sm"
                              maxLength={2000}
                            />
                          </div>

                          <div className="flex justify-end">
                            <Button
                              type="button"
                              onClick={() =>
                                void handleSendResultToCustomer(headResolution)
                              }
                              disabled={resolving || !headResolution.trim()}
                              className="bg-emerald-600 text-white hover:bg-emerald-700 gap-2"
                            >
                              {resolving ? (
                                <Loader2 className="size-4 animate-spin" />
                              ) : (
                                <Send className="size-4" />
                              )}
                              <span>Gửi kết quả & Đóng Ticket</span>
                            </Button>
                          </div>
                        </CardContent>
                      </Card>
                    </div>
                  )}

                  {/* NẾU LÀ NHÂN VIÊN ĐƯỢC PHÂN CÔNG (STAFF): Chỉ hiển thị chỉ đạo của Trưởng phòng & Form Báo cáo kết quả (KHÔNG CÓ CHAT) */}
                  {isStaff && (
                    <Card className="border-purple-500/30 bg-purple-500/5 shadow-xs">
                      <CardHeader className="border-b border-purple-200/50 pb-3 dark:border-purple-900/50">
                        <div className="flex items-center gap-2.5">
                          <div className="flex size-9 items-center justify-center rounded-lg bg-purple-600/10 text-purple-600 dark:text-purple-400">
                            <ClipboardCheck className="size-5" />
                          </div>
                          <div>
                            <CardTitle className="text-base font-bold text-purple-950 dark:text-purple-200">
                              Báo cáo kết quả xử lý yêu cầu
                            </CardTitle>
                            <CardDescription className="text-xs">
                              Sau khi xử lý xong sự cố, nhập chi tiết kết quả
                              bên dưới để gửi báo cáo lên Trưởng phòng{" "}
                              {ticket.departmentHeadName || ""}
                            </CardDescription>
                          </div>
                        </div>
                      </CardHeader>
                      <CardContent className="space-y-4 pt-4 text-sm">
                        {/* Hiển thị chỉ đạo của Trưởng phòng nếu có */}
                        {internalReports.length > 0 && (
                          <div className="space-y-2 rounded-lg border bg-background p-3.5">
                            <p className="text-xs font-bold text-muted-foreground">
                              Nội dung điều phối & Báo cáo nội bộ:
                            </p>
                            {internalReports.map((rep) => (
                              <div
                                key={rep.ticketMessageId}
                                className="rounded border-l-2 border-purple-500 bg-slate-50 px-3 py-2 text-xs dark:bg-slate-900"
                              >
                                <p className="whitespace-pre-line text-foreground">
                                  {rep.ticketMessageContent}
                                </p>
                                <p className="mt-1 text-[11px] text-muted-foreground">
                                  {formatZaloTime(rep.createdAt)}
                                </p>
                              </div>
                            ))}
                          </div>
                        )}

                        <div className="space-y-2">
                          <Label className="text-sm font-semibold">
                            Kết quả xử lý công việc được giao:
                          </Label>
                          <Textarea
                            value={staffResolution}
                            onChange={(e) => setStaffResolution(e.target.value)}
                            placeholder="Mô tả chi tiết nguyên nhân và kết quả đã xử lý xong..."
                            className="min-h-[160px] resize-y bg-background text-sm leading-relaxed"
                            maxLength={2000}
                          />
                        </div>

                        <div className="flex flex-wrap items-center justify-end gap-3">
                          <Button
                            type="button"
                            onClick={() => void handleStaffSendToHead()}
                            disabled={
                              staffCompleting || !staffResolution.trim()
                            }
                            className="bg-purple-600 text-white hover:bg-purple-700 gap-2"
                          >
                            {staffCompleting ? (
                              <Loader2 className="size-4 animate-spin" />
                            ) : (
                              <Send className="size-4" />
                            )}
                            <span>Gửi báo cáo lên Trưởng phòng</span>
                          </Button>
                        </div>
                      </CardContent>
                    </Card>
                  )}
                </div>
              )}

              {/* =========================================================
                  TRANG 2: NHẮN TIN TRỰC TUYẾN VỚI KHÁCH HÀNG (KIỂU ZALO REALTIME)
                  (Chỉ dành cho Trưởng phòng - Nhân viên không có quyền nhắn tin)
                 ========================================================= */}
              {activeTab === "chat" && isDepartmentHead && (
                <div className="flex h-[640px] flex-col overflow-hidden rounded-xl border border-slate-200 bg-card shadow-sm dark:border-slate-800">
                  {/* Header Zalo Chat */}
                  <div className="flex items-center justify-between border-b bg-white px-5 py-3.5 dark:bg-slate-900">
                    <div className="flex items-center gap-3">
                      <div className="relative flex size-10 items-center justify-center rounded-full bg-teal-600 font-bold text-white">
                        KH
                        <span className="absolute right-0 bottom-0 size-2.5 rounded-full bg-emerald-500 ring-2 ring-white" />
                      </div>
                      <div>
                        <p className="text-sm font-bold text-foreground">
                          {ticket.createdByName ||
                            ticket.tenantName ||
                            "Khách hàng"}
                        </p>
                        <p className="flex items-center gap-1.5 text-xs text-emerald-600 dark:text-emerald-400">
                          <span className="size-1.5 rounded-full bg-emerald-500" />
                          Kênh trao đổi trực tiếp Trưởng phòng ↔ Khách hàng (Tự
                          động làm mới tức thì)
                        </p>
                      </div>
                    </div>

                    <div className="hidden text-right text-xs text-muted-foreground sm:block">
                      <p className="font-semibold text-foreground">
                        {serviceLabel}
                      </p>
                      <p>Mã phiếu: #{ticket.ticketCode}</p>
                    </div>
                  </div>

                  {/* Khung hiển thị tin nhắn kiểu Zalo */}
                  <div className="flex-1 space-y-4 overflow-y-auto bg-[#eef0f5] p-5 dark:bg-slate-950">
                    {chatMessages.map((msg) => {
                      const isMine = msg.isFromEmployee;
                      return (
                        <div
                          key={msg.ticketMessageId}
                          className={cn(
                            "flex items-end gap-2.5",
                            isMine ? "justify-end" : "justify-start",
                          )}
                        >
                          {!isMine && (
                            <div className="flex size-8 shrink-0 items-center justify-center rounded-full bg-teal-600 text-xs font-bold text-white shadow-xs">
                              KH
                            </div>
                          )}

                          <div
                            className={cn(
                              "max-w-[75%] rounded-2xl px-4 py-2.5 shadow-xs",
                              isMine
                                ? "rounded-br-xs border border-blue-200/80 bg-[#e5efff] text-slate-900 dark:border-blue-500 dark:bg-blue-600 dark:text-white"
                                : "rounded-bl-xs border border-slate-200/80 bg-white text-slate-900 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100",
                            )}
                          >
                            <p
                              className={cn(
                                "mb-1 text-[11px] font-bold",
                                isMine
                                  ? "text-blue-700 dark:text-blue-100"
                                  : "text-teal-600 dark:text-teal-400",
                              )}
                            >
                              {msg.senderName}
                            </p>

                            <p className="whitespace-pre-line text-sm leading-relaxed">
                              {msg.ticketMessageContent}
                            </p>

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

                  {/* Phản hồi mẫu nhanh & Ô nhập tin nhắn kiểu Zalo */}
                  <form
                    onSubmit={handleSendReply}
                    className="space-y-2.5 border-t bg-white p-3.5 dark:bg-slate-900"
                  >
                    <div className="flex flex-wrap items-center gap-1.5">
                      {QUICK_RESPONSES.map((sample, idx) => (
                        <button
                          key={idx}
                          type="button"
                          onClick={() => setReplyMessage(sample)}
                          className="rounded-full border border-slate-200 bg-slate-50 px-3 py-1 text-[11px] text-slate-600 transition-colors hover:border-blue-300 hover:bg-blue-50 hover:text-blue-700 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-300"
                        >
                          Mẫu {idx + 1}: {sample.slice(0, 42)}...
                        </button>
                      ))}
                    </div>

                    <div className="flex items-center gap-2">
                      <input
                        type="text"
                        value={replyMessage}
                        onChange={(e) => setReplyMessage(e.target.value)}
                        placeholder="Nhập tin nhắn gửi trực tiếp cho khách hàng... (Nhấn Enter để gửi ngay)"
                        className="h-10 flex-1 rounded-full border border-slate-300 bg-slate-50 px-4 text-sm focus:border-blue-500 focus:bg-white focus:outline-none dark:border-slate-700 dark:bg-slate-800"
                      />
                      <Button
                        type="submit"
                        disabled={sendingReply || !replyMessage.trim()}
                        className="h-10 rounded-full bg-blue-600 px-5 text-white hover:bg-blue-700"
                      >
                        {sendingReply ? (
                          <Loader2 className="size-4 animate-spin" />
                        ) : (
                          <Send className="size-4" />
                        )}
                        <span className="ml-1.5">Gửi tin nhắn</span>
                      </Button>
                    </div>
                  </form>
                </div>
              )}

              {/* =========================================================
                  TRANG 3: THÔNG TIN KHÁCH HÀNG & LỊCH SỬ ĐIỀU PHỐI PHÒNG BAN
                 ========================================================= */}
              {activeTab === "history" && (
                <div className="grid grid-cols-1 gap-6 md:grid-cols-2">
                  {/* Card 1: Thông tin Khách hàng & Dịch vụ */}
                  <Card className="border-border shadow-xs">
                    <CardHeader className="border-b pb-3">
                      <div className="flex items-center gap-2">
                        <Building2 className="size-4 text-primary" />
                        <CardTitle className="text-base font-bold">
                          Thông tin Khách hàng & Dịch vụ
                        </CardTitle>
                      </div>
                    </CardHeader>
                    <CardContent className="space-y-4 pt-4 text-sm">
                      <div className="flex items-center justify-between border-b pb-2.5">
                        <span className="text-muted-foreground">
                          Người gửi yêu cầu:
                        </span>
                        <span className="font-semibold text-foreground">
                          {ticket.createdByName ||
                            ticket.tenantName ||
                            "Khách hàng"}
                        </span>
                      </div>

                      <div className="flex items-center justify-between border-b pb-2.5">
                        <span className="text-muted-foreground">
                          Dịch vụ đăng ký:
                        </span>
                        <span className="font-bold text-teal-600 dark:text-teal-400">
                          {serviceLabel}
                        </span>
                      </div>

                      <div className="flex items-center justify-between border-b pb-2.5">
                        <span className="text-muted-foreground">
                          Phòng ban tiếp nhận:
                        </span>
                        <span className="font-semibold text-foreground">
                          {ticket.department}
                        </span>
                      </div>

                      <div className="flex items-center justify-between border-b pb-2.5">
                        <span className="text-muted-foreground">
                          Mã phiếu hỗ trợ:
                        </span>
                        <div className="flex items-center gap-1.5">
                          <span className="font-mono font-semibold">
                            {ticket.ticketCode}
                          </span>
                          <button
                            type="button"
                            onClick={() =>
                              handleCopy(ticket.ticketCode, "mã ticket")
                            }
                            className="rounded p-1 text-muted-foreground hover:bg-muted hover:text-foreground"
                          >
                            <Copy className="size-3.5" />
                          </button>
                        </div>
                      </div>

                      <div className="flex items-center justify-between">
                        <span className="text-muted-foreground">
                          Thời điểm khởi tạo:
                        </span>
                        <span className="font-medium text-foreground">
                          {formatZaloTime(ticket.createdAt)}
                        </span>
                      </div>
                    </CardContent>
                  </Card>

                  {/* Card 2: Nhân sự phụ trách & Lịch sử điều phối */}
                  <Card className="border-border shadow-xs">
                    <CardHeader className="border-b pb-3">
                      <div className="flex items-center gap-2">
                        <ShieldCheck className="size-4 text-primary" />
                        <CardTitle className="text-base font-bold">
                          Nhân sự phụ trách & Lịch sử phân công
                        </CardTitle>
                      </div>
                    </CardHeader>
                    <CardContent className="space-y-3 pt-4 text-sm">
                      <div className="flex items-center justify-between rounded-lg border bg-muted/20 p-3">
                        <div className="flex items-center gap-2 text-muted-foreground">
                          <UserCheck className="size-4 text-blue-600" />
                          <span>Trưởng phòng điều phối:</span>
                        </div>
                        <span className="font-semibold text-foreground">
                          {ticket.departmentHeadName || "Trưởng bộ phận"}
                        </span>
                      </div>

                      <div className="flex items-center justify-between rounded-lg border bg-muted/20 p-3">
                        <div className="flex items-center gap-2 text-muted-foreground">
                          <User className="size-4 text-emerald-600" />
                          <span>Nhân viên xử lý:</span>
                        </div>
                        <span className="font-semibold text-foreground">
                          {ticket.assignedEmployeeName || "Chưa phân công"}
                        </span>
                      </div>

                      {ticket.assignments && ticket.assignments.length > 0 && (
                        <div className="space-y-2 pt-2">
                          <p className="text-xs font-bold uppercase tracking-wider text-muted-foreground">
                            Nhật ký điều phối trong phòng ban
                          </p>
                          <div className="space-y-2">
                            {ticket.assignments.map((a) => (
                              <div
                                key={a.ticketSupportEmployeeId}
                                className="flex items-center justify-between rounded-lg border bg-background px-3 py-2.5 text-xs"
                              >
                                <div>
                                  <p className="font-semibold text-foreground">
                                    {a.employeeName}
                                  </p>
                                  <p className="text-[11px] text-muted-foreground">
                                    {formatZaloTime(a.assignedAt)}
                                  </p>
                                </div>
                                <Badge variant="outline" className="text-[10px]">
                                  {a.assignmentRole === "DEPARTMENT_HEAD"
                                    ? "Trưởng phòng"
                                    : "Nhân viên xử lý"}
                                </Badge>
                              </div>
                            ))}
                          </div>
                        </div>
                      )}
                    </CardContent>
                  </Card>
                </div>
              )}
            </>
          )}
        </div>
      </div>
    </>
  );
}

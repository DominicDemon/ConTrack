"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { onNotificationRefresh } from "../../../../../Shared/notification-navigation";
import { useParams, useRouter, useSearchParams } from "next/navigation";
import {
  AlertTriangle,
  ArrowLeft,
  ArrowRightLeft,
  Building2,
  CheckCircle2,
  ClipboardCheck,
  Clock,
  Copy,
  FileText,
  Headset,
  History,
  Loader2,
  Lock,
  Send,
  User,
  UserCheck,
  UserPlus,
  Users,
  X,
  ZoomIn,
  ZoomOut,
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
import StaffPicker from "@/components/internal/staff-picker";
import { cn } from "@/lib/utils";
import {
  DepartmentStaffDto,
  ticketApi,
  TicketDto,
  TicketMessageDto,
  TicketPriority,
  TicketStatus,
  TICKET_PRIORITY_LABELS,
} from "@/services/internal-ticket-api";

const QUICK_RESOLUTION_TEMPLATES = [
  "Phòng ban chuyên môn đã trực tiếp kiểm tra và xử lý hoàn tất yêu cầu của Quý khách trên hệ thống. Quý khách vui lòng đăng nhập và kiểm tra lại dịch vụ.",
  "Sự cố kỹ thuật liên quan đến dịch vụ của Quý khách đã được khắc phục triệt để. Hệ thống hiện hoạt động ổn định bình thường.",
  "Yêu cầu điều chỉnh/hỗ trợ thông tin hợp đồng & dịch vụ của Quý khách đã được thực hiện thành công theo đúng đề nghị.",
];

function isRenderableImageUrl(url?: string | null): boolean {
  if (!url) return false;
  const trimmed = url.trim();
  return (
    trimmed.startsWith("data:image/") ||
    trimmed.startsWith("http://") ||
    trimmed.startsWith("https://") ||
    trimmed.startsWith("blob:")
  );
}

function getTicketImages(ticket: TicketDto | null): string[] {
  if (!ticket) return [];
  const raw =
    ticket.attachmentUrls && ticket.attachmentUrls.length > 0
      ? ticket.attachmentUrls
      : ticket.attachmentUrl
        ? [ticket.attachmentUrl]
        : [];
  return Array.from(new Set(raw.filter(isRenderableImageUrl)));
}

function getMessageImages(msg: TicketMessageDto): string[] {
  const raw =
    msg.attachmentUrls && msg.attachmentUrls.length > 0
      ? msg.attachmentUrls
      : msg.ticketMessageAttachmentUrl
        ? [msg.ticketMessageAttachmentUrl]
        : [];
  return Array.from(new Set(raw.filter(isRenderableImageUrl)));
}

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
      label: "Đang xử lý",
      className:
        "bg-cyan-50 text-cyan-700 border-cyan-200 dark:bg-cyan-500/10 dark:text-cyan-400 dark:border-cyan-500/20",
    },
    [TicketStatus.PendingHeadApproval]: {
      label: "Chờ Trưởng phòng duyệt kết quả",
      className:
        "bg-purple-50 text-purple-700 border-purple-200 dark:bg-purple-500/10 dark:text-purple-400 dark:border-purple-500/20",
    },
    [TicketStatus.Resolved]: {
      label: "Đã hoàn thành (Đã đóng)",
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

type AdminTabType = "assign" | "resolve" | "history";

export default function AdminSupportTicketDetailPage() {
  const params = useParams<{ id: string }>();
  const searchParams = useSearchParams();
  const router = useRouter();
  const ticketId = Number(params?.id);
  const user = useTicketUser();

  const [ticket, setTicket] = useState<TicketDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [activeTab, setActiveTab] = useState<AdminTabType>("assign");

  const [lightboxImage, setLightboxImage] = useState<{
    url: string;
    title?: string;
  } | null>(null);
  const [lightboxZoom, setLightboxZoom] = useState(1);

  const [departmentStaff, setDepartmentStaff] = useState<DepartmentStaffDto[]>(
    [],
  );
  const [loadingStaff, setLoadingStaff] = useState(false);
  const [selectedStaffIds, setSelectedStaffIds] = useState<number[]>([]);
  const [selectedPriority, setSelectedPriority] = useState<TicketPriority>(
    TicketPriority.Medium,
  );
  const [assignNote, setAssignNote] = useState("");
  const [assigning, setAssigning] = useState(false);

  const [staffResolution, setStaffResolution] = useState("");
  const [staffReportType, setStaffReportType] = useState<
    "COMPLETED" | "UNRESOLVED"
  >("COMPLETED");
  const [staffCompleting, setStaffCompleting] = useState(false);

  const [headResolution, setHeadResolution] = useState("");
  const [resolving, setResolving] = useState(false);

  const initializedResolutionRef = useRef<boolean>(false);
  const initializedTabRef = useRef<boolean>(false);

  const canAccess = Boolean(user?.canAccess);
  const isSysAdmin =
    user?.supportRole === "SYSADMIN" ||
    user?.username?.toLowerCase() === "sysadmin" ||
    user?.username?.toLowerCase() === "admin";

  useEffect(() => {
    if (!canAccess || isSysAdmin) {
      router.replace("/workspace");
    }
  }, [canAccess, isSysAdmin, router]);

  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === "Escape") {
        setLightboxImage(null);
        setLightboxZoom(1);
      }
    };
    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, []);

  // Listen to sidebar dropdown selection event
  useEffect(() => {
    const onSelectTab = (ev: Event) => {
      const detail = (ev as CustomEvent<{ tab?: AdminTabType }>).detail;
      if (detail?.tab) {
        setActiveTab(detail.tab);
      }
    };
    window.addEventListener("support-ticket-select-tab", onSelectTab);
    return () =>
      window.removeEventListener("support-ticket-select-tab", onSelectTab);
  }, []);

  // Sync URL ?tab=... parameter
  useEffect(() => {
    const tabParam = searchParams?.get("tab") as AdminTabType | null;
    if (
      tabParam &&
      ["assign", "resolve", "history"].includes(tabParam)
    ) {
      setActiveTab(tabParam);
      initializedTabRef.current = true;
    }
  }, [searchParams]);

  // Notify sidebar when activeTab changes
  useEffect(() => {
    if (typeof window !== "undefined") {
      window.sessionStorage.setItem("contrack_support_ticket_tab", activeTab);
      window.dispatchEvent(
        new CustomEvent("support-ticket-tab-changed", {
          detail: { tab: activeTab },
        }),
      );
    }
  }, [activeTab]);

  const loadStaffList = useCallback(async () => {
    if (!canAccess || !ticketId || isSysAdmin) return;
    setLoadingStaff(true);
    try {
      const staff = await ticketApi.getDepartmentStaff(ticketId);
      setDepartmentStaff(staff);
    } catch {
      toast.error("Không thể tải danh sách nhân sự trong phòng ban");
    } finally {
      setLoadingStaff(false);
    }
  }, [ticketId, isSysAdmin, canAccess]);

  const loadTicket = useCallback(
    async (silent = false) => {
      if (!canAccess || !Number.isFinite(ticketId) || ticketId <= 0 || isSysAdmin) return;
      if (!silent) setLoading(true);
      try {
        const data = await ticketApi.getById(ticketId);
        if (data) {
          setTicket(data);
          if (!silent) {
            if (
              data.assignedEmployeeIds &&
              data.assignedEmployeeIds.length > 0
            ) {
              setSelectedStaffIds(data.assignedEmployeeIds);
            } else if (data.assignedEmployeeId) {
              setSelectedStaffIds([data.assignedEmployeeId]);
            }
            if (data.ticketPriority) {
              setSelectedPriority(data.ticketPriority);
            }
          }
          if (!initializedResolutionRef.current && data.ticketResolution) {
            initializedResolutionRef.current = true;
            const resolution = data.ticketResolution;
            setHeadResolution(current => current || resolution);
            setStaffResolution(current => current || resolution);
          }
          if (!initializedTabRef.current) {
            initializedTabRef.current = true;
            const role = data.viewerRole || user?.supportRole || "STAFF";
            const savedTab =
              typeof window !== "undefined"
                ? (window.sessionStorage.getItem(
                  "contrack_support_ticket_tab",
                ) as AdminTabType | null)
                : null;
            if (role !== "DEPARTMENT_HEAD") {
              setActiveTab(savedTab === "history" ? "history" : "resolve");
            } else if (data.ticketStatus === TicketStatus.Resolved) {
              setActiveTab("history");
            } else if (
              data.ticketStatus === TicketStatus.PendingHeadApproval
            ) {
              setActiveTab("resolve");
            } else if (
              savedTab &&
              ["assign", "resolve", "history"].includes(savedTab)
            ) {
              setActiveTab(savedTab);
            } else {
              setActiveTab("assign");
            }
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
    [ticketId, isSysAdmin, canAccess, user?.supportRole],
  );

  useEffect(() => {
    void loadTicket(false);
  }, [loadTicket]);

  useEffect(() => onNotificationRefresh(() => void loadTicket(true)), [loadTicket]);

  useEffect(() => {
    const timer = setInterval(() => {
      void loadTicket(true);
    }, 2500);
    return () => clearInterval(timer);
  }, [loadTicket]);

  const effectiveRole = ticket?.viewerRole || user?.supportRole || "STAFF";
  const isDepartmentHead = effectiveRole === "DEPARTMENT_HEAD";
  const isStaff = !isDepartmentHead;
  const isTicketResolved = ticket?.ticketStatus === TicketStatus.Resolved;

  // Nhân viên chỉ vào các tab xử lý và lịch sử
  useEffect(() => {
    if (isStaff && activeTab === "assign") {
      setActiveTab("resolve");
    }
  }, [isStaff, activeTab]);

  useEffect(() => {
    if (ticket && isDepartmentHead) {
      void loadStaffList();
    }
  }, [ticket?.ticketSupportId, isDepartmentHead, loadStaffList]);

  const internalReports = (ticket?.messages ?? []).filter(
    (m) =>
      m.ticketMessageType === "STAFF_REPORT" ||
      m.ticketMessageType === "STAFF_COMPLETION" ||
      m.ticketMessageType === "ASSIGNMENT" ||
      m.ticketMessageType === "INTERNAL_NOTE",
  );

  const staffOnlyReports = (ticket?.messages ?? []).filter(
    (m) =>
      m.ticketMessageType === "STAFF_REPORT" ||
      m.ticketMessageType === "STAFF_COMPLETION",
  );


  const handleCopy = (text: string, label: string) => {
    navigator.clipboard.writeText(text);
    toast.success(`Đã sao chép ${label}: ${text}`);
  };

  const toggleStaffSelection = (empId: number) => {
    if (isTicketResolved) return;
    setSelectedStaffIds((prev) =>
      prev.includes(empId)
        ? prev.filter((id) => id !== empId)
        : [...prev, empId],
    );
  };

  const handleAssignToStaff = async () => {
    if (isTicketResolved) {
      toast.error("Phiếu hỗ trợ đã hoàn thành và đóng, không thể phân công.");
      return;
    }
    if (requiresTransferReason && !assignNote.trim()) {
      toast.error("Nhập lý do chuyển người xử lý.");
      return;
    }
    if (selectedStaffIds.length === 0) {
      toast.error("Vui lòng chọn ít nhất 1 nhân sự phụ trách xử lý.");
      return;
    }
    setAssigning(true);
    try {
      const updated = await ticketApi.assignToStaff(ticketId, {
        staffEmployeeId: selectedStaffIds[0],
        staffEmployeeIds: selectedStaffIds,
        priority: selectedPriority,
        note: assignNote.trim(),
      });
      const chosenMembers = departmentStaff.filter((s) =>
        selectedStaffIds.includes(s.employeeId),
      );
      const isOnlyManager =
        chosenMembers.length === 1 && chosenMembers[0]?.isManager;

      if (isOnlyManager) {
        toast.success(
          "Trưởng phòng đã trực tiếp tiếp nhận xử lý yêu cầu này!",
        );
        setTicket(updated);
        setAssignNote("");
        setActiveTab("resolve");
      } else {
        const names = chosenMembers.map((m) => m.employeeName).join(", ");
        toast.success(
          `Đã phân công xử lý cho: ${names || "nhân sự đã chọn"} (Độ ưu tiên: ${TICKET_PRIORITY_LABELS[selectedPriority]})!`,
        );
        setTicket(updated);
        setAssignNote("");
      }
    } catch (err) {
      toast.error(
        err instanceof Error
          ? err.message
          : "Không thể thực hiện phân công xử lý.",
      );
    } finally {
      setAssigning(false);
    }
  };

  const handleStaffSendToHead = async () => {
    if (isTicketResolved) {
      toast.error("Phiếu hỗ trợ đã hoàn thành và đóng.");
      return;
    }
    if (!staffResolution.trim()) {
      toast.error(
        staffReportType === "UNRESOLVED"
          ? "Vui lòng nhập lý do chưa xử lý được để Trưởng phòng nắm thông tin chuyển giao."
          : "Vui lòng nhập nội dung kết quả xử lý trước khi gửi.",
      );
      return;
    }
    setStaffCompleting(true);
    try {
      const updated = await ticketApi.staffComplete(ticketId, {
        resolution: staffResolution.trim(),
        reportType: staffReportType,
      });
      if (staffReportType === "UNRESOLVED") {
        toast.success(
          "Đã gửi báo cáo đề xuất chuyển người xử lý lên Trưởng phòng!",
        );
      } else {
        toast.success("Đã gửi báo cáo kết quả xử lý lên Trưởng phòng!");
      }
      setTicket(updated);
      setHeadResolution(staffResolution.trim());
    } catch (err) {
      toast.error(
        err instanceof Error
          ? err.message
          : "Lỗi khi gửi báo cáo lên Trưởng phòng.",
      );
    } finally {
      setStaffCompleting(false);
    }
  };

  const handleSendResultToCustomer = async (contentText: string) => {
    if (isTicketResolved) {
      toast.error("Phiếu hỗ trợ đã hoàn thành và đóng.");
      return;
    }
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
        "Đã gửi kết quả xử lý cho khách hàng và đóng phiếu hỗ trợ!",
      );
      setTicket(updated);
    } catch (err) {
      toast.error(
        err instanceof Error
          ? err.message
          : "Lỗi khi gửi kết quả xử lý cho khách hàng.",
      );
    } finally {
      setResolving(false);
    }
  };

  const serviceLabel = ticket?.serviceName || ticket?.module || "Dịch vụ chung";
  const initialImages = getTicketImages(ticket);

  const viewerEmpId = ticket?.viewerEmployeeId || 0;
  const activeAssigneeRecords = (ticket?.assignments ?? []).filter(
    (a) => a.assignmentRole === "ASSIGNEE" && !a.unassignedAt,
  );
  const requiresTransferReason = activeAssigneeRecords.some(a => !selectedStaffIds.includes(a.employeeId));
  const pastAssigneeRecords = (ticket?.assignments ?? []).filter(
    (a) => a.assignmentRole === "ASSIGNEE" && Boolean(a.unassignedAt),
  );
  const isCurrentlyAssignedStaff =
    !isStaff ||
    ticket?.assignedEmployeeId === viewerEmpId ||
    Boolean(ticket?.assignedEmployeeIds?.includes(viewerEmpId)) ||
    activeAssigneeRecords.some((a) => a.employeeId === viewerEmpId);

  const wasPreviouslyAssignedAndHandedOver =
    isStaff &&
    !isCurrentlyAssignedStaff &&
    pastAssigneeRecords.some((a) => a.employeeId === viewerEmpId);

  const renderCustomerRequestCard = () => {
    if (!ticket) return null;
    return (
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
                    {ticket.createdByName || ticket.tenantName || "Khách hàng"}
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
        <CardContent className="space-y-4 pt-4">
          <div className="rounded-xl border bg-slate-50/80 p-4 text-sm leading-relaxed whitespace-pre-wrap text-foreground dark:bg-slate-900/50">
            {ticket.ticketDescription}
          </div>

          {initialImages.length > 0 && (
            <div className="space-y-2">
              <p className="text-xs font-semibold text-muted-foreground">
                Hình ảnh khách hàng đính kèm ({initialImages.length} ảnh — nhấn
                vào ảnh để phóng to):
              </p>
              <div className="flex flex-wrap gap-3">
                {initialImages.map((imgUrl, idx) => (
                  <button
                    key={idx}
                    type="button"
                    onClick={() => {
                      setLightboxZoom(1);
                      setLightboxImage({
                        url: imgUrl,
                        title:
                          ticket.attachmentNames?.[idx] ||
                          `Hình ảnh đính kèm #${idx + 1}`,
                      });
                    }}
                    className="group relative overflow-hidden rounded-xl border border-slate-200 bg-slate-100 shadow-xs transition-all hover:ring-2 hover:ring-primary dark:border-slate-700 dark:bg-slate-800"
                  >
                    <img
                      src={imgUrl}
                      alt={`Ảnh đính kèm ${idx + 1}`}
                      className="h-32 w-44 object-cover transition-transform duration-200 group-hover:scale-105"
                    />
                    <div className="absolute inset-0 flex items-center justify-center bg-black/0 transition-colors group-hover:bg-black/30">
                      <span className="flex items-center gap-1 rounded-full bg-black/70 px-2.5 py-1 text-[11px] font-medium text-white opacity-0 transition-opacity group-hover:opacity-100">
                        <ZoomIn className="size-3.5" />
                        Xem ảnh lớn
                      </span>
                    </div>
                  </button>
                ))}
              </div>
            </div>
          )}
        </CardContent>
      </Card>
    );
  };

  if (!canAccess || isSysAdmin) {
    return (
      <div className="grow overflow-y-auto bg-slate-50/50 p-4 lg:p-8 dark:bg-background">
        <div className="mx-auto max-w-2xl py-16 text-center">
          <Headset className="mx-auto mb-3 size-12 text-muted-foreground/40" />
          <h2 className="text-lg font-semibold text-foreground mb-1">
            Không có quyền truy cập
          </h2>
          <p className="text-sm text-muted-foreground">
            Chức năng Hỗ trợ khách hàng chỉ dành cho nhân sự thuộc Phòng Kỹ Thuật & Hỗ Trợ, Phòng Kế Toán - Tài Chính và Phòng Hành Chính - Pháp Chế.
          </p>
        </div>
      </div>
    );
  }

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
              {/* Header phiếu hỗ trợ */}
              <div className="flex flex-col gap-4 rounded-xl border bg-card p-5 shadow-xs">
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
              </div>

              {/* Banner thông báo khi phiếu đã hoàn thành (Khóa toàn bộ thao tác) */}
              {isTicketResolved && (
                <div className="flex items-start gap-3 rounded-xl border border-emerald-300 bg-emerald-50/90 p-4 text-sm text-emerald-950 shadow-xs dark:border-emerald-800 dark:bg-emerald-950/40 dark:text-emerald-200">
                  <Lock className="mt-0.5 size-5 shrink-0 text-emerald-600 dark:text-emerald-400" />
                  <div className="space-y-1">
                    <p className="font-bold">
                      Phiếu hỗ trợ đã hoàn thành và đóng (Chỉ xem lịch sử)
                    </p>
                    <p className="text-xs leading-relaxed text-emerald-800 dark:text-emerald-300">
                      Yêu cầu hỗ trợ này đã chuyển sang trạng thái{" "}
                      <strong>Đã hoàn thành</strong> lúc{" "}
                      {formatZaloTime(
                        ticket.ticketResolvedAt || ticket.updatedAt,
                      )}
                      . Trưởng phòng, Nhân viên và Khách hàng chỉ có thể xem lại
                      lịch sử phân công, kết quả xử lý và lịch sử tin nhắn,
                      không thể gửi thêm tin nhắn hay cập nhật mới.
                    </p>
                  </div>
                </div>
              )}

              {/* ================= TAB 1: PHÂN CÔNG XỬ LÝ (CHỈ TRƯỞNG PHÒNG) ================= */}
              {activeTab === "assign" && isDepartmentHead && (
                <div className="space-y-6">
                  {/* Nếu nhân viên có gửi báo cáo (đặc biệt là báo cáo chưa xử lý được / xin chuyển giao), hiển thị ngay tại trang Phân công để Trưởng phòng nắm rõ và phân công người tiếp theo */}
                  {staffOnlyReports.length > 0 && (
                    <Card className="border-amber-300 bg-amber-50/60 shadow-xs dark:border-amber-800 dark:bg-amber-950/30">
                      <CardHeader className="border-b border-amber-200/70 pb-3 dark:border-amber-800/60">
                        <div className="flex items-center gap-2.5">
                          <div className="flex size-8 items-center justify-center rounded-lg bg-amber-500/15 text-amber-700 dark:text-amber-300">
                            <ArrowRightLeft className="size-4" />
                          </div>
                          <div>
                            <CardTitle className="text-sm font-bold text-amber-950 dark:text-amber-200">
                              Báo cáo tiến độ & Đề xuất từ nhân viên phụ trách
                            </CardTitle>
                            <CardDescription className="text-xs">
                              Xem báo cáo từ nhân sự đợt trước để điều phối tiếp
                              cho nhân sự khác hoặc trực tiếp xử lý
                            </CardDescription>
                          </div>
                        </div>
                      </CardHeader>
                      <CardContent className="space-y-2.5 pt-3">
                        {staffOnlyReports.map((rep) => {
                          const isUnresolved =
                            rep.ticketMessageType === "STAFF_REPORT";
                          return (
                            <div
                              key={rep.ticketMessageId}
                              className={cn(
                                "rounded-lg border p-3 text-xs",
                                isUnresolved
                                  ? "border-rose-300 bg-rose-50/90 text-rose-950 dark:border-rose-800 dark:bg-rose-950/40 dark:text-rose-200"
                                  : "border-purple-200 bg-white text-slate-900 dark:border-purple-800 dark:bg-slate-900 dark:text-slate-100",
                              )}
                            >
                              <div className="mb-1 flex flex-wrap items-center justify-between gap-2">
                                <span className="font-bold">
                                  {rep.senderName}
                                </span>
                                <div className="flex items-center gap-2">
                                  <Badge
                                    variant="outline"
                                    className={cn(
                                      "text-[10px]",
                                      isUnresolved
                                        ? "border-rose-300 bg-rose-100 text-rose-800"
                                        : "border-purple-300 bg-purple-100 text-purple-800",
                                    )}
                                  >
                                    {isUnresolved
                                      ? "Chưa xử lý được • Cần chuyển người"
                                      : "Đã báo cáo kết quả"}
                                  </Badge>
                                  <span className="text-[11px] text-muted-foreground">
                                    {formatZaloTime(rep.createdAt)}
                                  </span>
                                </div>
                              </div>
                              <p className="whitespace-pre-line leading-relaxed">
                                {rep.ticketMessageContent}
                              </p>
                            </div>
                          );
                        })}
                      </CardContent>
                    </Card>
                  )}

                  {/* Khung Phân công hợp nhất (Cho phép chọn Trưởng phòng tự xử lý, chọn 1 người, chọn nhiều người, hoặc chuyển giao sang người thứ 2) */}
                  <Card className="border-blue-500/30 bg-blue-500/5 shadow-xs">
                    <CardHeader className="border-b border-blue-200/50 pb-3 dark:border-blue-900/50">
                      <div className="flex flex-wrap items-center justify-between gap-2">
                        <div className="flex items-center gap-2.5">
                          <div className="flex size-9 items-center justify-center rounded-lg bg-blue-600/10 text-blue-600 dark:text-blue-400">
                            <UserPlus className="size-5" />
                          </div>
                          <div>
                            <CardTitle className="text-base font-bold text-blue-950 dark:text-blue-200">
                              Phân công & Điều phối nhân sự xử lý (
                              {ticket.department})
                            </CardTitle>
                            <CardDescription className="text-xs">
                              Chọn trực tiếp Trưởng phòng để tự xử lý, hoặc chọn
                              1 / nhiều nhân viên cùng phối hợp, hoặc chuyển
                              giao sang nhân viên khác khi người đầu chưa xử lý
                              được
                            </CardDescription>
                          </div>
                        </div>

                        {ticket.assignedEmployeeName && (
                          <Badge
                            variant="outline"
                            className="border-blue-300 bg-background text-xs font-semibold text-blue-700 dark:border-blue-800 dark:text-blue-300"
                          >
                            Đang phụ trách: {ticket.assignedEmployeeName}
                          </Badge>
                        )}
                      </div>
                    </CardHeader>

                    <CardContent className="space-y-5 pt-5 text-sm">
                      <div className="grid grid-cols-1 gap-5 lg:grid-cols-12">
                        {/* Cột trái: Danh sách chọn nhân sự (Trưởng phòng + Nhân viên, chọn 1 hoặc nhiều người) */}
                        <div className="space-y-2.5 lg:col-span-7">
                          <div className="flex items-center justify-between">
                            <Label className="text-xs font-semibold">
                              Chọn nhân sự phụ trách (có thể chọn Trưởng phòng
                              hoặc 1 / nhiều nhân viên):
                            </Label>
                            <span className="text-xs font-semibold text-blue-700 dark:text-blue-300">
                              Đã chọn: {selectedStaffIds.length} người
                            </span>
                          </div>

                          {loadingStaff ? (
                            <div className="flex items-center gap-2 rounded-xl border bg-background p-4 text-xs text-muted-foreground">
                              <Loader2 className="size-4 animate-spin text-primary" />
                              Đang tải danh sách nhân sự trong phòng ban...
                            </div>
                          ) : departmentStaff.length === 0 ? (
                            <div className="rounded-xl border bg-background p-4 text-xs text-muted-foreground">
                              Không tìm thấy nhân sự khả dụng trong phòng ban.
                            </div>
                          ) : (
                            <StaffPicker staff={departmentStaff} selected={selectedStaffIds} onToggle={toggleStaffSelection} disabled={isTicketResolved}
                              activeIds={activeAssigneeRecords.map(a => a.employeeId)} pastIds={pastAssigneeRecords.map(a => a.employeeId)}/>
                          )}
                        </div>

                        {/* Cột phải: Độ ưu tiên & Ghi chú chỉ đạo / bàn giao */}
                        <div className="space-y-4 lg:col-span-5">
                          <div className="space-y-2">
                            <Label className="text-xs font-semibold">
                              Độ ưu tiên xử lý:
                            </Label>
                            <Select
                              disabled={isTicketResolved}
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
                              {requiresTransferReason ? "Lý do chuyển người xử lý *" : "Ghi chú chỉ đạo / Nội dung bàn giao xử lý"}
                            </Label>
                            <Textarea
                              disabled={isTicketResolved}
                              value={assignNote}
                              onChange={(e) => setAssignNote(e.target.value)}
                              placeholder="Nhập hướng dẫn xử lý hoặc ghi chú bàn giao từ người trước cho nhân sự mới..."
                              className="min-h-[125px] resize-y bg-background text-sm"
                              maxLength={500}
                            />
                          </div>
                        </div>
                      </div>

                      {!isTicketResolved && (
                        <div className="flex flex-wrap items-center justify-between gap-3 border-t border-blue-200/60 pt-4 dark:border-blue-900/50">
                          <p className="text-xs text-muted-foreground">
                            💡 Nếu chọn{" "}
                            <strong>⭐ [Trưởng phòng]</strong> duy nhất, hệ
                            thống sẽ chuyển thẳng sang mục{" "}
                            <em>Xử lý & Báo khách hàng</em>. Nếu đổi từ người
                            đầu sang người thứ 2, lịch sử của người đầu vẫn được
                            lưu lại đầy đủ.
                          </p>
                          <Button
                            type="button"
                            onClick={() => void handleAssignToStaff()}
                            disabled={
                              assigning ||
                              selectedStaffIds.length === 0 || (requiresTransferReason && !assignNote.trim()) ||
                              isTicketResolved
                            }
                            className="gap-2 bg-blue-600 text-white hover:bg-blue-700"
                          >
                            {assigning ? (
                              <Loader2 className="size-4 animate-spin" />
                            ) : (
                              <UserPlus className="size-4" />
                            )}
                            <span>
                              {activeAssigneeRecords.length > 0
                                ? "Cập nhật phân công / Chuyển giao xử lý"
                                : "Xác nhận phân công"}
                            </span>
                          </Button>
                        </div>
                      )}
                    </CardContent>
                  </Card>
                </div>
              )}

              {/* ================= TAB 2: XỬ LÝ & BÁO KẾT QUẢ ================= */}
              {activeTab === "resolve" && (
                <div className="space-y-6">
                  {/* Giao diện Trưởng phòng: Duyệt kết quả & Báo cho khách hàng (Đã xóa nút quay lại trang phân công) */}
                  {isDepartmentHead && (
                    <Card className="border-emerald-500/30 bg-emerald-500/5 shadow-xs">
                      <CardHeader className="border-b border-emerald-200/50 pb-3 dark:border-emerald-900/50">
                        <div className="flex flex-wrap items-center justify-between gap-2">
                          <div className="flex items-center gap-2.5">
                            <div className="flex size-9 items-center justify-center rounded-lg bg-emerald-600/10 text-emerald-600 dark:text-emerald-400">
                              <CheckCircle2 className="size-5" />
                            </div>
                            <div>
                              <CardTitle className="text-base font-bold text-emerald-950 dark:text-emerald-200">
                                Xử lý yêu cầu & Báo kết quả cho Khách hàng
                              </CardTitle>
                              <CardDescription className="text-xs">
                                Chỉ Trưởng phòng mới gửi kết quả cuối cùng cho
                                khách hàng và đóng phiếu hỗ trợ
                              </CardDescription>
                            </div>
                          </div>

                          {ticket.assignedEmployeeName && (
                            <Badge
                              variant="outline"
                              className="border-emerald-300 bg-background text-xs font-semibold text-emerald-700 dark:border-emerald-800 dark:text-emerald-300"
                            >
                              Người phụ trách: {ticket.assignedEmployeeName}
                            </Badge>
                          )}
                        </div>
                      </CardHeader>

                      <CardContent className="space-y-4 pt-4 text-sm">
                        {/* Danh sách báo cáo nội bộ từ các nhân viên */}
                        {staffOnlyReports.length > 0 && (
                          <div className="space-y-2.5 rounded-xl border border-purple-200 bg-purple-50/70 p-4 dark:border-purple-800 dark:bg-purple-950/30">
                            <p className="text-xs font-bold text-purple-950 dark:text-purple-200">
                              📋 Lịch sử báo cáo kết quả từ Nhân viên gửi lên
                              Trưởng phòng:
                            </p>
                            {staffOnlyReports.map((rep) => {
                              const isUnresolved =
                                rep.ticketMessageType === "STAFF_REPORT";
                              return (
                                <div
                                  key={rep.ticketMessageId}
                                  className="rounded-lg border bg-background p-3 text-xs"
                                >
                                  <div className="flex flex-wrap items-center justify-between gap-2">
                                    <div className="flex items-center gap-2">
                                      <span className="font-bold text-foreground">
                                        {rep.senderName}
                                      </span>
                                      <Badge
                                        variant="outline"
                                        className={cn(
                                          "text-[10px]",
                                          isUnresolved
                                            ? "border-rose-300 bg-rose-50 text-rose-700"
                                            : "border-emerald-300 bg-emerald-50 text-emerald-700",
                                        )}
                                      >
                                        {isUnresolved
                                          ? "Báo chưa xử lý được / Xin chuyển người"
                                          : "Báo đã hoàn thành"}
                                      </Badge>
                                      <span className="text-[11px] text-muted-foreground">
                                        {formatZaloTime(rep.createdAt)}
                                      </span>
                                    </div>
                                    {!isTicketResolved && !isUnresolved && (
                                      <button
                                        type="button"
                                        onClick={() =>
                                          setHeadResolution(
                                            rep.ticketMessageContent.replace(
                                              /^\[BÁO CÁO KẾT QUẢ XỬ LÝ TỪ NHÂN VIÊN\]\s*/i,
                                              "",
                                            ),
                                          )
                                        }
                                        className="rounded-md border border-purple-300 bg-purple-50 px-2.5 py-1 text-[11px] font-semibold text-purple-700 hover:bg-purple-100 dark:border-purple-700 dark:bg-slate-800 dark:text-purple-300"
                                      >
                                        Sử dụng nội dung này báo khách hàng
                                      </button>
                                    )}
                                  </div>
                                  <p className="mt-1.5 whitespace-pre-wrap leading-relaxed text-foreground">
                                    {rep.ticketMessageContent}
                                  </p>
                                </div>
                              );
                            })}
                          </div>
                        )}

                        {!isTicketResolved && (
                          <div className="space-y-1.5">
                            <Label className="text-xs font-semibold text-muted-foreground">
                              Mẫu phản hồi kết quả nhanh (nhấn để điền nhanh):
                            </Label>
                            <div className="flex flex-wrap gap-1.5">
                              {QUICK_RESOLUTION_TEMPLATES.map((tpl, idx) => (
                                <button
                                  key={idx}
                                  type="button"
                                  onClick={() => setHeadResolution(tpl)}
                                  className="rounded-lg border border-emerald-200 bg-background px-3 py-1.5 text-left text-xs text-slate-700 transition-colors hover:border-emerald-400 hover:bg-emerald-50 dark:border-emerald-900 dark:text-slate-300 dark:hover:bg-emerald-950/50"
                                >
                                  Mẫu {idx + 1}: {tpl.slice(0, 58)}...
                                </button>
                              ))}
                            </div>
                          </div>
                        )}

                        <div className="space-y-2">
                          <Label className="text-xs font-semibold">
                            Nội dung kết quả chính thức gửi tới Khách hàng:
                          </Label>
                          <Textarea
                            disabled={isTicketResolved}
                            value={headResolution}
                            onChange={(e) => setHeadResolution(e.target.value)}
                            placeholder="Nhập phương án / kết quả giải quyết chính thức để thông báo cho khách hàng và đóng yêu cầu hỗ trợ..."
                            className="min-h-[150px] resize-y bg-background text-sm leading-relaxed"
                            maxLength={2000}
                          />
                        </div>

                        {!isTicketResolved && (
                          <div className="flex justify-end pt-1">
                            <Button
                              type="button"
                              onClick={() =>
                                void handleSendResultToCustomer(headResolution)
                              }
                              disabled={resolving || !headResolution.trim()}
                              className="gap-2 bg-emerald-600 text-white hover:bg-emerald-700"
                            >
                              {resolving ? (
                                <Loader2 className="size-4 animate-spin" />
                              ) : (
                                <Send className="size-4" />
                              )}
                              <span>
                                Gửi kết quả & Báo cho Khách hàng (Đóng Ticket)
                              </span>
                            </Button>
                          </div>
                        )}
                      </CardContent>
                    </Card>
                  )}

                  {/* Giao diện Nhân viên: Chỉ gửi báo cáo kết quả (hoặc báo chưa xử lý được) lên Trưởng phòng, KHÔNG nhắn tin khách hàng */}
                  {isStaff && (
                    <Card className="border-purple-500/30 bg-purple-500/5 shadow-xs">
                      <CardHeader className="border-b border-purple-200/50 pb-3 dark:border-purple-900/50">
                        <div className="flex flex-wrap items-center justify-between gap-2">
                          <div className="flex items-center gap-2.5">
                            <div className="flex size-9 items-center justify-center rounded-lg bg-purple-600/10 text-purple-600 dark:text-purple-400">
                              <ClipboardCheck className="size-5" />
                            </div>
                            <div>
                              <CardTitle className="text-base font-bold text-purple-950 dark:text-purple-200">
                                Báo cáo kết quả xử lý cho Trưởng phòng
                              </CardTitle>
                              <CardDescription className="text-xs">
                                Nhân viên chỉ gửi báo cáo nội bộ lên Trưởng
                                phòng {ticket.departmentHeadName || ""} để
                                Trưởng phòng phản hồi cho khách hàng
                              </CardDescription>
                            </div>
                          </div>

                          {wasPreviouslyAssignedAndHandedOver && (
                            <Badge
                              variant="outline"
                              className="border-amber-300 bg-amber-50 text-amber-800"
                            >
                              Đã bàn giao sang nhân sự khác (Chỉ xem)
                            </Badge>
                          )}
                        </div>
                      </CardHeader>
                      <CardContent className="space-y-4 pt-4 text-sm">
                        {internalReports.length > 0 && (
                          <div className="space-y-2 rounded-lg border bg-background p-3.5">
                            <p className="text-xs font-bold text-muted-foreground">
                              Nội dung điều phối từ Trưởng phòng & Lịch sử báo
                              cáo nội bộ (Gồm cả người xử lý trước):
                            </p>
                            {internalReports.map((rep) => (
                              <div
                                key={rep.ticketMessageId}
                                className="rounded border-l-2 border-purple-500 bg-slate-50 px-3 py-2 text-xs dark:bg-slate-900"
                              >
                                <div className="mb-1 flex items-center justify-between">
                                  <span className="font-bold text-foreground">
                                    {rep.senderName}
                                  </span>
                                  <span className="text-[11px] text-muted-foreground">
                                    {formatZaloTime(rep.createdAt)}
                                  </span>
                                </div>
                                <p className="whitespace-pre-line text-foreground">
                                  {rep.ticketMessageContent}
                                </p>
                              </div>
                            ))}
                          </div>
                        )}

                        {isTicketResolved ? (
                          <div className="rounded-xl border border-emerald-200 bg-emerald-50/70 p-4 text-xs text-emerald-900 dark:border-emerald-800 dark:bg-emerald-950/30 dark:text-emerald-200">
                            <p className="font-bold">
                              Kết quả xử lý cuối cùng đã gửi Khách hàng:
                            </p>
                            <p className="mt-1.5 whitespace-pre-wrap leading-relaxed">
                              {ticket.ticketResolution ||
                                "Yêu cầu hỗ trợ đã hoàn thành."}
                            </p>
                          </div>
                        ) : !isCurrentlyAssignedStaff ? (
                          <div className="rounded-xl border border-amber-200 bg-amber-50/80 p-4 text-xs text-amber-900 dark:border-amber-800 dark:bg-amber-950/30 dark:text-amber-200">
                            Phiếu hỗ trợ này hiện đã được Trưởng phòng chuyển
                            giao cho{" "}
                            <strong>
                              {ticket.assignedEmployeeName || "nhân sự khác"}
                            </strong>{" "}
                            tiếp tục xử lý. Bạn chỉ có thể theo dõi tiến độ và
                            xem lịch sử.
                          </div>
                        ) : (
                          <>
                            <div className="space-y-2">
                              <Label className="text-xs font-semibold">
                                Trạng thái thực hiện công việc:
                              </Label>
                              <div className="grid grid-cols-1 gap-2.5 sm:grid-cols-2">
                                <button
                                  type="button"
                                  onClick={() =>
                                    setStaffReportType("COMPLETED")
                                  }
                                  className={cn(
                                    "flex items-start gap-2.5 rounded-xl border p-3 text-left transition-all",
                                    staffReportType === "COMPLETED"
                                      ? "border-emerald-600 bg-emerald-50/80 ring-1 ring-emerald-600 dark:bg-emerald-950/40"
                                      : "border-border bg-background hover:bg-slate-50",
                                  )}
                                >
                                  <CheckCircle2 className="mt-0.5 size-4 shrink-0 text-emerald-600" />
                                  <div>
                                    <p className="text-xs font-bold text-foreground">
                                      Đã xử lý hoàn tất
                                    </p>
                                    <p className="text-[11px] text-muted-foreground">
                                      Gửi kết quả xử lý lên Trưởng phòng duyệt
                                      và báo lại cho khách hàng
                                    </p>
                                  </div>
                                </button>

                                <button
                                  type="button"
                                  onClick={() =>
                                    setStaffReportType("UNRESOLVED")
                                  }
                                  className={cn(
                                    "flex items-start gap-2.5 rounded-xl border p-3 text-left transition-all",
                                    staffReportType === "UNRESOLVED"
                                      ? "border-rose-600 bg-rose-50/80 ring-1 ring-rose-600 dark:bg-rose-950/40"
                                      : "border-border bg-background hover:bg-slate-50",
                                  )}
                                >
                                  <AlertTriangle className="mt-0.5 size-4 shrink-0 text-rose-600" />
                                  <div>
                                    <p className="text-xs font-bold text-foreground">
                                      Chưa xử lý được / Đề xuất chuyển người
                                    </p>
                                    <p className="text-[11px] text-muted-foreground">
                                      Báo rõ nguyên nhân để Trưởng phòng phân
                                      công người thứ 2 hoặc trực tiếp xử lý
                                    </p>
                                  </div>
                                </button>
                              </div>
                            </div>

                            <div className="space-y-2">
                              <Label className="text-sm font-semibold">
                                {staffReportType === "UNRESOLVED"
                                  ? "Nguyên nhân chưa khắc phục được & Nội dung bàn giao lại:"
                                  : "Nội dung kết quả xử lý gửi lên Trưởng phòng:"}
                              </Label>
                              <Textarea
                                value={staffResolution}
                                onChange={(e) =>
                                  setStaffResolution(e.target.value)
                                }
                                placeholder={
                                  staffReportType === "UNRESOLVED"
                                    ? "Mô tả các bước đã kiểm tra, lý do chưa xử lý được để Trưởng phòng nắm rõ và phân công người tiếp theo..."
                                    : "Mô tả chi tiết nguyên nhân và kết quả đã xử lý xong để Trưởng phòng báo cho khách hàng..."
                                }
                                className="min-h-[150px] resize-y bg-background text-sm leading-relaxed"
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
                                className={cn(
                                  "gap-2 text-white",
                                  staffReportType === "UNRESOLVED"
                                    ? "bg-rose-600 hover:bg-rose-700"
                                    : "bg-purple-600 hover:bg-purple-700",
                                )}
                              >
                                {staffCompleting ? (
                                  <Loader2 className="size-4 animate-spin" />
                                ) : (
                                  <Send className="size-4" />
                                )}
                                <span>
                                  {staffReportType === "UNRESOLVED"
                                    ? "Gửi báo cáo & Đề xuất chuyển người lên Trưởng phòng"
                                    : "Gửi kết quả xử lý lên Trưởng phòng"}
                                </span>
                              </Button>
                            </div>
                          </>
                        )}
                      </CardContent>
                    </Card>
                  )}
                </div>
              )}

              {/* ================= TAB 4: LỊCH SỬ HỖ TRỢ & THÔNG TIN (GIAO DIỆN TIMELINE MỚI) ================= */}
              {activeTab === "history" && (
                <div className="space-y-6">
                  {renderCustomerRequestCard()}
                  <div className="grid grid-cols-1 gap-6 lg:grid-cols-12">
                    {/* Card 1: Thông tin Khách hàng & Dịch vụ */}
                    <Card className="border-border shadow-xs lg:col-span-5">
                      <CardHeader className="border-b bg-slate-50/60 pb-3 dark:bg-slate-900/40">
                        <div className="flex items-center gap-2.5">
                          <div className="flex size-8 items-center justify-center rounded-lg bg-primary/10 text-primary">
                            <Building2 className="size-4" />
                          </div>
                          <div>
                            <CardTitle className="text-base font-bold">
                              Thông tin Phiếu & Khách hàng
                            </CardTitle>
                            <CardDescription className="text-xs">
                              Chi tiết dịch vụ và thông tin định danh yêu cầu
                            </CardDescription>
                          </div>
                        </div>
                      </CardHeader>
                      <CardContent className="space-y-3.5 pt-4 text-sm">
                        <div className="flex items-center justify-between border-b pb-2.5">
                          <span className="text-xs text-muted-foreground">
                            Mã phiếu hỗ trợ:
                          </span>
                          <div className="flex items-center gap-1.5">
                            <span className="font-mono text-sm font-bold text-primary">
                              #{ticket.ticketCode}
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

                        <div className="flex items-center justify-between border-b pb-2.5">
                          <span className="text-xs text-muted-foreground">
                            Khách hàng gửi yêu cầu:
                          </span>
                          <span className="font-semibold text-foreground">
                            {ticket.createdByName ||
                              ticket.tenantName ||
                              "Khách hàng"}
                          </span>
                        </div>

                        <div className="flex items-center justify-between border-b pb-2.5">
                          <span className="text-xs text-muted-foreground">
                            Dịch vụ cần hỗ trợ:
                          </span>
                          <span className="font-bold text-teal-600 dark:text-teal-400">
                            {serviceLabel}
                          </span>
                        </div>

                        <div className="flex items-center justify-between border-b pb-2.5">
                          <span className="text-xs text-muted-foreground">
                            Phòng ban tiếp nhận:
                          </span>
                          <span className="font-semibold text-foreground">
                            {ticket.department}
                          </span>
                        </div>

                        <div className="flex items-center justify-between border-b pb-2.5">
                          <span className="text-xs text-muted-foreground">
                            Thời gian gửi yêu cầu:
                          </span>
                          <span className="text-xs font-medium text-foreground">
                            {formatZaloTime(ticket.createdAt)}
                          </span>
                        </div>

                        <div className="flex items-center justify-between">
                          <span className="text-xs text-muted-foreground">
                            Thời gian hoàn thành:
                          </span>
                          <span className="text-xs font-semibold text-emerald-600 dark:text-emerald-400">
                            {ticket.ticketResolvedAt
                              ? formatZaloTime(ticket.ticketResolvedAt)
                              : "Đang trong quá trình xử lý"}
                          </span>
                        </div>

                        {ticket.ticketResolution && (
                          <div className="mt-3 rounded-xl border border-emerald-200 bg-emerald-50/70 p-3 text-xs dark:border-emerald-800 dark:bg-emerald-950/30">
                            <p className="font-bold text-emerald-900 dark:text-emerald-200">
                              ✅ Kết quả xử lý:
                            </p>
                            <p className="mt-1 whitespace-pre-wrap leading-relaxed text-emerald-950 dark:text-emerald-100">
                              {ticket.ticketResolution}
                            </p>
                          </div>
                        )}
                      </CardContent>
                    </Card>

                    {/* Card 2: Sơ đồ Phân công & Lịch sử chuyển giao nhân sự */}
                    <Card className="border-border shadow-xs lg:col-span-7">
                      <CardHeader className="border-b bg-slate-50/60 pb-3 dark:bg-slate-900/40">
                        <div className="flex items-center justify-between gap-2">
                          <div className="flex items-center gap-2.5">
                            <div className="flex size-8 items-center justify-center rounded-lg bg-blue-600/10 text-blue-600">
                              <Users className="size-4" />
                            </div>
                            <div>
                              <CardTitle className="text-base font-bold">
                                Lịch sử Phân công & Bàn giao Nhân sự
                              </CardTitle>
                              <CardDescription className="text-xs">
                                Theo dõi toàn bộ các đợt giao việc, phối hợp
                                nhóm và chuyển giao người xử lý
                              </CardDescription>
                            </div>
                          </div>
                        </div>
                      </CardHeader>

                      <CardContent className="space-y-4 pt-4 text-sm">
                        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
                          <div className="rounded-xl border bg-blue-50/50 p-3 dark:bg-blue-950/20">
                            <div className="flex items-center gap-2 text-xs text-muted-foreground">
                              <UserCheck className="size-4 text-blue-600" />
                              <span>Trưởng phòng điều phối:</span>
                            </div>
                            <p className="mt-1 font-bold text-foreground">
                              {ticket.departmentHeadName || "Trưởng bộ phận"}
                            </p>
                          </div>

                          <div className="rounded-xl border bg-emerald-50/50 p-3 dark:bg-emerald-950/20">
                            <div className="flex items-center gap-2 text-xs text-muted-foreground">
                              <User className="size-4 text-emerald-600" />
                              <span>Nhân sự đang phụ trách:</span>
                            </div>
                            <p className="mt-1 font-bold text-emerald-700 dark:text-emerald-300">
                              {ticket.assignedEmployeeName || "Chưa phân công"}
                            </p>
                          </div>
                        </div>

                        {ticket.assignments && ticket.assignments.length > 0 ? (
                          <div className="space-y-2.5">
                            <p className="text-xs font-bold tracking-wider text-muted-foreground uppercase">
                              Chi tiết từng đợt điều phối (
                              {ticket.assignments.length} bản ghi)
                            </p>
                            <div className="space-y-2">
                              {ticket.assignments.map((a, index) => {
                                const isHeadRole =
                                  a.assignmentRole === "DEPARTMENT_HEAD";
                                const isHandedOver = Boolean(a.unassignedAt);
                                return (
                                  <div
                                    key={`${a.ticketSupportEmployeeId}-${index}`}
                                    className={cn(
                                      "flex flex-wrap items-center justify-between gap-2 rounded-xl border p-3 text-xs transition-colors",
                                      isHeadRole
                                        ? "border-blue-200 bg-blue-50/30 dark:border-blue-900/50 dark:bg-blue-950/20"
                                        : isHandedOver
                                          ? "border-slate-200 bg-slate-50/80 opacity-85 dark:border-slate-800 dark:bg-slate-900/40"
                                          : "border-emerald-300 bg-emerald-50/40 dark:border-emerald-800 dark:bg-emerald-950/20",
                                    )}
                                  >
                                    <div className="space-y-0.5">
                                      <div className="flex items-center gap-2">
                                        <span className="font-bold text-foreground">
                                          {a.employeeName}
                                        </span>
                                        {a.employeeCode && (
                                          <span className="font-mono text-[11px] text-muted-foreground">
                                            ({a.employeeCode})
                                          </span>
                                        )}
                                      </div>
                                      <p className="text-[11px] text-muted-foreground">
                                        Giao lúc: {formatZaloTime(a.assignedAt)}
                                        {a.assignedByName
                                          ? ` • Bởi: ${a.assignedByName}`
                                          : ""}
                                        {isHandedOver
                                          ? ` → Chuyển giao lúc: ${formatZaloTime(a.unassignedAt)}`
                                          : ""}
                                      </p>
                                    </div>

                                    <Badge
                                      variant="outline"
                                      className={cn(
                                        "text-[10px] font-semibold",
                                        isHeadRole
                                          ? "border-blue-300 bg-blue-100 text-blue-800 dark:bg-blue-950 dark:text-blue-300"
                                          : isHandedOver
                                            ? "border-amber-300 bg-amber-100 text-amber-800 dark:bg-amber-950 dark:text-amber-300"
                                            : "border-emerald-300 bg-emerald-100 text-emerald-800 dark:bg-emerald-950 dark:text-emerald-300",
                                      )}
                                    >
                                      {isHeadRole
                                        ? "Trưởng phòng tiếp nhận"
                                        : isHandedOver
                                          ? "Đã bàn giao / Chuyển người"
                                          : "Đang phụ trách xử lý"}
                                    </Badge>
                                  </div>
                                );
                              })}
                            </div>
                          </div>
                        ) : (
                          <p className="text-xs text-muted-foreground">
                            Chưa có bản ghi phân công nhân sự.
                          </p>
                        )}
                      </CardContent>
                    </Card>
                  </div>

                  {/* Card 3: Dòng thời gian (Timeline) toàn bộ lịch sử hỗ trợ */}
                  <Card className="border-border shadow-xs">
                    <CardHeader className="border-b bg-slate-50/60 pb-3 dark:bg-slate-900/40">
                      <div className="flex items-center gap-2.5">
                        <div className="flex size-8 items-center justify-center rounded-lg bg-primary/10 text-primary">
                          <History className="size-4" />
                        </div>
                        <div>
                          <CardTitle className="text-base font-bold">
                            Dòng thời gian Lịch sử Hỗ trợ & Xử lý
                          </CardTitle>
                          <CardDescription className="text-xs">
                            Toàn bộ nhật ký từ lúc khách hàng khởi tạo, các bước
                            phân công, báo cáo nội bộ đến khi hoàn thành
                          </CardDescription>
                        </div>
                      </div>
                    </CardHeader>
                    <CardContent className="pt-5">
                      {!ticket.messages || ticket.messages.length === 0 ? (
                        <p className="text-center text-xs text-muted-foreground">
                          Chưa có dữ liệu lịch sử hoạt động.
                        </p>
                      ) : (
                        <div className="relative space-y-4 before:absolute before:top-2 before:bottom-2 before:left-4 before:w-0.5 before:bg-slate-200 dark:before:bg-slate-800">
                          {ticket.messages.map((ev) => {
                            const type = ev.ticketMessageType;
                            const evImages = getMessageImages(ev);
                            let badgeLabel = "Trao đổi với khách hàng";
                            let badgeClass =
                              "border-blue-200 bg-blue-50 text-blue-700";
                            let dotClass = "bg-blue-600 ring-blue-100";

                            if (type === "INITIAL") {
                              badgeLabel = "Khởi tạo yêu cầu";
                              badgeClass =
                                "border-teal-200 bg-teal-50 text-teal-700";
                              dotClass = "bg-teal-600 ring-teal-100";
                            } else if (type === "ASSIGNMENT") {
                              badgeLabel = "Điều phối / Phân công";
                              badgeClass =
                                "border-indigo-200 bg-indigo-50 text-indigo-700";
                              dotClass = "bg-indigo-600 ring-indigo-100";
                            } else if (type === "STAFF_REPORT") {
                              badgeLabel =
                                "Báo cáo chưa xử lý được • Đề xuất chuyển người";
                              badgeClass =
                                "border-rose-200 bg-rose-50 text-rose-700";
                              dotClass = "bg-rose-600 ring-rose-100";
                            } else if (type === "STAFF_COMPLETION") {
                              badgeLabel = "Báo cáo kết quả nội bộ";
                              badgeClass =
                                "border-purple-200 bg-purple-50 text-purple-700";
                              dotClass = "bg-purple-600 ring-purple-100";
                            } else if (type === "RESOLUTION") {
                              badgeLabel =
                                "Hoàn thành & Báo kết quả Khách hàng";
                              badgeClass =
                                "border-emerald-200 bg-emerald-50 text-emerald-700";
                              dotClass = "bg-emerald-600 ring-emerald-100";
                            }

                            return (
                              <div
                                key={ev.ticketMessageId}
                                className="relative pl-10"
                              >
                                <span
                                  className={cn(
                                    "absolute top-3 left-2.5 size-3.5 rounded-full ring-4",
                                    dotClass,
                                  )}
                                />
                                <div className="rounded-xl border bg-background p-3.5 shadow-2xs">
                                  <div className="flex flex-wrap items-center justify-between gap-2">
                                    <div className="flex flex-wrap items-center gap-2">
                                      <span className="text-xs font-bold text-foreground">
                                        {ev.senderName}
                                      </span>
                                      <Badge
                                        variant="outline"
                                        className={cn(
                                          "text-[10px] font-semibold",
                                          badgeClass,
                                        )}
                                      >
                                        {badgeLabel}
                                      </Badge>
                                    </div>
                                    <span className="flex items-center gap-1 text-[11px] text-muted-foreground">
                                      <Clock className="size-3" />
                                      {formatZaloTime(ev.createdAt)}
                                    </span>
                                  </div>

                                  {ev.ticketMessageContent && (
                                    <p className="mt-2 whitespace-pre-line text-xs leading-relaxed text-foreground">
                                      {ev.ticketMessageContent}
                                    </p>
                                  )}

                                  {evImages.length > 0 && (
                                    <div className="mt-2.5 flex flex-wrap gap-2">
                                      {evImages.map((imgUrl, idx) => (
                                        <button
                                          key={idx}
                                          type="button"
                                          onClick={() => {
                                            setLightboxZoom(1);
                                            setLightboxImage({
                                              url: imgUrl,
                                              title: `Ảnh đính kèm #${idx + 1}`,
                                            });
                                          }}
                                          className="overflow-hidden rounded-lg border"
                                        >
                                          <img
                                            src={imgUrl}
                                            alt="Ảnh đính kèm"
                                            className="h-20 w-28 object-cover"
                                          />
                                        </button>
                                      ))}
                                    </div>
                                  )}
                                </div>
                              </div>
                            );
                          })}
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

      {lightboxImage && (
        <div
          onClick={() => {
            setLightboxImage(null);
            setLightboxZoom(1);
          }}
          className="fixed inset-0 z-50 flex flex-col items-center justify-center bg-black/90 p-4 backdrop-blur-xs"
        >
          <div
            onClick={(e) => e.stopPropagation()}
            className="mb-3 flex w-full max-w-4xl items-center justify-between rounded-xl bg-white/10 px-4 py-2.5 text-white backdrop-blur-md"
          >
            <span className="truncate text-sm font-medium">
              {lightboxImage.title || "Xem hình ảnh đính kèm"}
            </span>
            <div className="flex items-center gap-2">
              <button
                type="button"
                onClick={() =>
                  setLightboxZoom((z) =>
                    Math.max(0.5, Number((z - 0.25).toFixed(2))),
                  )
                }
                className="flex size-8 items-center justify-center rounded-lg bg-white/15 hover:bg-white/25"
                title="Thu nhỏ"
              >
                <ZoomOut className="size-4" />
              </button>
              <span className="min-w-12 text-center text-xs font-semibold">
                {Math.round(lightboxZoom * 100)}%
              </span>
              <button
                type="button"
                onClick={() =>
                  setLightboxZoom((z) =>
                    Math.min(3, Number((z + 0.25).toFixed(2))),
                  )
                }
                className="flex size-8 items-center justify-center rounded-lg bg-white/15 hover:bg-white/25"
                title="Phóng to"
              >
                <ZoomIn className="size-4" />
              </button>
              <button
                type="button"
                onClick={() => {
                  setLightboxImage(null);
                  setLightboxZoom(1);
                }}
                className="ml-2 flex size-8 items-center justify-center rounded-lg bg-rose-600 text-white hover:bg-rose-700"
                title="Đóng (Esc)"
              >
                <X className="size-4" />
              </button>
            </div>
          </div>

          <div
            onClick={(e) => e.stopPropagation()}
            className="flex max-h-[82vh] max-w-[92vw] items-center justify-center overflow-auto rounded-xl"
          >
            <img
              src={lightboxImage.url}
              alt={lightboxImage.title || "Preview"}
              style={{
                transform: `scale(${lightboxZoom})`,
                transformOrigin: "center center",
              }}
              className="max-h-[80vh] max-w-[90vw] rounded-lg object-contain transition-transform duration-150"
            />
          </div>
        </div>
      )}
    </>
  );
}

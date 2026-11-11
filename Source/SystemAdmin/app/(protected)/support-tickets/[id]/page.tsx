"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import {
  ArrowLeft,
  Building2,
  CheckCheck,
  CheckCircle2,
  ClipboardCheck,
  Copy,
  FileText,
  History,
  ImagePlus,
  Loader2,
  MessageCircle,
  Send,
  ShieldCheck,
  Sparkles,
  User,
  UserCheck,
  UserPlus,
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
import { Header } from "@/components/ui/custom/header";
import { useAuthStore } from "@/hooks/use-auth-store";
import { cn } from "@/lib/utils";
import {
  DepartmentStaffDto,
  ticketApi,
  TicketDto,
  TicketMessageDto,
  TicketPriority,
  TicketStatus,
  TICKET_PRIORITY_LABELS,
} from "@/services/ticket-api";

const ALLOWED_IMAGE_MIME_TYPES = new Set([
  "image/png",
  "image/jpeg",
  "image/jpg",
  "image/webp",
  "image/gif",
]);

const QUICK_RESPONSES = [
  "Chào anh/chị, phòng ban chuyên môn đã tiếp nhận yêu cầu và đang tiến hành kiểm tra xử lý.",
  "Yêu cầu của anh/chị đã được xử lý hoàn tất trên hệ thống. Anh/chị vui lòng kiểm tra lại giúp em nhé.",
  "Anh/chị vui lòng cung cấp thêm ảnh chụp màn hình hoặc mã hợp đồng liên quan để bộ phận chuyên môn kiểm tra chi tiết hơn.",
];

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

async function compressImageToDataUrl(file: File): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onerror = () => reject(new Error("Không thể đọc file ảnh"));
    reader.onload = () => {
      const result = typeof reader.result === "string" ? reader.result : "";
      if (file.type === "image/gif" || file.size <= 350 * 1024) {
        resolve(result);
        return;
      }
      const img = new window.Image();
      img.onerror = () => resolve(result);
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
        }
        const canvas = document.createElement("canvas");
        canvas.width = width;
        canvas.height = height;
        const ctx = canvas.getContext("2d");
        if (!ctx) {
          resolve(result);
          return;
        }
        ctx.drawImage(img, 0, 0, width, height);
        const compressed = canvas.toDataURL("image/jpeg", 0.84);
        resolve(compressed);
      };
      img.src = result;
    };
    reader.readAsDataURL(file);
  });
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

type AdminTabType = "assign" | "resolve" | "chat" | "history";

export default function AdminSupportTicketDetailPage() {
  const params = useParams<{ id: string }>();
  const router = useRouter();
  const ticketId = Number(params?.id);
  const user = useAuthStore((state) => state.user);

  const [ticket, setTicket] = useState<TicketDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [activeTab, setActiveTab] = useState<AdminTabType>("assign");

  const [replyMessage, setReplyMessage] = useState("");
  const [pendingChatImages, setPendingChatImages] = useState<
    { name: string; dataUrl: string }[]
  >([]);
  const [sendingReply, setSendingReply] = useState(false);

  const [lightboxImage, setLightboxImage] = useState<{
    url: string;
    title?: string;
  } | null>(null);
  const [lightboxZoom, setLightboxZoom] = useState(1);

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
  const [selfTaking, setSelfTaking] = useState(false);

  const [staffResolution, setStaffResolution] = useState("");
  const [staffCompleting, setStaffCompleting] = useState(false);

  const [headResolution, setHeadResolution] = useState("");
  const [resolving, setResolving] = useState(false);

  const chatBottomRef = useRef<HTMLDivElement | null>(null);
  const chatImageInputRef = useRef<HTMLInputElement | null>(null);
  const prevMsgCountRef = useRef<number>(0);
  const initializedResolutionRef = useRef<boolean>(false);
  const initializedTabRef = useRef<boolean>(false);

  const isSysAdmin =
    user?.supportRole === "SYSADMIN" ||
    user?.username?.toLowerCase() === "sysadmin" ||
    user?.username?.toLowerCase() === "admin";

  useEffect(() => {
    if (isSysAdmin) {
      router.replace("/dashboard");
    }
  }, [isSysAdmin, router]);

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

  const loadStaffList = useCallback(async () => {
    if (!ticketId || isSysAdmin) return;
    setLoadingStaff(true);
    try {
      const staff = await ticketApi.getDepartmentStaff(ticketId);
      setDepartmentStaff(staff);
    } catch {
      toast.error("Không thể tải danh sách nhân sự trong phòng ban");
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
          if (!initializedTabRef.current) {
            initializedTabRef.current = true;
            const role = data.viewerRole || user?.supportRole || "STAFF";
            if (role !== "DEPARTMENT_HEAD") {
              setActiveTab("resolve");
            } else if (
              data.ticketStatus === TicketStatus.PendingHeadApproval ||
              data.ticketStatus === TicketStatus.Resolved
            ) {
              setActiveTab("resolve");
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
    [ticketId, isSysAdmin, user?.supportRole],
  );

  useEffect(() => {
    void loadTicket(false);
  }, [loadTicket]);

  // Tự động đồng bộ tin nhắn và trạng thái mới mỗi 2 giây
  useEffect(() => {
    const timer = setInterval(() => {
      void loadTicket(true);
    }, 2000);
    return () => clearInterval(timer);
  }, [loadTicket]);

  const effectiveRole = ticket?.viewerRole || user?.supportRole || "STAFF";
  const isDepartmentHead = effectiveRole === "DEPARTMENT_HEAD";
  const isStaff = !isDepartmentHead;

  // Nhân viên phòng ban (STAFF) chỉ ở tab resolve hoặc history
  useEffect(() => {
    if (isStaff && (activeTab === "chat" || activeTab === "assign")) {
      setActiveTab("resolve");
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

  // Trưởng phòng phân công cho nhân viên (hoặc cho chính mình)
  const handleAssignToStaff = async () => {
    if (!selectedStaffId) {
      toast.error("Vui lòng chọn nhân sự phụ trách xử lý.");
      return;
    }
    setAssigning(true);
    try {
      const updated = await ticketApi.assignToStaff(ticketId, {
        staffEmployeeId: Number(selectedStaffId),
        priority: selectedPriority,
        note: assignNote.trim(),
      });
      const chosenMember = departmentStaff.find(
        (s) => String(s.employeeId) === selectedStaffId,
      );
      if (chosenMember?.isManager) {
        toast.success(
          "Trưởng phòng đã trực tiếp tiếp nhận xử lý yêu cầu này!",
        );
        setTicket(updated);
        setAssignNote("");
        setActiveTab("resolve");
      } else {
        toast.success(
          `Đã phân công nhân viên xử lý (Độ ưu tiên: ${TICKET_PRIORITY_LABELS[selectedPriority]})!`,
        );
        setTicket(updated);
        setAssignNote("");
      }
    } catch {
      toast.error("Không thể thực hiện phân công xử lý.");
    } finally {
      setAssigning(false);
    }
  };

  // Trưởng phòng bấm nút tự tiếp nhận xử lý trực tiếp ngay
  const handleHeadSelfTakeOver = async () => {
    const managerEntry =
      departmentStaff.find((s) => s.isManager) ||
      (ticket?.departmentHeadEmployeeId
        ? {
            employeeId: ticket.departmentHeadEmployeeId,
            employeeName: ticket.departmentHeadName || "Trưởng phòng",
            isManager: true,
          }
        : null);

    setSelfTaking(true);
    try {
      if (managerEntry?.employeeId) {
        const updated = await ticketApi.assignToStaff(ticketId, {
          staffEmployeeId: Number(managerEntry.employeeId),
          priority: selectedPriority,
          note:
            assignNote.trim() ||
            "Trưởng phòng trực tiếp tiếp nhận và xử lý yêu cầu hỗ trợ.",
        });
        setTicket(updated);
        setSelectedStaffId(String(managerEntry.employeeId));
      }
      toast.success(
        "Đã chuyển sang trang Xử lý & Báo cho khách hàng để Trưởng phòng trực tiếp giải quyết!",
      );
      setActiveTab("resolve");
    } catch {
      setActiveTab("resolve");
    } finally {
      setSelfTaking(false);
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

  const processChatImageFiles = async (fileList: FileList | File[]) => {
    const files = Array.from(fileList);
    if (files.length === 0) return;

    const currentCount = pendingChatImages.length;
    if (currentCount >= 3) {
      toast.error("Mỗi lần gửi tối đa 3 hình ảnh.");
      return;
    }

    const availableSlots = 3 - currentCount;
    const validFiles: File[] = [];

    for (const file of files) {
      if (!ALLOWED_IMAGE_MIME_TYPES.has(file.type.toLowerCase())) {
        toast.error(
          `Chỉ hỗ trợ gửi hình ảnh (.png, .jpg, .jpeg, .webp, .gif). Không hỗ trợ file "${file.name}".`,
        );
        continue;
      }
      if (file.size > 10 * 1024 * 1024) {
        toast.error(`Ảnh "${file.name}" vượt quá dung lượng 10MB.`);
        continue;
      }
      validFiles.push(file);
    }

    const toProcess = validFiles.slice(0, availableSlots);
    const added: { name: string; dataUrl: string }[] = [];

    for (const file of toProcess) {
      try {
        const dataUrl = await compressImageToDataUrl(file);
        added.push({ name: file.name || "image.png", dataUrl });
      } catch {
        toast.error(`Không thể xử lý ảnh "${file.name}".`);
      }
    }

    if (added.length > 0) {
      setPendingChatImages((prev) => [...prev, ...added]);
    }
  };

  const handleChatImageInputChange = async (
    e: React.ChangeEvent<HTMLInputElement>,
  ) => {
    if (e.target.files && e.target.files.length > 0) {
      await processChatImageFiles(e.target.files);
      e.target.value = "";
    }
  };

  const handlePasteChatImage = async (
    e: React.ClipboardEvent<HTMLInputElement>,
  ) => {
    const items = e.clipboardData?.items;
    if (!items) return;
    const imageFiles: File[] = [];
    for (let i = 0; i < items.length; i++) {
      const item = items[i];
      if (item.type.startsWith("image/")) {
        const file = item.getAsFile();
        if (file) imageFiles.push(file);
      }
    }
    if (imageFiles.length > 0) {
      e.preventDefault();
      await processChatImageFiles(imageFiles);
    }
  };

  const removePendingChatImage = (index: number) => {
    setPendingChatImages((prev) => prev.filter((_, i) => i !== index));
  };

  const handleSendReply = async (e?: React.FormEvent) => {
    e?.preventDefault();
    if (!ticket || !isDepartmentHead) return;
    if (!replyMessage.trim() && pendingChatImages.length === 0) return;

    setSendingReply(true);
    try {
      const imageUrls = pendingChatImages.map((img) => img.dataUrl);
      const imageNames = pendingChatImages.map((img) => img.name);

      const newMsg = await ticketApi.replyMessage(ticket.ticketSupportId, {
        messageContent: replyMessage.trim(),
        attachmentUrl: imageUrls[0] ?? null,
        attachmentName: imageNames[0] ?? null,
        attachmentUrls: imageUrls,
        attachmentNames: imageNames,
      });
      setTicket((prev) => {
        if (!prev) return null;
        return {
          ...prev,
          messages: [...(prev.messages || []), newMsg],
        };
      });
      setReplyMessage("");
      setPendingChatImages([]);
      setTimeout(() => {
        chatBottomRef.current?.scrollIntoView({ behavior: "smooth" });
      }, 50);
    } catch {
      toast.error("Không thể gửi tin nhắn cho khách hàng.");
    } finally {
      setSendingReply(false);
    }
  };

  const serviceLabel =
    ticket?.serviceName || ticket?.module || "Dịch vụ chung";
  const initialImages = getTicketImages(ticket);
  const staffMembersOnly = departmentStaff.filter((s) => !s.isManager);
  const managerMember = departmentStaff.find((s) => s.isManager);
  const isAssignedToHead =
    ticket?.assignedEmployeeId &&
    ((managerMember &&
      ticket.assignedEmployeeId === managerMember.employeeId) ||
      (ticket.departmentHeadEmployeeId &&
        ticket.assignedEmployeeId === ticket.departmentHeadEmployeeId));

  // Khối hiển thị nội dung yêu cầu gốc + hình ảnh đính kèm của khách hàng
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

          {/* Hiển thị trực tiếp hình ảnh khách hàng đính kèm (Nhấn vào mở ảnh to kiểu Zalo) */}
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

  return (
    <>
      <Header title="Chi tiết & Xử lý Yêu cầu Hỗ trợ" />

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
                onClick={() => router.push("/support-tickets")}
              >
                Quay lại danh sách
              </Button>
            </Card>
          ) : (
            <>
              {/* Thanh tiêu đề & Điều hướng tách thành các trang riêng biệt */}
              <div className="flex flex-col gap-4 rounded-xl border bg-card p-5 shadow-xs lg:flex-row lg:items-center lg:justify-between">
                <div className="space-y-1.5">
                  <button
                    type="button"
                    onClick={() => router.push("/support-tickets")}
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

                {/* Thanh chuyển trang tách biệt rõ ràng:
                    - Trưởng phòng có 4 trang: (1) Phân công xử lý | (2) Xử lý & Báo khách hàng | (3) Nhắn tin với Khách hàng | (4) Thông tin & Lịch sử
                    - Nhân viên có 2 trang: (1) Nội dung & Báo cáo kết quả | (2) Thông tin phiếu hỗ trợ */}
                <div className="inline-flex flex-wrap items-center gap-1 rounded-xl border bg-slate-100 p-1.5 dark:bg-slate-800">
                  {isDepartmentHead && (
                    <button
                      type="button"
                      onClick={() => setActiveTab("assign")}
                      className={cn(
                        "inline-flex items-center gap-2 rounded-lg px-3.5 py-2 text-xs font-semibold transition-all",
                        activeTab === "assign"
                          ? "bg-white text-blue-600 shadow-xs dark:bg-slate-900 dark:text-blue-400"
                          : "text-slate-600 hover:text-slate-900 dark:text-slate-300",
                      )}
                    >
                      <UserPlus className="size-4" />
                      <span>1. Phân công xử lý</span>
                    </button>
                  )}

                  <button
                    type="button"
                    onClick={() => setActiveTab("resolve")}
                    className={cn(
                      "inline-flex items-center gap-2 rounded-lg px-3.5 py-2 text-xs font-semibold transition-all",
                      activeTab === "resolve"
                        ? "bg-white text-emerald-600 shadow-xs dark:bg-slate-900 dark:text-emerald-400"
                        : "text-slate-600 hover:text-slate-900 dark:text-slate-300",
                    )}
                  >
                    <ClipboardCheck className="size-4" />
                    <span>
                      {isDepartmentHead
                        ? "2. Xử lý & Báo khách hàng"
                        : "1. Nội dung & Báo cáo kết quả"}
                    </span>
                    {isDepartmentHead &&
                      ticket.ticketStatus ===
                        TicketStatus.PendingHeadApproval && (
                        <span className="rounded-full bg-purple-100 px-1.5 py-0.5 text-[10px] font-bold text-purple-700 dark:bg-purple-950 dark:text-purple-300">
                          Có báo cáo
                        </span>
                      )}
                  </button>

                  {isDepartmentHead && (
                    <button
                      type="button"
                      onClick={() => setActiveTab("chat")}
                      className={cn(
                        "inline-flex items-center gap-2 rounded-lg px-3.5 py-2 text-xs font-semibold transition-all",
                        activeTab === "chat"
                          ? "bg-white text-blue-600 shadow-xs dark:bg-slate-900 dark:text-blue-400"
                          : "text-slate-600 hover:text-slate-900 dark:text-slate-300",
                      )}
                    >
                      <MessageCircle className="size-4" />
                      <span>3. Nhắn tin với Khách hàng</span>
                      <span className="rounded-full bg-blue-100 px-1.5 py-0.5 text-[10px] font-bold text-blue-700 dark:bg-blue-950 dark:text-blue-300">
                        {chatMessages.length}
                      </span>
                    </button>
                  )}

                  <button
                    type="button"
                    onClick={() => setActiveTab("history")}
                    className={cn(
                      "inline-flex items-center gap-2 rounded-lg px-3.5 py-2 text-xs font-semibold transition-all",
                      activeTab === "history"
                        ? "bg-white text-primary shadow-xs dark:bg-slate-900"
                        : "text-slate-600 hover:text-slate-900 dark:text-slate-300",
                    )}
                  >
                    <History className="size-4" />
                    <span>
                      {isDepartmentHead
                        ? "4. Lịch sử & Thông tin"
                        : "2. Thông tin phiếu hỗ trợ"}
                    </span>
                  </button>
                </div>
              </div>

              {/* =========================================================
                  TRANG 1 (DÀNH CHO TRƯỞNG PHÒNG): PHÂN CÔNG XỬ LÝ
                  (Trưởng phòng có thể chọn: Tự mình trực tiếp xử lý HOẶC Giao cho nhân viên phòng ban)
                 ========================================================= */}
              {activeTab === "assign" && isDepartmentHead && (
                <div className="space-y-6">
                  {renderCustomerRequestCard()}

                  <div className="grid grid-cols-1 gap-6 lg:grid-cols-12">
                    {/* Cột trái (5/12): Trưởng phòng trực tiếp tiếp nhận & xử lý luôn */}
                    <Card className="border-emerald-500/30 bg-emerald-500/5 shadow-xs lg:col-span-5">
                      <CardHeader className="border-b border-emerald-200/50 pb-3 dark:border-emerald-900/50">
                        <div className="flex items-center gap-2.5">
                          <div className="flex size-9 items-center justify-center rounded-lg bg-emerald-600/10 text-emerald-600 dark:text-emerald-400">
                            <Sparkles className="size-5" />
                          </div>
                          <div>
                            <CardTitle className="text-base font-bold text-emerald-950 dark:text-emerald-200">
                              Phương án 1: Trưởng phòng trực tiếp xử lý
                            </CardTitle>
                            <CardDescription className="text-xs">
                              Không cần qua nhân viên, Trưởng phòng tự xử lý và
                              phản hồi kết quả luôn cho khách hàng
                            </CardDescription>
                          </div>
                        </div>
                      </CardHeader>
                      <CardContent className=" flex flex-col justify-between space-y-4 pt-4 text-sm">
                        <div className="space-y-3">
                          <div className="rounded-xl border border-emerald-200/70 bg-background/90 p-3.5 text-xs leading-relaxed text-muted-foreground dark:border-emerald-900/60">
                            <p className="font-semibold text-foreground">
                              Khi nào nên chọn phương án này?
                            </p>
                            <ul className="mt-1.5 list-disc space-y-1 pl-4">
                              <li>
                                Yêu cầu cần phản hồi nhanh hoặc thuộc thẩm quyền
                                quyết định trực tiếp của Trưởng phòng.
                              </li>
                              <li>
                                Trưởng phòng nắm rõ nghiệp vụ và muốn giải quyết
                                dứt điểm ngay mà không cần bàn giao cho nhân
                                viên.
                              </li>
                            </ul>
                          </div>

                          {isAssignedToHead && (
                            <div className="flex items-center justify-between rounded-lg border border-emerald-300 bg-emerald-50/80 px-3 py-2 text-xs dark:border-emerald-800 dark:bg-emerald-950/50">
                              <span className="font-medium text-emerald-800 dark:text-emerald-300">
                                Trạng thái hiện tại:
                              </span>
                              <span className="font-bold text-emerald-700 dark:text-emerald-300">
                                Trưởng phòng đang trực tiếp phụ trách
                              </span>
                            </div>
                          )}
                        </div>

                        <div className="space-y-2.5 pt-2">
                          <Button
                            type="button"
                            onClick={() => void handleHeadSelfTakeOver()}
                            disabled={selfTaking}
                            className="w-full gap-2 bg-emerald-600 text-white hover:bg-emerald-700"
                          >
                            {selfTaking ? (
                              <Loader2 className="size-4 animate-spin" />
                            ) : (
                              <CheckCircle2 className="size-4" />
                            )}
                            <span>
                              Trưởng phòng trực tiếp xử lý & Báo khách hàng
                            </span>
                          </Button>
                        </div>
                      </CardContent>
                    </Card>

                    {/* Cột phải (7/12): Phân công cho nhân viên trong phòng ban */}
                    <Card className="border-blue-500/30 bg-blue-500/5 shadow-xs lg:col-span-7">
                      <CardHeader className="border-b border-blue-200/50 pb-3 dark:border-blue-900/50">
                        <div className="flex items-center gap-2.5">
                          <div className="flex size-9 items-center justify-center rounded-lg bg-blue-600/10 text-blue-600 dark:text-blue-400">
                            <UserPlus className="size-5" />
                          </div>
                          <div>
                            <CardTitle className="text-base font-bold text-blue-950 dark:text-blue-200">
                              Phương án 2: Phân công nhân viên phòng ban xử lý
                            </CardTitle>
                            <CardDescription className="text-xs">
                              Giao yêu cầu kèm độ ưu tiên cho nhân viên thuộc{" "}
                              {ticket.department}
                            </CardDescription>
                          </div>
                        </div>
                      </CardHeader>
                      <CardContent className="space-y-4 pt-4 text-sm">
                        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
                          <div className="space-y-2">
                            <Label className="text-xs font-semibold">
                              Nhân sự phụ trách trong phòng ban:
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
                                      ? "Đang tải danh sách nhân sự..."
                                      : "Chọn nhân sự phụ trách"
                                  }
                                />
                              </SelectTrigger>
                              <SelectContent>
                                {(staffMembersOnly.length > 0
                                  ? departmentStaff
                                  : departmentStaff
                                ).map((staff) => (
                                  <SelectItem
                                    key={staff.employeeId}
                                    value={String(staff.employeeId)}
                                  >
                                    {staff.isManager
                                      ? `⭐ [Trưởng phòng] ${staff.employeeName}`
                                      : `${staff.employeeName}`}{" "}
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
                        </div>

                        <div className="space-y-2">
                          <Label className="text-xs font-semibold">
                            Ghi chú chỉ đạo xử lý:
                          </Label>
                          <Textarea
                            value={assignNote}
                            onChange={(e) => setAssignNote(e.target.value)}
                            placeholder="Nhập hướng dẫn hoặc yêu cầu xử lý chi tiết cho nhân viên..."
                            className="min-h-[100px] resize-y bg-background text-sm"
                            maxLength={500}
                          />
                        </div>

                        {ticket.assignedEmployeeName && (
                          <div className="flex items-center justify-between rounded-lg border border-blue-200 bg-background/80 px-3 py-2 text-xs">
                            <span className="text-muted-foreground">
                              Nhân sự đang được giao phụ trách:
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
                            className="gap-2 bg-blue-600 text-white hover:bg-blue-700"
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
                  </div>
                </div>
              )}

              {/* =========================================================
                  TRANG 2: XỬ LÝ & BÁO KẾT QUẢ CHO KHÁCH HÀNG
                  (Dành cho Trưởng phòng trực tiếp xử lý / duyệt báo cáo nhân viên,
                   hoặc Nhân viên phòng ban gửi báo cáo hoàn thành lên Trưởng phòng)
                 ========================================================= */}
              {activeTab === "resolve" && (
                <div className="space-y-6">
                  {renderCustomerRequestCard()}

                  {/* GIAO DIỆN DÀNH CHO TRƯỞNG PHÒNG: TRỰC TIẾP XỬ LÝ HOẶC DUYỆT BÁO CÁO & GỬI KẾT QUẢ CHO KHÁCH HÀNG */}
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
                                Trưởng phòng có thể trực tiếp nhập kết quả xử lý
                                hoặc duyệt báo cáo từ nhân viên để gửi thông báo
                                hoàn tất cho khách hàng
                              </CardDescription>
                            </div>
                          </div>

                          {ticket.assignedEmployeeName && (
                            <Badge
                              variant="outline"
                              className="border-emerald-300 bg-background text-xs font-semibold text-emerald-700 dark:border-emerald-800 dark:text-emerald-300"
                            >
                              Người xử lý: {ticket.assignedEmployeeName}
                            </Badge>
                          )}
                        </div>
                      </CardHeader>

                      <CardContent className="space-y-4 pt-4 text-sm">
                        {/* Hiển thị báo cáo từ nhân viên nếu có */}
                        {ticket.ticketResolution && (
                          <div className="rounded-xl border border-purple-200 bg-purple-50/90 p-4 text-xs text-purple-950 dark:border-purple-800 dark:bg-purple-950/40 dark:text-purple-200">
                            <div className="flex flex-wrap items-center justify-between gap-2">
                              <p className="font-bold">
                                📋 Báo cáo kết quả xử lý nội bộ{" "}
                                {ticket.assignedEmployeeName
                                  ? `từ ${ticket.assignedEmployeeName}`
                                  : ""}
                                :
                              </p>
                              <button
                                type="button"
                                onClick={() =>
                                  setHeadResolution(
                                    ticket.ticketResolution || "",
                                  )
                                }
                                className="rounded-md border border-purple-300 bg-white px-2.5 py-1 text-[11px] font-semibold text-purple-700 hover:bg-purple-100 dark:border-purple-700 dark:bg-slate-900 dark:text-purple-300"
                              >
                                Sử dụng nguyên văn báo cáo này
                              </button>
                            </div>
                            <p className="mt-2 whitespace-pre-wrap leading-relaxed">
                              {ticket.ticketResolution}
                            </p>
                          </div>
                        )}

                        {/* Gợi ý mẫu phản hồi kết quả nhanh khi Trưởng phòng trực tiếp xử lý */}
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

                        <div className="space-y-2">
                          <Label className="text-xs font-semibold">
                            Nội dung kết quả chính thức gửi tới Khách hàng:
                          </Label>
                          <Textarea
                            value={headResolution}
                            onChange={(e) => setHeadResolution(e.target.value)}
                            placeholder="Nhập phương án / kết quả giải quyết chính thức để thông báo cho khách hàng và đóng yêu cầu hỗ trợ..."
                            className="min-h-[150px] resize-y bg-background text-sm leading-relaxed"
                            maxLength={2000}
                          />
                        </div>

                        <div className="flex flex-wrap items-center justify-between gap-3 pt-1">
                          <Button
                            type="button"
                            variant="outline"
                            onClick={() => setActiveTab("assign")}
                            className="gap-1.5 text-xs"
                          >
                            <UserPlus className="size-3.5" />
                            <span>Chuyển sang trang Phân công nhân viên</span>
                          </Button>

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
                      </CardContent>
                    </Card>
                  )}

                  {/* GIAO DIỆN DÀNH CHO NHÂN VIÊN ĐƯỢC PHÂN CÔNG (STAFF) */}
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
                            className="gap-2 bg-purple-600 text-white hover:bg-purple-700"
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
                  TRANG 3: NHẮN TIN TRỰC TUYẾN VỚI KHÁCH HÀNG (KIỂU ZALO REALTIME + GỬI ẢNH)
                  (Chỉ dành cho Trưởng phòng)
                 ========================================================= */}
              {activeTab === "chat" && isDepartmentHead && (
                <div className="flex h-[650px] flex-col overflow-hidden rounded-xl border border-slate-200 bg-card shadow-sm dark:border-slate-800">
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
                          Kênh trao đổi trực tiếp Trưởng phòng ↔ Khách hàng (Hỗ
                          trợ gửi ảnh & phóng to kiểu Zalo)
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
                      const msgImages = getMessageImages(msg);
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

                            {msg.ticketMessageContent && (
                              <p className="whitespace-pre-line text-sm leading-relaxed">
                                {msg.ticketMessageContent}
                              </p>
                            )}

                            {/* Hiển thị trực tiếp hình ảnh trong bong bóng chat kiểu Zalo */}
                            {msgImages.length > 0 && (
                              <div
                                className={cn(
                                  "mt-2 grid gap-2",
                                  msgImages.length === 1
                                    ? "grid-cols-1"
                                    : "grid-cols-2",
                                )}
                              >
                                {msgImages.map((imgUrl, idx) => (
                                  <button
                                    key={idx}
                                    type="button"
                                    onClick={() => {
                                      setLightboxZoom(1);
                                      setLightboxImage({
                                        url: imgUrl,
                                        title:
                                          msg.attachmentNames?.[idx] ||
                                          msg.ticketMessageAttachmentName ||
                                          `Ảnh trao đổi #${idx + 1}`,
                                      });
                                    }}
                                    className="group relative overflow-hidden rounded-xl border border-black/10 bg-black/5 text-left"
                                  >
                                    <img
                                      src={imgUrl}
                                      alt="Hình ảnh đính kèm"
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

                  {/* Phản hồi mẫu nhanh, xem trước ảnh & Ô nhập tin nhắn kiểu Zalo */}
                  <form
                    onSubmit={handleSendReply}
                    className="space-y-2.5 border-t bg-white p-3.5 dark:bg-slate-900"
                  >
                    {/* Xem trước ảnh sắp gửi */}
                    {pendingChatImages.length > 0 && (
                      <div className="flex flex-wrap items-center gap-2.5 rounded-xl border border-blue-200 bg-blue-50/60 p-2.5 dark:border-slate-700 dark:bg-slate-800/70">
                        {pendingChatImages.map((img, idx) => (
                          <div
                            key={idx}
                            className="group relative size-16 overflow-hidden rounded-lg border border-slate-300 bg-white shadow-xs"
                          >
                            <img
                              src={img.dataUrl}
                              alt={img.name}
                              onClick={() => {
                                setLightboxZoom(1);
                                setLightboxImage({
                                  url: img.dataUrl,
                                  title: img.name,
                                });
                              }}
                              className="size-full cursor-pointer object-cover"
                            />
                            <button
                              type="button"
                              onClick={() => removePendingChatImage(idx)}
                              className="absolute top-1 right-1 flex size-5 items-center justify-center rounded-full bg-rose-600 text-white shadow-xs hover:bg-rose-700"
                              title="Xóa ảnh"
                            >
                              <X className="size-3" />
                            </button>
                          </div>
                        ))}
                        <span className="text-xs text-muted-foreground">
                          Đã chọn {pendingChatImages.length}/3 ảnh (Nhấn vào ảnh
                          để xem lớn)
                        </span>
                      </div>
                    )}

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
                        ref={chatImageInputRef}
                        type="file"
                        accept="image/png,image/jpeg,image/jpg,image/webp,image/gif"
                        multiple
                        onChange={handleChatImageInputChange}
                        className="hidden"
                      />
                      <button
                        type="button"
                        onClick={() => chatImageInputRef.current?.click()}
                        title="Gửi hình ảnh minh họa (chỉ nhận file ảnh)"
                        className="flex size-10 shrink-0 items-center justify-center rounded-full border border-slate-300 bg-slate-50 text-slate-600 transition-colors hover:border-blue-500 hover:bg-blue-50 hover:text-blue-600 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-300"
                      >
                        <ImagePlus className="size-5" />
                      </button>

                      <input
                        type="text"
                        value={replyMessage}
                        onChange={(e) => setReplyMessage(e.target.value)}
                        onPaste={handlePasteChatImage}
                        placeholder="Nhập tin nhắn hoặc dán ảnh (Ctrl+V) để gửi cho khách hàng..."
                        className="h-10 flex-1 rounded-full border border-slate-300 bg-slate-50 px-4 text-sm focus:border-blue-500 focus:bg-white focus:outline-none dark:border-slate-700 dark:bg-slate-800"
                      />
                      <Button
                        type="submit"
                        disabled={
                          sendingReply ||
                          (!replyMessage.trim() &&
                            pendingChatImages.length === 0)
                        }
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
                  TRANG 4: THÔNG TIN KHÁCH HÀNG & LỊCH SỬ ĐIỀU PHỐI PHÒNG BAN
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
                          <span>Người trực tiếp xử lý:</span>
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
                                    : "Người xử lý"}
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

      {/* Modal phóng to xem ảnh kiểu Zalo Lightbox */}
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
                  setLightboxZoom((z) => Math.max(0.5, Number((z - 0.25).toFixed(2))))
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
                  setLightboxZoom((z) => Math.min(3, Number((z + 0.25).toFixed(2))))
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

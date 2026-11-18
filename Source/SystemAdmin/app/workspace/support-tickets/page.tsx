"use client";

import { Suspense, useCallback, useEffect, useMemo, useState } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { ColumnDef, Row } from "@tanstack/react-table";
import {
  Building2,
  CalendarDays,
  CheckCircle2,
  Clock,
  Copy,
  Eye,
  Headset,
  Loader2,
  MessageSquare,
  RefreshCw,
} from "lucide-react";
import { toast } from "@/components/ui/sonner";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Checkbox } from "@/components/ui/checkbox";
import { DataTable } from "@/components/ui/custom/data-table";
import { SelectFilter } from "@/components/ui/custom/select-filter";
import { SplitActionMenu } from "@/components/ui/custom/split-action-menu";
import {
  SummaryCardItem,
  SummaryCards,
} from "@/components/ui/custom/summary-cards";
import { cn } from "@/lib/utils";
import { useTicketUser } from "@/components/internal/ticket-user";
import {
  ticketApi,
  TicketDto,
  TicketPriority,
  TicketStatus,
} from "@/services/internal-ticket-api";

const STATUS_FILTER_OPTIONS = [
  { label: "Tất cả trạng thái", value: "All" },
  { label: "Chờ tiếp nhận", value: TicketStatus.Open },
  { label: "Đang xử lý", value: TicketStatus.InProgress },
  { label: "Chờ duyệt kết quả", value: TicketStatus.PendingHeadApproval },
  { label: "Đã xử lý", value: TicketStatus.Resolved },
];

const PRIORITY_FILTER_OPTIONS = [
  { label: "Tất cả độ ưu tiên", value: "All" },
  { label: "Khẩn cấp", value: TicketPriority.Urgent },
  { label: "Cao", value: TicketPriority.High },
  { label: "Bình thường", value: TicketPriority.Medium },
  { label: "Thấp", value: TicketPriority.Low },
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
    [TicketStatus.PendingHeadApproval]: {
      label: "Chờ duyệt kết quả",
      className:
        "bg-violet-50 text-violet-700 border-violet-200 dark:bg-violet-500/10 dark:text-violet-400 dark:border-violet-500/20",
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

function TicketPriorityBadge({ priority }: { priority: TicketPriority }) {
  const config: Record<TicketPriority, { label: string; className: string }> = {
    [TicketPriority.Urgent]: {
      label: "Khẩn cấp",
      className:
        "bg-rose-50 text-rose-700 border-rose-200 dark:bg-rose-500/10 dark:text-rose-400 dark:border-rose-500/20",
    },
    [TicketPriority.High]: {
      label: "Cao",
      className:
        "bg-amber-50 text-amber-700 border-amber-200 dark:bg-amber-500/10 dark:text-amber-400 dark:border-amber-500/20",
    },
    [TicketPriority.Medium]: {
      label: "Bình thường",
      className: "bg-primary/10 text-primary border-primary/20",
    },
    [TicketPriority.Low]: {
      label: "Thấp",
      className: "bg-muted text-muted-foreground border-border",
    },
  };
  const current = config[priority] ?? config[TicketPriority.Medium];
  return (
    <Badge variant="outline" className={cn("font-medium", current.className)}>
      {current.label}
    </Badge>
  );
}

function SupportTicketsContent() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const ticketIdParam = searchParams.get("ticketId");
  const user = useTicketUser();

  const [tickets, setTickets] = useState<TicketDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadingId, setLoadingId] = useState<number | null>(null);
  const [filterStatus, setFilterStatus] = useState("All");
  const [filterPriority, setFilterPriority] = useState("All");

  const isSysAdmin =
    user?.supportRole === "SYSADMIN" ||
    user?.username?.toLowerCase() === "sysadmin" ||
    user?.username?.toLowerCase() === "admin";

  useEffect(() => {
    if (isSysAdmin) {
      router.replace("/workspace");
    }
  }, [isSysAdmin, router]);

  useEffect(() => {
    if (!ticketIdParam) return;
    const id = Number(ticketIdParam);
    if (Number.isFinite(id) && id > 0) {
      router.replace(`/workspace/support-tickets/${id}`);
    }
  }, [ticketIdParam, router]);

  const loadTickets = useCallback(async () => {
    if (isSysAdmin) {
      setTickets([]);
      setLoading(false);
      return;
    }
    setLoading(true);
    try {
      const data = await ticketApi.getAll({
        status: filterStatus,
        priority: filterPriority,
      });
      setTickets(data);
    } catch {
      toast.error("Không thể tải danh sách ticket hỗ trợ");
    } finally {
      setLoading(false);
    }
  }, [filterStatus, filterPriority, isSysAdmin]);

  useEffect(() => {
    void loadTickets();
  }, [loadTickets]);

  useEffect(() => {
    const handleRealtimeUpdate = () => {
      void loadTickets();
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
  }, [loadTickets]);

  const handleCopy = (text: string, label: string) => {
    navigator.clipboard.writeText(text);
    toast.success(`Đã sao chép ${label}: ${text}`);
  };

  const handleView = useCallback(
    (id: number) => {
      setLoadingId(id);
      const tabParam = searchParams.get("tab");
      router.push(
        tabParam
          ? `/workspace/support-tickets/${id}?tab=${encodeURIComponent(tabParam)}`
          : `/workspace/support-tickets/${id}`,
      );
    },
    [router, searchParams],
  );

  // SummaryCards đồng bộ với TenantsPage
  const summaryItems: SummaryCardItem[] = useMemo(() => {
    const total = tickets.length;
    const openCount = tickets.filter(
      (t) => t.ticketStatus === TicketStatus.Open,
    ).length;
    const inProgressCount = tickets.filter(
      (t) => t.ticketStatus === TicketStatus.InProgress,
    ).length;
    const resolvedCount = tickets.filter(
      (t) => t.ticketStatus === TicketStatus.Resolved,
    ).length;

    return [
      {
        title: "Tổng yêu cầu",
        value: total,
        icon: <Headset className="w-6 h-6" />,
        iconWrapperClassName: "bg-primary/10 text-primary",
      },
      {
        title: "Chờ tiếp nhận",
        value: openCount,
        icon: <Clock className="w-6 h-6" />,
        iconWrapperClassName:
          "bg-amber-500/10 text-amber-600 dark:text-amber-500",
      },
      {
        title: "Đang xử lý",
        value: inProgressCount,
        icon: <Loader2 className="w-6 h-6" />,
        iconWrapperClassName: "bg-blue-500/10 text-blue-600 dark:text-blue-500",
      },
      {
        title: "Đã xử lý",
        value: resolvedCount,
        icon: <CheckCircle2 className="w-6 h-6" />,
        iconWrapperClassName:
          "bg-emerald-500/10 text-emerald-600 dark:text-emerald-500",
      },
    ];
  }, [tickets]);

  const columns: ColumnDef<TicketDto>[] = useMemo(
    () => [
      {
        id: "select",
        header: ({ table }) => (
          <div className="flex justify-center">
            <Checkbox
              checked={
                table.getIsAllPageRowsSelected() ||
                (table.getIsSomePageRowsSelected() && "indeterminate")
              }
              onCheckedChange={(value) =>
                table.toggleAllPageRowsSelected(!!value)
              }
              aria-label="Select all"
              className="translate-y-[2px]"
            />
          </div>
        ),
        cell: ({ row }) => (
          <div
            className="flex justify-center"
            onClick={(e) => e.stopPropagation()}
          >
            <Checkbox
              checked={row.getIsSelected()}
              onCheckedChange={(value) => row.toggleSelected(!!value)}
              aria-label="Select row"
              className="translate-y-[2px]"
            />
          </div>
        ),
        enableSorting: false,
      },
      {
        id: "ticketInfo",
        accessorFn: (row) =>
          `${row.ticketCode} ${row.ticketSubject} ${row.tenantName} ${row.tenantCode} ${row.department}`,
        header: "Tổ chức / Tenant",
        cell: ({ row }) => {
          const item = row.original;
          return (
            <div className="max-w-[280px] flex items-center gap-3">
              <div className="w-9 h-9 rounded-full bg-primary/10 flex items-center justify-center shrink-0 text-primary font-bold text-sm">
                {item.tenantName ? (
                  item.tenantName.charAt(0).toUpperCase()
                ) : (
                  <Building2 className="w-4.5 h-4.5" />
                )}
              </div>
              <div className="min-w-0">
                <span className="font-medium text-foreground block truncate">
                  {item.tenantName}
                </span>
                <div className="flex items-center gap-1.5 mt-0.5">
                  <span className="text-xs font-mono text-muted-foreground bg-muted/80 px-1.5 py-0.2 rounded border border-border/50 truncate">
                    {item.tenantCode}
                  </span>
                  <span className="text-xs text-muted-foreground truncate">
                    • {item.department}
                  </span>
                </div>
              </div>
            </div>
          );
        },
      },
      {
        accessorKey: "ticketCode",
        header: "Mã Ticket & Thời gian",
        cell: ({ row }) => {
          const item = row.original;
          const d = new Date(item.createdAt);
          return (
            <div className="flex flex-col">
              <div className="flex items-center gap-1.5">
                <span className="font-mono text-xs font-semibold text-foreground">
                  {item.ticketCode}
                </span>
                <button
                  type="button"
                  onClick={(e) => {
                    e.stopPropagation();
                    handleCopy(item.ticketCode, "mã ticket");
                  }}
                  className="text-muted-foreground hover:text-foreground transition-colors p-0.5 rounded hover:bg-muted"
                  title="Sao chép mã ticket"
                >
                  <Copy className="w-3 h-3" />
                </button>
              </div>
              <span className="mt-1 flex items-center gap-1 text-xs text-muted-foreground">
                <CalendarDays className="w-3 h-3" />
                {d.toLocaleDateString("vi-VN", {
                  day: "2-digit",
                  month: "2-digit",
                  year: "numeric",
                  hour: "2-digit",
                  minute: "2-digit",
                })}
              </span>
            </div>
          );
        },
      },
      {
        accessorKey: "ticketSubject",
        header: "Tiêu đề & Nội dung",
        cell: ({ row }) => {
          const item = row.original;
          return (
            <div className="max-w-[340px]">
              <div className="font-medium text-foreground truncate">
                {item.ticketSubject}
              </div>
              <div className="text-xs text-muted-foreground line-clamp-1 mt-0.5">
                {item.ticketDescription}
              </div>
            </div>
          );
        },
      },
      {
        accessorKey: "serviceName",
        header: "Dịch vụ & Phụ trách",
        cell: ({ row }) => {
          const item = row.original;
          const svc = item.serviceName || item.module || "Dịch vụ chung";
          return (
            <div className="flex flex-col max-w-[240px]">
              <span className="font-semibold text-foreground text-xs truncate">
                {svc}
              </span>
              <span className="text-xs text-muted-foreground truncate mt-0.5">
                {item.assignedEmployeeName
                  ? `NV: ${item.assignedEmployeeName}`
                  : "Chưa phân công NV"}
              </span>
            </div>
          );
        },
      },
      {
        accessorKey: "ticketPriority",
        header: () => <div className="text-center">Độ ưu tiên</div>,
        cell: ({ row }) => (
          <div className="flex justify-center min-w-[100px]">
            <TicketPriorityBadge priority={row.original.ticketPriority} />
          </div>
        ),
      },
      {
        accessorKey: "ticketStatus",
        header: () => <div className="text-center">Trạng thái</div>,
        cell: ({ row }) => (
          <div className="flex justify-center min-w-[120px]">
            <TicketStatusBadge status={row.original.ticketStatus} />
          </div>
        ),
      },
      {
        id: "action",
        header: () => <div className="text-right pr-4">Thao tác</div>,
        cell: ({ row }) => {
          const item = row.original;
          return (
            <SplitActionMenu
              primaryLabel="Chi tiết"
              primaryIcon={<Eye className="w-4 h-4" />}
              onPrimaryClick={() => handleView(item.ticketSupportId)}
              isLoading={loadingId === item.ticketSupportId}
              menuItems={[
                {
                  label: "Xử lý & Phản hồi",
                  icon: <MessageSquare className="w-4 h-4" />,
                  onClick: () => handleView(item.ticketSupportId),
                },
                {
                  label: "Sao chép mã Ticket",
                  icon: <Copy className="w-4 h-4" />,
                  onClick: () => handleCopy(item.ticketCode, "mã ticket"),
                },
                {
                  label: "Sao chép mã Tenant",
                  icon: <Building2 className="w-4 h-4" />,
                  onClick: () => handleCopy(item.tenantCode, "mã tenant"),
                },
              ]}
            />
          );
        },
      },
    ],
    [handleView, loadingId],
  );

  const CustomFilters = (
    <>
      <SelectFilter
        value={filterStatus}
        onChange={(val) => setFilterStatus(val)}
        options={STATUS_FILTER_OPTIONS}
        placeholder="Trạng thái"
      />
      <SelectFilter
        value={filterPriority}
        onChange={(val) => setFilterPriority(val)}
        options={PRIORITY_FILTER_OPTIONS}
        placeholder="Độ ưu tiên xử lý"
      />
    </>
  );

  return (
    <>

      <div className="grow overflow-y-auto p-2 lg:p-10 space-y-6">
        <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
          <div>
            <h1 className="text-2xl font-bold tracking-tight text-foreground">
              {user?.supportRole === "DEPARTMENT_HEAD"
                ? `Yêu cầu hỗ trợ — ${user?.departmentName || "Phòng ban phụ trách"}`
                : "Yêu cầu hỗ trợ được phân công"}
            </h1>
            <p className="text-sm text-muted-foreground mt-1">
              {user?.supportRole === "DEPARTMENT_HEAD"
                ? `Danh sách các yêu cầu hỗ trợ do khách hàng gửi đến ${user?.departmentName || "phòng ban của bạn"}. Phân công nhân viên trong phòng xử lý và duyệt kết quả.`
                : "Danh sách các yêu cầu hỗ trợ do Trưởng phòng phân công cho bạn trực tiếp tiếp nhận và xử lý."}
            </p>
          </div>

          <div className="flex items-center gap-2">
            <Button
              variant="outline"
              size="sm"
              onClick={() => void loadTickets()}
              disabled={loading}
              className="h-9 gap-1.5"
            >
              <RefreshCw className={cn("w-4 h-4", loading && "animate-spin")} />
              <span>Làm mới</span>
            </Button>
          </div>
        </div>

        <SummaryCards items={summaryItems} isLoading={loading} />

        <Card className="border-border shadow-sm bg-card min-h-[500px] flex flex-col gap-0 p-0">
          <CardContent className="p-4 flex flex-col justify-between flex-1 pb-0">
            <DataTable
              columns={columns}
              data={tickets}
              searchKey="ticketInfo"
              searchPlaceholder="Tìm kiếm mã ticket, tiêu đề, tên hoặc mã tenant..."
              filterSlot={CustomFilters}
              isLoading={loading}
              onRowClick={(row) => handleView(row.ticketSupportId)}
              mobileCardRenderer={(row: Row<TicketDto>, { isSelected }) => {
                const item = row.original;
                return (
                  <div
                    className={`rounded-xl border bg-card p-4 shadow-sm transition-colors active:bg-secondary/40 ${
                      isSelected
                        ? "border-primary/40 bg-primary/5"
                        : "border-border"
                    }`}
                  >
                    <div className="flex items-start justify-between gap-3">
                      <div className="min-w-0">
                        <span className="font-mono text-xs font-semibold text-primary">
                          {item.ticketCode}
                        </span>
                        <p className="font-medium text-foreground truncate mt-0.5">
                          {item.ticketSubject}
                        </p>
                        <p className="text-xs text-muted-foreground truncate mt-0.5">
                          {item.tenantName} ({item.tenantCode})
                        </p>
                      </div>
                      <TicketStatusBadge status={item.ticketStatus} />
                    </div>
                  </div>
                );
              }}
            />
          </CardContent>
        </Card>
      </div>
    </>
  );
}

export default function SupportTicketsPage() {
  return (
    <Suspense
      fallback={
        <div className="p-8 text-sm text-muted-foreground">Đang tải...</div>
      }
    >
      <SupportTicketsContent />
    </Suspense>
  );
}

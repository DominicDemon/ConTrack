"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import { useRouter } from "next/navigation";
import { ColumnDef, Row } from "@tanstack/react-table";
import {
  CalendarDays,
  CheckCircle2,
  Clock,
  Copy,
  Eye,
  Headset,
  Loader2,
  MessageSquare,
  Plus,
  RefreshCw,
} from "lucide-react";
import { toast } from "@/components/ui/sonner";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Checkbox } from "@/components/ui/checkbox";
import { Header } from "@/components/ui/custom/header";
import { DataTable } from "@/components/ui/custom/data-table";
import { SelectFilter } from "@/components/ui/custom/select-filter";
import { SplitActionMenu } from "@/components/ui/custom/split-action-menu";
import {
  SummaryCardItem,
  SummaryCards,
} from "@/components/ui/custom/summary-cards";
import { cn } from "@/lib/utils";
import {
  customerTicketApi,
  TicketDto,
  TicketPriority,
  TicketStatus,
} from "@/services/ticket-api";

const STATUS_FILTER_OPTIONS = [
  { label: "Tất cả trạng thái", value: "All" },
  { label: "Chờ tiếp nhận", value: TicketStatus.Open },
  { label: "Đang xử lý", value: TicketStatus.InProgress },
  { label: "Đã xử lý", value: TicketStatus.Resolved },
];

const PRIORITY_FILTER_OPTIONS = [
  { label: "Tất cả ưu tiên", value: "All" },
  { label: "Thấp", value: TicketPriority.Low },
  { label: "Bình thường", value: TicketPriority.Medium },
  { label: "Cao", value: TicketPriority.High },
  { label: "Khẩn cấp", value: TicketPriority.Urgent },
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

export default function CustomerSupportTicketsPage() {
  const router = useRouter();
  const [tickets, setTickets] = useState<TicketDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadingId, setLoadingId] = useState<number | null>(null);
  const [filterStatus, setFilterStatus] = useState("All");
  const [filterPriority, setFilterPriority] = useState("All");

  const loadTickets = useCallback(async () => {
    setLoading(true);
    try {
      const data = await customerTicketApi.getAll({
        status: filterStatus,
        priority: filterPriority,
      });
      setTickets(data);
    } catch {
      toast.error("Không thể tải danh sách yêu cầu hỗ trợ");
    } finally {
      setLoading(false);
    }
  }, [filterStatus, filterPriority]);

  useEffect(() => {
    void loadTickets();
  }, [loadTickets]);

  const handleCopy = (text: string, label: string) => {
    navigator.clipboard.writeText(text);
    toast.success(`Đã sao chép ${label}: ${text}`);
  };

  const handleView = useCallback(
    (id: number) => {
      setLoadingId(id);
      router.push(`/support-tickets/${id}`);
    },
    [router],
  );

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
        iconWrapperClassName: "bg-sky-500/10 text-sky-600 dark:text-sky-500",
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
        accessorKey: "ticketCode",
        header: "Mã Ticket & Ngày gửi",
        cell: ({ row }) => {
          const item = row.original;
          const d = new Date(item.createdAt);
          return (
            <div className="flex flex-col pl-1">
              <div className="flex items-center gap-1.5">
                <span className="font-semibold text-foreground font-mono">
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
        id: "searchInfo",
        accessorFn: (row) =>
          `${row.ticketCode} ${row.ticketSubject} ${row.ticketDescription} ${row.department}`,
        header: "Tiêu đề & Nội dung",
        cell: ({ row }) => {
          const item = row.original;
          return (
            <div className="max-w-[360px]">
              <p className="font-medium text-foreground truncate">
                {item.ticketSubject}
              </p>
              <p className="mt-1 text-xs text-muted-foreground truncate">
                {item.ticketDescription}
              </p>
            </div>
          );
        },
      },
      {
        accessorKey: "department",
        header: "Phòng ban & Phân hệ",
        cell: ({ row }) => {
          const item = row.original;
          return (
            <div className="flex flex-col">
              <span className="font-medium text-foreground text-sm">
                {item.department}
              </span>
              <span className="text-xs text-muted-foreground truncate max-w-[200px]">
                {item.module || "Hỗ trợ chung"}
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
                  label: "Phản hồi yêu cầu",
                  icon: <MessageSquare className="w-4 h-4" />,
                  onClick: () => handleView(item.ticketSupportId),
                },
                {
                  label: "Sao chép mã Ticket",
                  icon: <Copy className="w-4 h-4" />,
                  onClick: () => handleCopy(item.ticketCode, "mã ticket"),
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
        placeholder="Mức độ ưu tiên"
      />
    </>
  );

  return (
    <>
      <Header />

      <div className="grow overflow-y-auto p-2 lg:p-10 space-y-6">
        <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
          <div>
            <h1 className="text-2xl font-bold tracking-tight text-foreground">
              Hỗ trợ khách hàng
            </h1>
            <p className="text-sm text-muted-foreground mt-1">
              Gửi yêu cầu hỗ trợ kỹ thuật, theo dõi tiến độ xử lý và trao đổi
              trực tiếp với bộ phận hỗ trợ ConTrack.
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
            <Button
              onClick={() => router.push("/support-tickets/create")}
              className="h-9 shadow-sm"
            >
              <Plus className="w-4 h-4 mr-2" /> Tạo yêu cầu mới
            </Button>
          </div>
        </div>

        <SummaryCards items={summaryItems} isLoading={loading} />

        <Card className="border-border shadow-sm bg-card min-h-[500px] flex flex-col gap-0 p-0">
          <CardContent className="p-4 flex flex-col justify-between flex-1 pb-0">
            <DataTable
              columns={columns}
              data={tickets}
              isLoading={loading}
              searchKey="searchInfo"
              searchPlaceholder="Tìm theo mã ticket, tiêu đề, phòng ban..."
              filterSlot={CustomFilters}
              onRowClick={(row) => handleView(row.ticketSupportId)}
              mobileCardRenderer={(
                row: Row<TicketDto>,
                { isSelected, actionCell },
              ) => {
                const item = row.original;
                return (
                  <div
                    className={`rounded-xl border bg-card p-4 shadow-sm transition-colors active:bg-secondary/40 ${
                      isSelected
                        ? "border-primary/40 bg-primary/5"
                        : "border-border"
                    }`}
                  >
                    <div className="flex items-start justify-between gap-3 mb-3">
                      <div className="flex flex-col min-w-0">
                        <span className="font-semibold text-foreground font-mono truncate">
                          {item.ticketCode}
                        </span>
                        <span className="text-sm font-medium text-foreground truncate mt-0.5">
                          {item.ticketSubject}
                        </span>
                      </div>
                      <TicketStatusBadge status={item.ticketStatus} />
                    </div>

                    <div className="flex items-center justify-between gap-3 pt-3 border-t border-border">
                      <span className="text-xs text-muted-foreground truncate">
                        {item.department} •{" "}
                        {new Date(item.createdAt).toLocaleDateString("vi-VN")}
                      </span>
                      <TicketPriorityBadge priority={item.ticketPriority} />
                    </div>
                    {actionCell}
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

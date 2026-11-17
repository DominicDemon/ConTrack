"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import { useRouter } from "next/navigation";
import { ColumnDef } from "@tanstack/react-table";
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
  TicketStatus,
} from "@/services/ticket-api";

const STATUS_FILTER_OPTIONS = [
  { label: "Tất cả yêu cầu", value: "All" },
  { label: "Chờ xử lý", value: TicketStatus.Open },
  { label: "Đang xử lý", value: TicketStatus.InProgress },
  { label: "Chờ phản hồi", value: TicketStatus.PendingHeadApproval },
  { label: "Đã xử lý / Đóng", value: TicketStatus.Resolved },
];

function TicketStatusBadge({ status }: { status: TicketStatus }) {
  const config: Record<TicketStatus, { label: string; className: string }> = {
    [TicketStatus.Open]: {
      label: "Chờ xử lý",
      className:
        "bg-amber-50 text-amber-700 border-amber-200 dark:bg-amber-500/10 dark:text-amber-400 dark:border-amber-500/20",
    },
    [TicketStatus.InProgress]: {
      label: "Đang xử lý",
      className:
        "bg-sky-50 text-sky-700 border-sky-200 dark:bg-sky-500/10 dark:text-sky-400 dark:border-sky-500/20",
    },
    [TicketStatus.PendingHeadApproval]: {
      label: "Chờ phản hồi",
      className:
        "bg-violet-50 text-violet-700 border-violet-200 dark:bg-violet-500/10 dark:text-violet-400 dark:border-violet-500/20",
    },
    [TicketStatus.Resolved]: {
      label: "Đã đóng",
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

export default function CustomerSupportTicketsPage() {
  const router = useRouter();
  const [tickets, setTickets] = useState<TicketDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadingId, setLoadingId] = useState<number | null>(null);
  const [filterStatus, setFilterStatus] = useState("All");

  const loadTickets = useCallback(async () => {
    setLoading(true);
    try {
      const data = await customerTicketApi.getAll({
        status: filterStatus,
      });
      setTickets(data);
    } catch {
      toast.error("Không thể tải danh sách yêu cầu hỗ trợ");
    } finally {
      setLoading(false);
    }
  }, [filterStatus]);

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
      (t) =>
        t.ticketStatus === TicketStatus.InProgress ||
        t.ticketStatus === TicketStatus.PendingHeadApproval,
    ).length;
    const resolvedCount = tickets.filter(
      (t) => t.ticketStatus === TicketStatus.Resolved,
    ).length;

    return [
      {
        title: "Tổng yêu cầu của bạn",
        value: total,
        icon: <Headset className="w-6 h-6" />,
        iconWrapperClassName: "bg-primary/10 text-primary",
      },
      {
        title: "Chờ xử lý",
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
        title: "Đã xử lý / Đóng",
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
        header: "Mã yêu cầu & Thời gian",
        cell: ({ row }) => {
          const item = row.original;
          const d = new Date(item.createdAt);
          return (
            <div className="flex flex-col pl-1">
              <div className="flex items-center gap-1.5">
                <span className="font-semibold text-primary font-mono hover:underline">
                  {item.ticketCode}
                </span>
                <button
                  type="button"
                  onClick={(e) => {
                    e.stopPropagation();
                    handleCopy(item.ticketCode, "mã yêu cầu");
                  }}
                  className="text-muted-foreground hover:text-foreground transition-colors p-0.5 rounded hover:bg-muted"
                  title="Sao chép mã yêu cầu"
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
          `${row.ticketCode} ${row.ticketSubject} ${row.ticketDescription} ${row.department} ${row.serviceName || row.module || ""}`,
        header: "Tiêu đề yêu cầu",
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
        header: "Gửi đến (Phòng ban)",
        cell: ({ row }) => {
          const item = row.original;
          return (
            <span className="font-medium text-foreground text-sm">
              {item.department}
            </span>
          );
        },
      },
      {
        accessorKey: "serviceName",
        header: "Dịch vụ",
        cell: ({ row }) => {
          const item = row.original;
          const serviceLabel =
            item.serviceName || item.module || "Dịch vụ chung";
          return (
            <span className="font-semibold text-foreground text-sm">
              {serviceLabel}
            </span>
          );
        },
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
                  label: "Trao đổi trực tuyến",
                  icon: <MessageSquare className="w-4 h-4" />,
                  onClick: () => handleView(item.ticketSupportId),
                },
                {
                  label: "Sao chép mã yêu cầu",
                  icon: <Copy className="w-4 h-4" />,
                  onClick: () => handleCopy(item.ticketCode, "mã yêu cầu"),
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
    <SelectFilter
      value={filterStatus}
      onChange={(val) => setFilterStatus(val)}
      options={STATUS_FILTER_OPTIONS}
      placeholder="Trạng thái"
    />
  );

  return (
    <>
      <Header />

      <div className="grow overflow-y-auto p-2 lg:p-10 space-y-6">
        <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
          <div>
            <h1 className="text-2xl font-bold tracking-tight text-foreground">
              Danh sách yêu cầu hỗ trợ
            </h1>
            <p className="text-sm text-muted-foreground mt-1">
              Gửi yêu cầu hỗ trợ theo từng dịch vụ, theo dõi tiến độ xử lý và
              trao đổi trực tiếp với bộ phận hỗ trợ ConTrack.
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
              className="h-9 gap-1.5 shadow-sm"
            >
              <Plus className="w-4 h-4" />
              <span>Gửi yêu cầu hỗ trợ</span>
            </Button>
          </div>
        </div>

        <SummaryCards items={summaryItems} isLoading={loading} />

        <Card className="border-border shadow-sm overflow-hidden">
          <CardContent className="p-0">
            <DataTable
              columns={columns}
              data={tickets}
              isLoading={loading}
              searchKey="searchInfo"
              searchPlaceholder="Tìm theo mã yêu cầu, tiêu đề, dịch vụ, phòng ban..."
              filterSlot={CustomFilters}
              onRowClick={(row) => handleView(row.ticketSupportId)}
            />
          </CardContent>
        </Card>
      </div>
    </>
  );
}

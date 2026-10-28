"use client";

import { useRef, useState } from "react";
import { useRouter } from "next/navigation";
import {
  ArrowLeft,
  BellRing,
  CheckCircle2,
  CreditCard,
  FileText,
  Headset,
  HelpCircle,
  Loader2,
  Mail,
  Paperclip,
  PhoneCall,
  Send,
  ShoppingBag,
  Trash2,
  Upload,
  Wrench,
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
import { Input } from "@/components/ui/input";
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
import { cn } from "@/lib/utils";
import { customerTicketApi, TicketPriority } from "@/services/ticket-api";

const SUPPORT_MODULES = [
  "Quản lý Hợp đồng & Phụ lục",
  "Ký số điện tử & OTP",
  "Quản lý Báo giá (Quotation)",
  "Hồ sơ pháp lý & Phân quyền",
  "Gia hạn gói cước & Hóa đơn",
  "Khác / Yêu cầu chung",
];

const DEPARTMENTS = [
  {
    code: "KỸ THUẬT",
    title: "Phòng Kỹ thuật - Triển khai",
    desc: "Xử lý lỗi phần mềm, ký số điện tử, tích hợp hệ thống",
    icon: Wrench,
  },
  {
    code: "KẾ TOÁN - HÀNH CHÍNH",
    title: "Phòng Kế toán - Hành chính",
    desc: "Đối soát thanh toán, công nợ, xuất hóa đơn VAT điện tử",
    icon: CreditCard,
  },
  {
    code: "BÁN HÀNG",
    title: "Phòng Kinh doanh & CSKH",
    desc: "Tư vấn nâng cấp gói cước, mua thêm tài khoản nhân viên",
    icon: ShoppingBag,
  },
  {
    code: "GÓP Ý",
    title: "Hòm thư Góp ý dịch vụ",
    desc: "Đề xuất tính năng mới, phản ánh chất lượng phục vụ",
    icon: HelpCircle,
  },
];

const PRIORITIES = [
  {
    value: TicketPriority.Low,
    label: "Thấp",
    desc: "Không ảnh hưởng vận hành",
  },
  {
    value: TicketPriority.Medium,
    label: "Bình thường",
    desc: "Cần hỗ trợ trong ngày",
  },
  {
    value: TicketPriority.High,
    label: "Cao",
    desc: "Ảnh hưởng tiến độ ký kết",
  },
  {
    value: TicketPriority.Urgent,
    label: "Khẩn cấp",
    desc: "Ngừng trệ toàn bộ hệ thống",
  },
];

export default function CreateSupportTicketPage() {
  const router = useRouter();
  const fileInputRef = useRef<HTMLInputElement | null>(null);

  const [selectedModule, setSelectedModule] = useState(SUPPORT_MODULES[0]);
  const [selectedDepartment, setSelectedDepartment] = useState("KỸ THUẬT");
  const [selectedPriority, setSelectedPriority] = useState<TicketPriority>(
    TicketPriority.Medium,
  );
  const [subject, setSubject] = useState("");
  const [description, setDescription] = useState("");
  const [attachmentNames, setAttachmentNames] = useState<string[]>([]);
  const [submitting, setSubmitting] = useState(false);

  const handleSelectFiles = (e: React.ChangeEvent<HTMLInputElement>) => {
    const files = e.target.files;
    if (!files || files.length === 0) return;
    const names = Array.from(files).map((f) => f.name);
    setAttachmentNames((prev) => Array.from(new Set([...prev, ...names])));
    e.target.value = "";
  };

  const handleSubmit = async (e?: React.FormEvent) => {
    e?.preventDefault();
    if (!subject.trim()) {
      toast.error("Vui lòng nhập tiêu đề yêu cầu hỗ trợ");
      return;
    }
    if (!description.trim()) {
      toast.error("Vui lòng nhập nội dung mô tả chi tiết sự cố");
      return;
    }

    setSubmitting(true);
    try {
      const created = await customerTicketApi.create({
        subject: subject.trim(),
        description: description.trim(),
        priority: selectedPriority,
        department: selectedDepartment,
        module: selectedModule,
        attachmentNames,
      });

      toast.success(
        `Đã gửi yêu cầu hỗ trợ ${created.ticketCode} thành công.`,
      );
      router.push(`/support-tickets/${created.ticketSupportId}`);
    } catch {
      toast.error("Không thể gửi yêu cầu hỗ trợ. Vui lòng thử lại.");
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <>
      <Header />

      <div className="grow overflow-y-auto p-2 lg:p-10 space-y-6">
        {/* Page Header chuẩn giống trang Hợp đồng / Hồ sơ pháp lý */}
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
              <h1 className="text-2xl font-bold tracking-tight text-foreground">
                Tạo yêu cầu hỗ trợ mới
              </h1>
            </div>
            <p className="mt-1 max-w-3xl text-sm text-muted-foreground">
              Điền thông tin sự cố hoặc yêu cầu nghiệp vụ bên dưới để gửi tới
              bộ phận xử lý hỗ trợ ConTrack.
            </p>
          </div>

          <div className="flex flex-wrap items-center gap-2">
            <Button
              type="button"
              variant="outline"
              className="h-9"
              onClick={() => router.push("/support-tickets")}
              disabled={submitting}
            >
              Hủy bỏ
            </Button>
            <Button
              type="button"
              className="h-9 gap-1.5 shadow-sm"
              onClick={() => void handleSubmit()}
              disabled={submitting}
            >
              {submitting ? (
                <Loader2 className="size-4 animate-spin" />
              ) : (
                <Send className="size-4" />
              )}
              <span>Gửi yêu cầu hỗ trợ</span>
            </Button>
          </div>
        </div>

        {/* Main Grid 12 cột chuẩn */}
        <form
          onSubmit={handleSubmit}
          className="grid grid-cols-1 items-start gap-6 lg:grid-cols-12"
        >
          {/* Cột trái (8/12): Structured Form Cards */}
          <div className="space-y-6 lg:col-span-7 xl:col-span-8">
            {/* Card 1: Phân loại & Điều phối */}
            <Card className="border-border shadow-sm">
              <CardHeader className="border-b pb-4">
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-2.5">
                    <div className="flex size-9 items-center justify-center rounded-lg bg-primary/10 text-primary">
                      <Headset className="size-5" />
                    </div>
                    <div>
                      <CardTitle className="text-base font-semibold">
                        Phân loại & Phòng ban tiếp nhận
                      </CardTitle>
                      <CardDescription className="text-xs">
                        Chọn đúng phân hệ và phòng ban để yêu cầu được chuyển
                        thẳng tới chuyên viên phụ trách
                      </CardDescription>
                    </div>
                  </div>
                  <Badge variant="outline" className="text-[11px] font-normal">
                    Bước 1
                  </Badge>
                </div>
              </CardHeader>
              <CardContent className="space-y-5 pt-5">
                <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
                  <div className="space-y-1.5">
                    <Label className="text-xs font-medium">
                      Phân hệ gặp sự cố <span className="text-destructive">*</span>
                    </Label>
                    <Select
                      value={selectedModule}
                      onValueChange={setSelectedModule}
                    >
                      <SelectTrigger className="h-10 bg-background">
                        <SelectValue placeholder="Chọn phân hệ" />
                      </SelectTrigger>
                      <SelectContent>
                        {SUPPORT_MODULES.map((mod) => (
                          <SelectItem key={mod} value={mod}>
                            {mod}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </div>

                  <div className="space-y-1.5">
                    <Label className="text-xs font-medium">
                      Mức độ ưu tiên <span className="text-destructive">*</span>
                    </Label>
                    <Select
                      value={selectedPriority}
                      onValueChange={(val) =>
                        setSelectedPriority(val as TicketPriority)
                      }
                    >
                      <SelectTrigger className="h-10 bg-background">
                        <SelectValue placeholder="Chọn độ ưu tiên" />
                      </SelectTrigger>
                      <SelectContent>
                        {PRIORITIES.map((p) => (
                          <SelectItem key={p.value} value={p.value}>
                            {p.label} — {p.desc}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </div>
                </div>

                <div className="space-y-2">
                  <Label className="text-xs font-medium">
                    Phòng ban tiếp nhận xử lý{" "}
                    <span className="text-destructive">*</span>
                  </Label>
                  <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
                    {DEPARTMENTS.map((dept) => {
                      const Icon = dept.icon;
                      const active = selectedDepartment === dept.code;
                      return (
                        <button
                          key={dept.code}
                          type="button"
                          onClick={() => setSelectedDepartment(dept.code)}
                          className={cn(
                            "flex items-start gap-3 rounded-xl border p-3.5 text-left transition-colors",
                            active
                              ? "border-primary bg-primary/5 ring-1 ring-primary"
                              : "border-border bg-card hover:bg-secondary/40",
                          )}
                        >
                          <div
                            className={cn(
                              "mt-0.5 flex size-9 shrink-0 items-center justify-center rounded-lg",
                              active
                                ? "bg-primary text-primary-foreground"
                                : "bg-secondary text-muted-foreground",
                            )}
                          >
                            <Icon className="size-4" />
                          </div>
                          <div className="min-w-0">
                            <p className="text-sm font-semibold text-foreground">
                              {dept.title}
                            </p>
                            <p className="mt-0.5 text-xs text-muted-foreground line-clamp-2">
                              {dept.desc}
                            </p>
                          </div>
                        </button>
                      );
                    })}
                  </div>
                </div>
              </CardContent>
            </Card>

            {/* Card 2: Nội dung chi tiết & Đính kèm */}
            <Card className="border-border shadow-sm">
              <CardHeader className="border-b pb-4">
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-2.5">
                    <div className="flex size-9 items-center justify-center rounded-lg bg-sky-500/10 text-sky-600 dark:text-sky-400">
                      <FileText className="size-5" />
                    </div>
                    <div>
                      <CardTitle className="text-base font-semibold">
                        Nội dung yêu cầu & Tài liệu đính kèm
                      </CardTitle>
                      <CardDescription className="text-xs">
                        Mô tả chi tiết tình huống gặp phải và đính kèm ảnh chụp
                        màn hình (nếu có)
                      </CardDescription>
                    </div>
                  </div>
                  <Badge variant="outline" className="text-[11px] font-normal">
                    Bước 2
                  </Badge>
                </div>
              </CardHeader>
              <CardContent className="space-y-4 pt-5">
                <div className="space-y-1.5">
                  <Label htmlFor="ticket-subject" className="text-xs font-medium">
                    Tiêu đề yêu cầu hỗ trợ{" "}
                    <span className="text-destructive">*</span>
                  </Label>
                  <Input
                    id="ticket-subject"
                    value={subject}
                    onChange={(e) => setSubject(e.target.value)}
                    placeholder="Ví dụ: Không xuất được file PDF hợp đồng sau khi ký số..."
                    className="h-10"
                  />
                </div>

                <div className="space-y-1.5">
                  <div className="flex items-center justify-between">
                    <Label
                      htmlFor="ticket-description"
                      className="text-xs font-medium"
                    >
                      Nội dung mô tả chi tiết{" "}
                      <span className="text-destructive">*</span>
                    </Label>
                    <span className="text-[11px] font-mono text-muted-foreground">
                      {description.length}/2000
                    </span>
                  </div>
                  <Textarea
                    id="ticket-description"
                    value={description}
                    onChange={(e) => setDescription(e.target.value)}
                    rows={5}
                    maxLength={2000}
                    placeholder="Mô tả rõ mã hợp đồng, bước thao tác gặp lỗi hoặc yêu cầu cụ thể để chuyên viên hỗ trợ xử lý nhanh nhất..."
                    className="resize-y text-sm"
                  />
                </div>

                <div className="space-y-3 pt-2 border-t border-border">
                  <div className="flex flex-wrap items-center justify-between gap-2">
                    <div>
                      <Label className="text-xs font-medium block">
                        Tệp đính kèm / Ảnh chụp màn hình (Tùy chọn)
                      </Label>
                      <p className="text-xs text-muted-foreground mt-0.5">
                        Hỗ trợ đính kèm hình ảnh (.png, .jpg) hoặc tài liệu
                        (.pdf, .docx)
                      </p>
                    </div>
                    <input
                      ref={fileInputRef}
                      type="file"
                      multiple
                      className="hidden"
                      onChange={handleSelectFiles}
                    />
                    <Button
                      type="button"
                      variant="outline"
                      size="sm"
                      className="h-9 text-xs gap-1.5"
                      onClick={() => fileInputRef.current?.click()}
                    >
                      <Upload className="size-3.5" />
                      <span>Chọn tệp đính kèm</span>
                    </Button>
                  </div>

                  {attachmentNames.length > 0 && (
                    <div className="flex flex-wrap gap-2">
                      {attachmentNames.map((name) => (
                        <div
                          key={name}
                          className="inline-flex items-center gap-2 rounded-lg border border-border bg-secondary/50 px-3 py-1.5 text-xs"
                        >
                          <Paperclip className="size-3.5 text-primary" />
                          <span className="max-w-[240px] truncate font-medium text-foreground">
                            {name}
                          </span>
                          <button
                            type="button"
                            onClick={() =>
                              setAttachmentNames((prev) =>
                                prev.filter((item) => item !== name),
                              )
                            }
                            className="text-muted-foreground hover:text-destructive"
                          >
                            <Trash2 className="size-3.5" />
                          </button>
                        </div>
                      ))}
                    </div>
                  )}
                </div>
              </CardContent>
            </Card>
          </div>

          {/* Cột phải (4/12): Thông tin điều phối & Quy trình xử lý */}
          <div className="space-y-6 lg:col-span-5 xl:col-span-4">
            <Card className="border-border shadow-sm">
              <CardHeader className="border-b pb-4">
                <CardTitle className="text-base font-semibold">
                  Quy trình tiếp nhận & Thông báo
                </CardTitle>
                <CardDescription className="text-xs">
                  Đồng bộ tự động tới bộ phận xử lý hỗ trợ
                </CardDescription>
              </CardHeader>
              <CardContent className="space-y-4 pt-5 text-xs">
                <div className="rounded-xl border bg-muted/30 p-3.5 space-y-1.5">
                  <div className="flex items-center gap-2 font-semibold text-foreground">
                    <BellRing className="size-4 text-primary" />
                    <span>Thông báo trên Web người xử lý</span>
                  </div>
                  <p className="text-muted-foreground leading-relaxed">
                    Hệ thống tự động đẩy thông báo nổi và cập nhật chuông thông
                    báo trên trang quản trị của người xử lý hỗ trợ.
                  </p>
                </div>

                <div className="rounded-xl border bg-muted/30 p-3.5 space-y-1.5">
                  <div className="flex items-center gap-2 font-semibold text-foreground">
                    <Mail className="size-4 text-emerald-600" />
                    <span>Thông báo qua Email người xử lý</span>
                  </div>
                  <p className="text-muted-foreground leading-relaxed">
                    Email thông báo kèm nội dung chi tiết và liên kết xử lý
                    nhanh sẽ được gửi thẳng về hòm thư của chuyên viên hỗ trợ.
                  </p>
                </div>

                <div className="rounded-xl border bg-muted/30 p-3.5 space-y-1.5">
                  <div className="flex items-center gap-2 font-semibold text-foreground">
                    <CheckCircle2 className="size-4 text-sky-600" />
                    <span>Đồng bộ trên Web & App Mobile</span>
                  </div>
                  <p className="text-muted-foreground leading-relaxed">
                    Mọi phản hồi từ chuyên viên ConTrack được cập nhật tức thì
                    trên cả Web và ứng dụng di động.
                  </p>
                </div>

                <div className="rounded-xl border border-primary/20 bg-primary/5 p-3.5 flex items-center justify-between">
                  <div className="flex items-center gap-2">
                    <PhoneCall className="size-4 text-primary" />
                    <span className="font-medium text-foreground">
                      Tổng đài trực 24/7:
                    </span>
                  </div>
                  <a
                    href="tel:19006868"
                    className="font-bold text-primary hover:underline"
                  >
                    1900 6868
                  </a>
                </div>
              </CardContent>
            </Card>
          </div>
        </form>
      </div>
    </>
  );
}

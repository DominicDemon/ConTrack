"use client";

import { useEffect, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import {
  ArrowLeft,
  BellRing,
  CheckCircle2,
  CreditCard,
  FileText,
  Headset,
  HelpCircle,
  ImagePlus,
  Loader2,
  Mail,
  PhoneCall,
  Send,
  ShoppingBag,
  Trash2,
  Wrench,
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
import {
  customerTicketApi,
  SupportServiceItemDto,
  TicketPriority,
} from "@/services/ticket-api";

const DEPARTMENTS = [
  {
    id: 2,
    code: "Phòng Kỹ Thuật & Hỗ Trợ",
    title: "Phòng Kỹ Thuật & Hỗ Trợ",
    desc: "Hướng dẫn & xử lý sự cố phần mềm, ký số điện tử 24/7/365",
    icon: Wrench,
  },
  {
    id: 5,
    code: "Phòng Hành Chính - Pháp Chế",
    title: "Dịch vụ khách hàng / Pháp chế",
    desc: "Chính sách dịch vụ, tài khoản, hồ sơ pháp lý hợp đồng",
    icon: HelpCircle,
  },
  {
    id: 3,
    code: "Phòng Kinh Doanh",
    title: "Phòng Kinh Doanh (Bán Hàng)",
    desc: "Tư vấn dịch vụ, báo giá, đăng ký và gia hạn gói cước",
    icon: ShoppingBag,
  },
  {
    id: 4,
    code: "Phòng Kế Toán - Tài Chính",
    title: "Phòng Kế Toán - Tài Chính",
    desc: "Đối soát thanh toán, công nợ, xuất hóa đơn VAT điện tử",
    icon: CreditCard,
  },
];

interface AttachedImageItem {
  name: string;
  dataUrl: string;
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

export default function CreateSupportTicketPage() {
  const router = useRouter();
  const fileInputRef = useRef<HTMLInputElement | null>(null);

  const [services, setServices] = useState<SupportServiceItemDto[]>([]);
  const [selectedServiceId, setSelectedServiceId] = useState<string>("");
  const [selectedDepartment, setSelectedDepartment] = useState(
    DEPARTMENTS[0].code,
  );
  const [subject, setSubject] = useState("");
  const [description, setDescription] = useState("");
  const [images, setImages] = useState<AttachedImageItem[]>([]);
  const [previewImage, setPreviewImage] = useState<string | null>(null);
  const [zoomScale, setZoomScale] = useState(1);
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    let active = true;
    customerTicketApi.getServices().then((list) => {
      if (!active) return;
      setServices(list);
      if (list.length > 0) {
        setSelectedServiceId(String(list[0].serviceId));
      }
    });
    return () => {
      active = false;
    };
  }, []);

  const processImageFiles = async (fileList: File[]) => {
    const validImages = fileList.filter((f) => f.type.startsWith("image/"));
    if (validImages.length < fileList.length) {
      toast.warning(
        "Chỉ hỗ trợ gửi hình ảnh (PNG, JPG, JPEG, WEBP, GIF), không hỗ trợ gửi tệp tài liệu khác.",
      );
    }
    if (validImages.length === 0) return;

    const loaded: AttachedImageItem[] = [];
    for (const file of validImages) {
      try {
        const dataUrl = await readImageAsDataUrl(file);
        loaded.push({ name: file.name, dataUrl });
      } catch {
        toast.error(`Không thể đọc ảnh ${file.name}`);
      }
    }

    setImages((prev) => {
      const merged = [...prev, ...loaded];
      if (merged.length > 3) {
        toast.warning("Chỉ được đính kèm tối đa 3 hình ảnh minh họa.");
        return merged.slice(0, 3);
      }
      return merged;
    });
  };

  const handleSelectFiles = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const files = e.target.files;
    if (!files || files.length === 0) return;
    await processImageFiles(Array.from(files));
    e.target.value = "";
  };

  const handlePasteImages = async (
    e: React.ClipboardEvent<HTMLTextAreaElement>,
  ) => {
    const items = e.clipboardData?.items;
    if (!items) return;
    const pastedFiles: File[] = [];
    for (let i = 0; i < items.length; i++) {
      if (items[i].type.startsWith("image/")) {
        const f = items[i].getAsFile();
        if (f) pastedFiles.push(f);
      }
    }
    if (pastedFiles.length > 0) {
      await processImageFiles(pastedFiles);
      toast.success("Đã dán hình ảnh từ bộ nhớ tạm!");
    }
  };

  const handleSubmit = async (e?: React.FormEvent) => {
    e?.preventDefault();
    if (!selectedServiceId) {
      toast.error("Vui lòng chọn dịch vụ cần hỗ trợ");
      return;
    }
    if (!subject.trim()) {
      toast.error("Vui lòng nhập tiêu đề yêu cầu hỗ trợ");
      return;
    }
    if (!description.trim()) {
      toast.error("Vui lòng nhập nội dung câu hỏi / mô tả chi tiết");
      return;
    }

    const deptObj =
      DEPARTMENTS.find((d) => d.code === selectedDepartment) ?? DEPARTMENTS[0];
    const svcObj = services.find(
      (s) => String(s.serviceId) === selectedServiceId,
    );

    setSubmitting(true);
    try {
      const created = await customerTicketApi.create({
        subject: subject.trim(),
        description: description.trim(),
        priority: TicketPriority.Medium,
        departmentId: deptObj.id,
        department: deptObj.code,
        serviceId: svcObj ? svcObj.serviceId : Number(selectedServiceId),
        serviceName: svcObj?.serviceName ?? "Dịch vụ chung",
        module: svcObj?.serviceName ?? "Dịch vụ chung",
        attachmentNames: images.map((img) => img.name),
        attachmentUrls: images.map((img) => img.dataUrl),
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
        {/* Page Header đồng bộ với các trang khác */}
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
                Gửi yêu cầu hỗ trợ
              </h1>
            </div>
            <p className="mt-1 max-w-3xl text-sm text-muted-foreground">
              Vui lòng chọn chính xác Dịch vụ và Phòng ban tiếp nhận để được bộ
              phận chuyên môn hỗ trợ nhanh nhất.
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

        {/* Main Grid 12 cột chuẩn của hệ thống */}
        <form
          onSubmit={handleSubmit}
          className="grid grid-cols-1 items-start gap-6 lg:grid-cols-12"
        >
          {/* Cột trái (8/12): Form Cards */}
          <div className="space-y-6 lg:col-span-7 xl:col-span-8">
            {/* Card 1: Chọn Dịch vụ & Phòng ban tiếp nhận */}
            <Card className="border-border shadow-sm">
              <CardHeader className="border-b pb-4">
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-2.5">
                    <div className="flex size-9 items-center justify-center rounded-lg bg-primary/10 text-primary">
                      <Headset className="size-5" />
                    </div>
                    <div>
                      <CardTitle className="text-base font-semibold">
                        Dịch vụ & Phòng ban tiếp nhận
                      </CardTitle>
                      <CardDescription className="text-xs">
                        Chọn đúng dịch vụ và phòng ban để yêu cầu được chuyển
                        thẳng tới Trưởng bộ phận phụ trách
                      </CardDescription>
                    </div>
                  </div>
                  <Badge variant="outline" className="text-[11px] font-normal">
                    Bước 1
                  </Badge>
                </div>
              </CardHeader>
              <CardContent className="space-y-5 pt-5">
                <div className="space-y-1.5">
                  <Label className="text-xs font-medium">
                    Chọn dịch vụ cần hỗ trợ{" "}
                    <span className="text-destructive">*</span>
                  </Label>
                  <Select
                    value={selectedServiceId}
                    onValueChange={setSelectedServiceId}
                  >
                    <SelectTrigger className="h-10 w-full bg-background">
                      <SelectValue placeholder="Chọn dịch vụ cần hỗ trợ" />
                    </SelectTrigger>
                    <SelectContent>
                      {services.map((svc) => (
                        <SelectItem
                          key={svc.serviceId}
                          value={String(svc.serviceId)}
                        >
                          {svc.serviceName}
                          {svc.serviceCode ? ` (${svc.serviceCode})` : ""}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
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

            {/* Card 2: Nội dung câu hỏi & Hình ảnh minh họa */}
            <Card className="border-border shadow-sm">
              <CardHeader className="border-b pb-4">
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-2.5">
                    <div className="flex size-9 items-center justify-center rounded-lg bg-sky-500/10 text-sky-600 dark:text-sky-400">
                      <FileText className="size-5" />
                    </div>
                    <div>
                      <CardTitle className="text-base font-semibold">
                        Nội dung câu hỏi & Hình ảnh minh họa
                      </CardTitle>
                      <CardDescription className="text-xs">
                        Mô tả chi tiết tình huống gặp phải và gửi kèm ảnh chụp
                        màn hình (bấm vào ảnh để phóng to như Zalo)
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
                  <Label
                    htmlFor="ticket-subject"
                    className="text-xs font-medium"
                  >
                    Tiêu đề yêu cầu <span className="text-destructive">*</span>
                  </Label>
                  <Input
                    id="ticket-subject"
                    maxLength={200}
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
                      Nội dung câu hỏi{" "}
                      <span className="text-destructive">*</span>
                    </Label>
                    <span className="text-[11px] font-mono text-muted-foreground">
                      Nội dung không vượt quá 4000 ký tự ({description.length}
                      /4000)
                    </span>
                  </div>
                  <Textarea
                    id="ticket-description"
                    rows={8}
                    maxLength={4000}
                    value={description}
                    onChange={(e) => setDescription(e.target.value)}
                    onPaste={handlePasteImages}
                    placeholder="Mô tả chi tiết câu hỏi hoặc các bước dẫn đến lỗi, mã hợp đồng liên quan... (Bạn cũng có thể nhấn Ctrl+V để dán trực tiếp ảnh chụp màn hình)"
                    className="min-h-[200px] resize-y text-sm leading-relaxed"
                  />
                </div>

                {/* Chỉ gửi Hình ảnh minh họa (Hiển thị ảnh trực tiếp + Bấm vào phóng to như Zalo) */}
                <div className="space-y-3 pt-1">
                  <Label className="text-xs font-medium">
                    Hình ảnh minh họa (*.png, *.jpg, *.jpeg, *.webp, *.gif — Tối
                    đa 3 ảnh)
                  </Label>
                  <input
                    ref={fileInputRef}
                    type="file"
                    multiple
                    accept="image/png,image/jpeg,image/jpg,image/webp,image/gif"
                    className="hidden"
                    onChange={handleSelectFiles}
                  />
                  <div
                    onClick={() => fileInputRef.current?.click()}
                    className="flex cursor-pointer flex-col items-center justify-center rounded-xl border border-dashed border-border bg-secondary/20 px-4 py-5 text-center transition-colors hover:bg-secondary/40"
                  >
                    <ImagePlus className="size-6 text-primary" />
                    <p className="mt-1.5 text-xs font-medium text-foreground">
                      Nhấn để chọn ảnh minh họa ({images.length}/3)
                    </p>
                    <p className="mt-0.5 text-[11px] text-muted-foreground">
                      Chỉ nhận tệp hình ảnh (PNG, JPG, WEBP, GIF) • Bấm vào ảnh
                      đã tải để xem phóng to như Zalo
                    </p>
                  </div>

                  {images.length > 0 && (
                    <div className="grid grid-cols-2 gap-3 pt-1 sm:grid-cols-3">
                      {images.map((img, idx) => (
                        <div
                          key={`${img.name}-${idx}`}
                          className="group relative overflow-hidden rounded-xl border border-border bg-muted/30 shadow-xs"
                        >
                          <button
                            type="button"
                            onClick={() => {
                              setPreviewImage(img.dataUrl);
                              setZoomScale(1);
                            }}
                            className="relative block h-36 w-full cursor-zoom-in overflow-hidden bg-slate-950/5"
                            title="Bấm vào để xem ảnh phóng to"
                          >
                            <img
                              src={img.dataUrl}
                              alt={img.name}
                              className="h-full w-full object-cover transition-transform duration-200 group-hover:scale-105"
                            />
                            <span className="absolute inset-0 flex items-center justify-center bg-black/30 opacity-0 transition-opacity group-hover:opacity-100">
                              <span className="inline-flex items-center gap-1 rounded-full bg-black/75 px-2.5 py-1 text-[11px] font-medium text-white">
                                <ZoomIn className="size-3.5" />
                                Xem ảnh to
                              </span>
                            </span>
                          </button>

                          <button
                            type="button"
                            onClick={() =>
                              setImages((prev) =>
                                prev.filter((_, i) => i !== idx),
                              )
                            }
                            className="absolute top-2 right-2 flex size-7 items-center justify-center rounded-full bg-black/70 text-white transition-colors hover:bg-rose-600"
                            title="Xóa ảnh này"
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

          {/* Cột phải (4/12): Thông tin điều phối & Liên hệ */}
          <div className="space-y-6 lg:col-span-5 xl:col-span-4 lg:sticky lg:top-6">
            <Card className="border-border shadow-sm">
              <CardHeader className="border-b pb-4">
                <div className="flex items-center gap-2.5">
                  <div className="flex size-9 items-center justify-center rounded-lg bg-emerald-500/10 text-emerald-600 dark:text-emerald-400">
                    <BellRing className="size-5" />
                  </div>
                  <div>
                    <CardTitle className="text-base font-semibold">
                      Quy trình tiếp nhận & xử lý
                    </CardTitle>
                    <CardDescription className="text-xs">
                      Yêu cầu được điều phối trực tiếp tới phòng ban chuyên môn
                    </CardDescription>
                  </div>
                </div>
              </CardHeader>
              <CardContent className="space-y-3.5 pt-4 text-xs text-muted-foreground">
                <div className="flex items-start gap-2.5">
                  <CheckCircle2 className="mt-0.5 size-4 shrink-0 text-emerald-600" />
                  <p>
                    <span className="font-semibold text-foreground">
                      1. Gửi đúng Trưởng bộ phận:
                    </span>{" "}
                    Phiếu yêu cầu được chuyển thẳng tới Trưởng phòng của bộ phận
                    bạn đã chọn.
                  </p>
                </div>
                <div className="flex items-start gap-2.5">
                  <CheckCircle2 className="mt-0.5 size-4 shrink-0 text-emerald-600" />
                  <p>
                    <span className="font-semibold text-foreground">
                      2. Xử lý trực tiếp hoặc Phân công:
                    </span>{" "}
                    Trưởng phòng có thể trực tiếp xử lý ngay hoặc phân công kèm
                    độ ưu tiên cho chuyên viên trong phòng.
                  </p>
                </div>
                <div className="flex items-start gap-2.5">
                  <CheckCircle2 className="mt-0.5 size-4 shrink-0 text-emerald-600" />
                  <p>
                    <span className="font-semibold text-foreground">
                      3. Trao đổi & Gửi ảnh như Zalo:
                    </span>{" "}
                    Bạn có thể nhắn tin và gửi ảnh trực tiếp với Trưởng phòng
                    ngay trong trang chi tiết phiếu hỗ trợ.
                  </p>
                </div>
              </CardContent>
            </Card>

            <Card className="border-border shadow-sm">
              <CardHeader className="border-b pb-3">
                <CardTitle className="text-sm font-semibold">
                  Kênh liên hệ trực tiếp
                </CardTitle>
              </CardHeader>
              <CardContent className="space-y-3 pt-4 text-xs">
                <div className="flex items-center gap-2.5 text-muted-foreground">
                  <PhoneCall className="size-4 text-primary" />
                  <span>Hotline kỹ thuật: 1900 6868 (Nhánh 1)</span>
                </div>
                <div className="flex items-center gap-2.5 text-muted-foreground">
                  <Mail className="size-4 text-primary" />
                  <span>Email: support@contrack.vn</span>
                </div>
              </CardContent>
            </Card>
          </div>
        </form>
      </div>

      {/* Modal Xem ảnh phóng to kiểu Zalo (Lightbox) */}
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

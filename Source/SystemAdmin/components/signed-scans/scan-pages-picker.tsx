"use client";

import { useEffect, useId, useMemo, useRef, useState, type ChangeEvent } from "react";
import { ArrowDown, ArrowUp, Camera, Delete, FileText, ImagePlus, X } from "lucide-react";
import { Button } from "@/components/ui/button";
import { formatFileSize } from "./format";
import { ScanPhotoCropper } from "./scan-photo-cropper";

/** Same limit as The API. */
export const MAX_SCAN_BYTES = 20 * 1024 * 1024;
const MAX_PAGES = 30;
const IMAGE_TYPES = ["image/jpeg", "image/png"];

/** Larger photos only slow the cropping down; the PDF is built at a smaller size anyway. */
const MAX_PHOTO_SIDE = 3000;

/**
 * A phone camera often stores the photo sideways with an EXIF flag saying how to turn it, which
 * the OCR ignores. Drawing it once in the browser applies the turn and shrinks a huge photo.
 */
const uprightPhoto = async (file: File): Promise<File> => {
  if (file.type !== "image/jpeg" || typeof createImageBitmap !== "function") return file;
  try {
    const bitmap = await createImageBitmap(file, { imageOrientation: "from-image" });
    const scale = Math.min(1, MAX_PHOTO_SIDE / Math.max(bitmap.width, bitmap.height));
    const canvas = document.createElement("canvas");
    canvas.width = Math.round(bitmap.width * scale);
    canvas.height = Math.round(bitmap.height * scale);
    canvas.getContext("2d")?.drawImage(bitmap, 0, 0, canvas.width, canvas.height);
    bitmap.close();
    const blob = await new Promise<Blob | null>((resolve) => canvas.toBlob(resolve, "image/jpeg", 0.92));
    return blob ? new File([blob], file.name || "anh-chup.jpg", { type: "image/jpeg" }) : file;
  } catch {
    return file;
  }
};

export interface ScanPage {
  id: number;
  photo: File;
  /** Object URL of the photo, for its thumbnail. */
  url: string;
}

/** Either photos taken page by page (joined into one PDF on saving), or one PDF file. */
export type ScanPages = { kind: "photos"; pages: ScanPage[] } | { kind: "pdf"; file: File };

export const pageCountLabel = (value: ScanPages | null) =>
  !value ? "" : value.kind === "pdf" ? "1 tệp PDF" : `${value.pages.length} trang`;

let nextPageId = 1;

interface ScanPagesPickerProps {
  value: ScanPages | null;
  onChange: (value: ScanPages | null) => void;
  disabled?: boolean;
  error?: string;
}

export function ScanPagesPicker({ value, onChange, disabled, error }: ScanPagesPickerProps) {
  const cameraRef = useRef<HTMLInputElement>(null);
  const filesRef = useRef<HTMLInputElement>(null);
  const hintId = useId();
  const [queue, setQueue] = useState<File[]>([]);
  const [problem, setProblem] = useState<string | null>(null);
  const pages = useMemo(() => (value?.kind === "photos" ? value.pages : []), [value]);

  // Thumbnails are freed when their page goes away.
  const urls = useRef(new Set<string>());
  useEffect(() => {
    const kept = new Set(pages.map((page) => page.url));
    for (const url of urls.current) {
      if (!kept.has(url)) {
        URL.revokeObjectURL(url);
        urls.current.delete(url);
      }
    }
  }, [pages]);
  useEffect(() => {
    const all = urls.current;
    return () => all.forEach((url) => URL.revokeObjectURL(url));
  }, []);

  const setPages = (next: ScanPage[]) => onChange(next.length > 0 ? { kind: "photos", pages: next } : null);

  const addPhotos = async (files: File[]) => {
    const room = MAX_PAGES - pages.length;
    if (files.length > room) setProblem(`Mỗi bản quét có tối đa ${MAX_PAGES} trang.`);
    const upright: File[] = [];
    for (const file of files.slice(0, Math.max(0, room))) upright.push(await uprightPhoto(file));
    // Each photo is cropped to its page, one after another.
    setQueue((current) => [...current, ...upright]);
  };

  const onPicked = async (event: ChangeEvent<HTMLInputElement>) => {
    const files = Array.from(event.target.files ?? []);
    // Lets the same file be chosen again after it was removed.
    event.target.value = "";
    if (files.length === 0) return;
    setProblem(null);

    const pdfs = files.filter((file) => file.type === "application/pdf" || file.name.toLowerCase().endsWith(".pdf"));
    const photos = files.filter((file) => IMAGE_TYPES.includes(file.type));
    if (pdfs.length > 0) {
      if (pdfs.length > 1 || photos.length > 0 || pages.length > 0) {
        setProblem("Chọn một tệp PDF, hoặc chụp/chọn ảnh từng trang. Không ghép PDF với ảnh được.");
        return;
      }
      const [pdf] = pdfs;
      if (pdf.size === 0 || pdf.size > MAX_SCAN_BYTES) {
        setProblem("Tệp PDF phải có nội dung và không quá 20 MB.");
        return;
      }
      onChange({ kind: "pdf", file: pdf });
      return;
    }
    if (photos.length < files.length) setProblem("Chỉ nhận ảnh JPG, PNG hoặc một tệp PDF.");
    await addPhotos(photos);
  };

  const cropped = (photo: File) => {
    const url = URL.createObjectURL(photo);
    urls.current.add(url);
    setQueue((current) => current.slice(1));
    setPages([...pages, { id: nextPageId++, photo, url }]);
  };

  const move = (index: number, by: number) => {
    const next = [...pages];
    const [page] = next.splice(index, 1);
    next.splice(index + by, 0, page);
    setPages(next);
  };

  const pdfMode = value?.kind === "pdf";

  return (
    <div className="space-y-3">
      <div className="flex flex-wrap gap-2">
        <Button
          type="button"
          disabled={disabled || pdfMode}
          aria-describedby={hintId}
          onClick={() => cameraRef.current?.click()}
        >
          <Camera className="size-4" />
          {pages.length > 0 ? "Chụp thêm trang" : "Chụp trang"}
        </Button>
        <Button
          type="button"
          variant="outline"
          disabled={disabled || pdfMode}
          aria-describedby={hintId}
          onClick={() => filesRef.current?.click()}
        >
          <ImagePlus className="size-4" />
          Chọn ảnh hoặc tệp PDF
        </Button>
      </div>
      <p id={hintId} className="text-xs text-muted-foreground">
        Chụp lần lượt từng trang (kể cả trang có chữ ký), cắt sát tờ giấy; các trang được ghép thành một tệp PDF khi
        lưu. Hoặc chọn một tệp PDF đã scan sẵn. Tối đa 20 MB.
      </p>
      <input
        ref={cameraRef}
        type="file"
        accept="image/jpeg,image/png,image/*"
        capture="environment"
        className="sr-only"
        tabIndex={-1}
        aria-hidden="true"
        onChange={onPicked}
      />
      <input
        ref={filesRef}
        type="file"
        multiple
        accept=".pdf,.jpg,.jpeg,.png,application/pdf,image/jpeg,image/png"
        className="sr-only"
        tabIndex={-1}
        aria-hidden="true"
        onChange={onPicked}
      />

      {queue.length > 0 && (
        <ScanPhotoCropper
          // A fresh cropper for every photo, so nothing is left over from the previous one.
          key={`${queue[0].name}:${queue[0].size}:${queue[0].lastModified}:${pages.length}`}
          photo={queue[0]}
          onDone={cropped}
          onCancel={() => setQueue((current) => current.slice(1))}
        />
      )}

      {(problem ?? error) && (
        <p role="alert" className="text-sm text-destructive">
          {problem ?? error}
        </p>
      )}

      {value?.kind === "pdf" && (
        <div className="flex items-center justify-between gap-2 rounded-lg border bg-muted/40 p-2 text-sm">
          <span className="flex min-w-0 items-center gap-2">
            <FileText className="size-5 shrink-0 text-muted-foreground" />
            <span className="truncate font-medium text-foreground">
              {value.file.name} · {formatFileSize(value.file.size)}
            </span>
          </span>
          <Button
            type="button"
            variant="ghost"
            size="sm"
            aria-label="Bỏ tệp đã chọn"
            className="size-9 shrink-0 p-0 text-muted-foreground hover:bg-destructive/10 hover:text-destructive"
            disabled={disabled}
            onClick={() => onChange(null)}
          >
            <Delete className="size-4" />
          </Button>
        </div>
      )}

      {pages.length > 0 && (
        <ol className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-4">
          {pages.map((page, index) => (
            <li key={page.id} className="overflow-hidden rounded-lg border bg-card">
              {/* eslint-disable-next-line @next/next/no-img-element -- a local object URL, not a remote image */}
              <img
                src={page.url}
                alt={`Trang ${index + 1}`}
                className="aspect-[3/4] w-full bg-muted object-contain"
              />
              <div className="flex items-center justify-between gap-1 border-t px-2 py-1">
                <span className="text-sm font-medium">Trang {index + 1}</span>
                <span className="flex">
                  <Button
                    type="button"
                    variant="ghost"
                    size="icon"
                    className="size-8"
                    aria-label={`Đưa trang ${index + 1} lên trước`}
                    disabled={disabled || index === 0}
                    onClick={() => move(index, -1)}
                  >
                    <ArrowUp className="size-4" />
                  </Button>
                  <Button
                    type="button"
                    variant="ghost"
                    size="icon"
                    className="size-8"
                    aria-label={`Đưa trang ${index + 1} ra sau`}
                    disabled={disabled || index === pages.length - 1}
                    onClick={() => move(index, 1)}
                  >
                    <ArrowDown className="size-4" />
                  </Button>
                  <Button
                    type="button"
                    variant="ghost"
                    size="icon"
                    className="size-8 text-muted-foreground hover:bg-destructive/10 hover:text-destructive"
                    aria-label={`Bỏ trang ${index + 1}`}
                    disabled={disabled}
                    onClick={() => setPages(pages.filter((other) => other.id !== page.id))}
                  >
                    <X className="size-4" />
                  </Button>
                </span>
              </div>
            </li>
          ))}
        </ol>
      )}
    </div>
  );
}

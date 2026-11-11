"use client";

import { useEffect, useRef, useState, type PointerEvent as ReactPointerEvent } from "react";
import { Loader2, RotateCw } from "lucide-react";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";

/** Fractions of the photo, so the frame keeps its place whatever size the photo is shown at. */
interface Frame {
  x: number;
  y: number;
  w: number;
  h: number;
}

type Grip = "nw" | "ne" | "sw" | "se" | "move";

const MIN_SIDE = 0.1;
const START: Frame = { x: 0.04, y: 0.04, w: 0.92, h: 0.92 };
const GRIPS: { grip: Exclude<Grip, "move">; className: string; label: string }[] = [
  { grip: "nw", className: "-left-3.5 -top-3.5 cursor-nwse-resize", label: "Góc trên bên trái" },
  { grip: "ne", className: "-right-3.5 -top-3.5 cursor-nesw-resize", label: "Góc trên bên phải" },
  { grip: "sw", className: "-bottom-3.5 -left-3.5 cursor-nesw-resize", label: "Góc dưới bên trái" },
  { grip: "se", className: "-bottom-3.5 -right-3.5 cursor-nwse-resize", label: "Góc dưới bên phải" },
];

const clamp = (value: number, min: number, max: number) => Math.min(Math.max(value, min), max);

const loadCanvas = async (file: File) => {
  const bitmap = await createImageBitmap(file, { imageOrientation: "from-image" });
  const canvas = document.createElement("canvas");
  canvas.width = bitmap.width;
  canvas.height = bitmap.height;
  canvas.getContext("2d")?.drawImage(bitmap, 0, 0);
  bitmap.close();
  return canvas;
};

const rotated = (source: HTMLCanvasElement) => {
  const canvas = document.createElement("canvas");
  canvas.width = source.height;
  canvas.height = source.width;
  const context = canvas.getContext("2d");
  context?.translate(canvas.width, 0);
  context?.rotate(Math.PI / 2);
  context?.drawImage(source, 0, 0);
  return canvas;
};

const toFile = (canvas: HTMLCanvasElement, name: string) =>
  new Promise<File | null>((resolve) =>
    canvas.toBlob(
      (blob) => resolve(blob ? new File([blob], name.replace(/\.(png|jpe?g)$/i, "") + ".jpg", { type: "image/jpeg" }) : null),
      "image/jpeg",
      0.92,
    ),
  );

interface ScanPhotoCropperProps {
  /** The photo to crop. Mount one cropper per photo (give it the photo as `key`). */
  photo: File;
  onDone: (file: File) => void;
  onCancel: () => void;
}

interface Loaded {
  canvas: HTMLCanvasElement;
  /** Object URL of a smaller copy, for showing it. */
  preview: string;
}

const withPreview = (canvas: HTMLCanvasElement) =>
  new Promise<Loaded>((resolve, reject) =>
    canvas.toBlob(
      (blob) => (blob ? resolve({ canvas, preview: URL.createObjectURL(blob) }) : reject(new Error("preview"))),
      "image/jpeg",
      0.8,
    ),
  );

/**
 * Lets the user keep only the contract page of a photo: other papers or labels around it would
 * otherwise be read by OCR as keywords, and be stored in the archive copy.
 */
export function ScanPhotoCropper({ photo, onDone, onCancel }: ScanPhotoCropperProps) {
  const [loaded, setLoaded] = useState<Loaded | null>(null);
  const [frame, setFrame] = useState<Frame>(START);
  const [busy, setBusy] = useState(false);
  const areaRef = useRef<HTMLDivElement>(null);
  const dragRef = useRef<{ grip: Grip; startX: number; startY: number; start: Frame } | null>(null);
  // Read through a ref so a new callback on each render does not reload the photo.
  const onDoneRef = useRef(onDone);
  useEffect(() => {
    onDoneRef.current = onDone;
  }, [onDone]);

  useEffect(() => {
    let cancelled = false;
    loadCanvas(photo)
      .then(withPreview)
      .then(
        (next) => (cancelled ? URL.revokeObjectURL(next.preview) : setLoaded(next)),
        // A photo the browser cannot draw is used as it is.
        () => !cancelled && onDoneRef.current(photo),
      );
    return () => {
      cancelled = true;
    };
  }, [photo]);

  useEffect(() => () => {
    if (loaded) URL.revokeObjectURL(loaded.preview);
  }, [loaded]);

  const source = loaded?.canvas ?? null;

  const beginDrag = (grip: Grip, event: ReactPointerEvent<HTMLElement>) => {
    event.preventDefault();
    event.stopPropagation();
    event.currentTarget.setPointerCapture(event.pointerId);
    dragRef.current = { grip, startX: event.clientX, startY: event.clientY, start: frame };
  };

  const drag = (event: ReactPointerEvent<HTMLElement>) => {
    const current = dragRef.current;
    const area = areaRef.current?.getBoundingClientRect();
    if (!current || !area) return;
    const dx = (event.clientX - current.startX) / area.width;
    const dy = (event.clientY - current.startY) / area.height;
    const { x, y, w, h } = current.start;

    if (current.grip === "move") {
      setFrame({ x: clamp(x + dx, 0, 1 - w), y: clamp(y + dy, 0, 1 - h), w, h });
      return;
    }
    let left = x;
    let top = y;
    let right = x + w;
    let bottom = y + h;
    if (current.grip.includes("w")) left = clamp(x + dx, 0, right - MIN_SIDE);
    if (current.grip.includes("e")) right = clamp(x + w + dx, left + MIN_SIDE, 1);
    if (current.grip.includes("n")) top = clamp(y + dy, 0, bottom - MIN_SIDE);
    if (current.grip.includes("s")) bottom = clamp(y + h + dy, top + MIN_SIDE, 1);
    setFrame({ x: left, y: top, w: right - left, h: bottom - top });
  };

  const endDrag = () => {
    dragRef.current = null;
  };

  const rotate = async () => {
    if (!source) return;
    setBusy(true);
    try {
      setLoaded(await withPreview(rotated(source)));
      // The frame turns with the photo.
      setFrame((current) => ({ x: 1 - current.y - current.h, y: current.x, w: current.h, h: current.w }));
    } finally {
      setBusy(false);
    }
  };

  const finish = async (whole: boolean) => {
    if (!source) return;
    setBusy(true);
    try {
      const area = whole ? { x: 0, y: 0, w: 1, h: 1 } : frame;
      const canvas = document.createElement("canvas");
      canvas.width = Math.max(1, Math.round(area.w * source.width));
      canvas.height = Math.max(1, Math.round(area.h * source.height));
      canvas.getContext("2d")?.drawImage(
        source,
        Math.round(area.x * source.width),
        Math.round(area.y * source.height),
        canvas.width,
        canvas.height,
        0,
        0,
        canvas.width,
        canvas.height,
      );
      onDone((await toFile(canvas, photo.name)) ?? photo);
    } finally {
      setBusy(false);
    }
  };

  const ratio = source ? source.width / source.height : 3 / 4;

  return (
    <Dialog open onOpenChange={(open) => !open && onCancel()}>
      <DialogContent className="max-h-[95dvh] gap-3 overflow-y-auto p-4 sm:max-w-xl">
        <DialogHeader>
          <DialogTitle>Cắt ảnh bản giấy</DialogTitle>
          <DialogDescription>
            Kéo các góc để khung chỉ ôm tờ hợp đồng, bỏ phần bàn, giấy khác hay chữ xung quanh.
          </DialogDescription>
        </DialogHeader>

        <div className="flex justify-center">
          {loaded ? (
            <div
              ref={areaRef}
              className="relative touch-none select-none overflow-hidden rounded-md bg-muted"
              style={{ aspectRatio: String(ratio), width: `min(100%, calc(60dvh * ${ratio}))` }}
            >
              {/* eslint-disable-next-line @next/next/no-img-element -- a local object URL, not a remote image */}
              <img src={loaded.preview} alt="Ảnh vừa chụp" className="absolute inset-0 size-full" draggable={false} />
              <div
                role="presentation"
                onPointerDown={(event) => beginDrag("move", event)}
                onPointerMove={drag}
                onPointerUp={endDrag}
                onPointerCancel={endDrag}
                className="absolute cursor-move border-2 border-white shadow-[0_0_0_9999px_rgba(0,0,0,0.55)]"
                style={{
                  left: `${frame.x * 100}%`,
                  top: `${frame.y * 100}%`,
                  width: `${frame.w * 100}%`,
                  height: `${frame.h * 100}%`,
                }}
              >
                {GRIPS.map(({ grip, className, label }) => (
                  <span
                    key={grip}
                    aria-label={label}
                    onPointerDown={(event) => beginDrag(grip, event)}
                    onPointerMove={drag}
                    onPointerUp={endDrag}
                    onPointerCancel={endDrag}
                    className={`absolute flex size-7 items-center justify-center ${className}`}
                  >
                    <span className="size-4 rounded-full border-2 border-primary bg-white shadow" />
                  </span>
                ))}
              </div>
            </div>
          ) : (
            <div className="flex h-60 items-center justify-center text-muted-foreground">
              <Loader2 className="animate-spin" aria-hidden="true" />
              <span className="sr-only">Đang mở ảnh…</span>
            </div>
          )}
        </div>

        <DialogFooter className="flex-row flex-wrap gap-2 sm:justify-between">
          <Button type="button" variant="outline" onClick={rotate} disabled={!source || busy}>
            <RotateCw aria-hidden="true" />
            Xoay
          </Button>
          <div className="flex flex-wrap gap-2">
            <Button type="button" variant="outline" onClick={() => finish(true)} disabled={!source || busy}>
              Dùng cả ảnh
            </Button>
            <Button type="button" onClick={() => finish(false)} disabled={!source || busy}>
              {busy && <Loader2 className="animate-spin" aria-hidden="true" />}
              Cắt và dùng
            </Button>
          </div>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

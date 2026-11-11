"use client";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";

export type AddedContent = { id: string; afterBlockId: string; kind: "clause" | "paragraph" | "image"; title: string; text: string;
  imageBase64?: string; imageMime?: string; widthCm: number; align: "left" | "center" | "right"; order: number };
export default function WordContentEditor({ value, disabled, onChange, onDelete }: {
  value: AddedContent; disabled: boolean; onChange: (value: AddedContent) => void; onDelete: () => void;
}) {
  return <fieldset disabled={disabled} className="my-4 rounded border border-blue-200 p-3">
    <legend>{value.kind === "image" ? "Ảnh mới" : value.kind === "clause" ? "Điều khoản mới" : "Đoạn mới"}</legend>
    {value.kind !== "paragraph" && <label>{value.kind === "image" ? "Chú thích ảnh (không bắt buộc)" : "Tiêu đề điều khoản"}
      <Input aria-label={value.kind === "image" ? "Chú thích ảnh" : "Tiêu đề điều khoản mới"} placeholder={value.kind === "clause" ? "Ví dụ: Điều 7: Trách nhiệm của Bên B" : ""}
        value={value.title} maxLength={500} onChange={e => onChange({...value,title:e.target.value})}/></label>}
    {value.kind === "image" ? <>
      <div className="my-2 flex flex-wrap items-center gap-3">
        <label>Rộng (cm) <input aria-label="Chiều rộng ảnh" type="number" min={1} max={16} step={0.5} value={value.widthCm} className="w-16 rounded border px-1" onChange={e => onChange({...value,widthCm:Math.max(1,Math.min(16,Number(e.target.value)||1))})}/></label>
        <select aria-label="Căn lề ảnh" value={value.align} className="rounded border bg-white px-2 text-black" onChange={e => onChange({...value,align:e.target.value as AddedContent["align"]})}>
          <option value="left">Căn trái</option><option value="center">Căn giữa</option><option value="right">Căn phải</option>
        </select>
        <span>Giữ tỷ lệ ảnh</span>
      </div>
      {/* Uploaded image bytes stay local to this editor and are embedded in the saved Word file. */}
      {/* eslint-disable-next-line @next/next/no-img-element */}
      <img src={`data:${value.imageMime};base64,${value.imageBase64}`} alt={value.title || "Ảnh chèn thêm"} style={{width:`${value.widthCm}cm`,maxWidth:"100%",height:"auto",display:"block",marginLeft:value.align === "left" ? 0 : "auto",marginRight:value.align === "right" ? 0 : "auto"}}/>
    </> : <label>Nội dung<textarea aria-label="Nội dung chèn thêm" rows={4} value={value.text} maxLength={20000} onChange={e => onChange({...value,text:e.target.value})}/></label>}
    <Button type="button" variant="outline" size="sm" onClick={onDelete}>Xóa nội dung mới</Button>
  </fieldset>;
}

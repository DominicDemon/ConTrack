/**
 * Joins page photos into one PDF in the browser, one photo per page, so a contract photographed
 * page by page is stored and read as one document. Each page is a JPEG drawn straight onto the
 * page (PDF reads JPEG as it is), so no library is needed.
 */

/** Text stays sharp for OCR at this size, and ten pages stay well under the 20 MB limit. */
const MAX_SIDE = 2500;
const JPEG_QUALITY = 0.85;
/** The long side of each page, in points: the long side of A4. */
const PAGE_LONG_SIDE = 842;

interface PageImage {
  jpeg: Uint8Array;
  width: number;
  height: number;
}

const toPageImage = async (photo: Blob): Promise<PageImage> => {
  const bitmap = await createImageBitmap(photo, { imageOrientation: "from-image" });
  const scale = Math.min(1, MAX_SIDE / Math.max(bitmap.width, bitmap.height));
  const canvas = document.createElement("canvas");
  canvas.width = Math.max(1, Math.round(bitmap.width * scale));
  canvas.height = Math.max(1, Math.round(bitmap.height * scale));
  const context = canvas.getContext("2d");
  if (!context) throw new Error("Trình duyệt không vẽ được ảnh.");
  // A PNG may be transparent; paper is white.
  context.fillStyle = "#ffffff";
  context.fillRect(0, 0, canvas.width, canvas.height);
  context.drawImage(bitmap, 0, 0, canvas.width, canvas.height);
  bitmap.close();
  const blob = await new Promise<Blob | null>((resolve) => canvas.toBlob(resolve, "image/jpeg", JPEG_QUALITY));
  if (!blob) throw new Error("Trình duyệt không nén được ảnh.");
  return { jpeg: new Uint8Array(await blob.arrayBuffer()), width: canvas.width, height: canvas.height };
};

const encoder = new TextEncoder();

/** One PDF page per photo, in the given order. */
export const imagesToPdf = async (photos: Blob[], fileName: string): Promise<File> => {
  const pages = [];
  for (const photo of photos) pages.push(await toPageImage(photo));

  const chunks: Uint8Array[] = [];
  const offsets: number[] = [];
  let length = 0;
  const push = (part: string | Uint8Array) => {
    const bytes = typeof part === "string" ? encoder.encode(part) : part;
    chunks.push(bytes);
    length += bytes.length;
  };
  const object = (id: number, body: string | Uint8Array[]) => {
    offsets[id] = length;
    push(`${id} 0 obj\n`);
    if (typeof body === "string") push(body);
    else body.forEach(push);
    push("\nendobj\n");
  };

  // Objects: 1 catalog, 2 page tree, then for page i: 3+3i page, 4+3i image, 5+3i drawing.
  const pageId = (index: number) => 3 + index * 3;
  push("%PDF-1.4\n%âãÏÓ\n");
  object(1, "<< /Type /Catalog /Pages 2 0 R >>");
  object(2, `<< /Type /Pages /Count ${pages.length} /Kids [${pages.map((_, i) => `${pageId(i)} 0 R`).join(" ")}] >>`);
  pages.forEach((page, index) => {
    const id = pageId(index);
    const scale = PAGE_LONG_SIDE / Math.max(page.width, page.height);
    const width = (page.width * scale).toFixed(2);
    const height = (page.height * scale).toFixed(2);
    object(
      id,
      `<< /Type /Page /Parent 2 0 R /MediaBox [0 0 ${width} ${height}] ` +
        `/Resources << /XObject << /Im0 ${id + 1} 0 R >> >> /Contents ${id + 2} 0 R >>`,
    );
    object(id + 1, [
      encoder.encode(
        `<< /Type /XObject /Subtype /Image /Width ${page.width} /Height ${page.height} /ColorSpace /DeviceRGB ` +
          `/BitsPerComponent 8 /Filter /DCTDecode /Length ${page.jpeg.length} >>\nstream\n`,
      ),
      page.jpeg,
      encoder.encode("\nendstream"),
    ]);
    const drawing = `q ${width} 0 0 ${height} 0 0 cm /Im0 Do Q`;
    object(id + 2, `<< /Length ${drawing.length} >>\nstream\n${drawing}\nendstream`);
  });

  const count = pageId(pages.length);
  const xref = length;
  push(`xref\n0 ${count}\n0000000000 65535 f \n`);
  for (let id = 1; id < count; id++) push(`${String(offsets[id]).padStart(10, "0")} 00000 n \n`);
  push(`trailer\n<< /Size ${count} /Root 1 0 R >>\nstartxref\n${xref}\n%%EOF\n`);

  return new File(chunks as BlobPart[], fileName, { type: "application/pdf" });
};

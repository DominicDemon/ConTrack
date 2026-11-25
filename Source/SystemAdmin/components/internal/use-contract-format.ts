import type { CSSProperties } from "react";
import type { ContractFormat } from "@/services/internal-api";

// The A4 preview is 794px wide: 1pt = 4/3 px, 1mm = 794/210 px.
const pt = (value: number) => `${(value * 4 / 3).toFixed(2)}px`;
const mm = (value: number) => `${(value * 794 / 210).toFixed(1)}px`;

/** Inline style of the preview paper so it matches the printed PDF; headings read --paper-title and --paper-heading. */
export function paperStyle(format: ContractFormat | null): CSSProperties | undefined {
  if (!format) return undefined;
  return {
    fontFamily: `'${format.fontFamily}', serif`, fontSize: pt(format.bodyFontSize), lineHeight: 1.2 * format.lineSpacing,
    padding: `${mm(format.marginTopMm)} ${mm(format.marginRightMm)} ${mm(format.marginBottomMm)} ${mm(format.marginLeftMm)}`,
    ["--paper-title" as string]: pt(format.titleFontSize), ["--paper-heading" as string]: pt(format.headingFontSize),
    ["--paper-title-case" as string]: format.titleUppercase ? "uppercase" : "none",
  };
}

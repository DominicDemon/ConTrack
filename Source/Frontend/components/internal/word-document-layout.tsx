"use client";
import type { ReactNode } from "react";

export type DocumentBlock = { id: string; location: string; text: string; tableId?: number | null; row?: number | null; cell?: number | null; colSpan?: number; verticalMerge?: string | null; heading?: boolean; images?: {mime:string;base64:string}[] };

export default function WordDocumentLayout({ blocks, renderBlock, afterBlock }: { blocks: DocumentBlock[]; renderBlock?: (block: DocumentBlock) => ReactNode; afterBlock?: (block: DocumentBlock) => ReactNode }) {
  const render = renderBlock ?? ((block: DocumentBlock) => <p className="whitespace-pre-wrap min-h-5">{block.text.replace(/\{\{[A-Z][A-Z0-9_]*\}\}/g, "________________")}</p>);
  const rendered = new Set<number>();
  return <div className="space-y-3">
    {blocks.map(block => {
      if (block.tableId == null) return <div key={block.id}>{render(block)}{afterBlock?.(block)}</div>;
      if (rendered.has(block.tableId)) return null;
      rendered.add(block.tableId);
      const tableBlocks = blocks.filter(b => b.tableId === block.tableId);
      const rowIds = [...new Set(tableBlocks.map(b => b.row!))];
      return <div key={"table-" + block.tableId} className="overflow-x-auto"><table className="w-full border-collapse table-fixed"><tbody>
        {rowIds.map((rowId, rowIndex) => {
          const rowBlocks = tableBlocks.filter(b => b.row === rowId);
          const cellIds = [...new Set(rowBlocks.map(b => b.cell!))];
          return <tr key={rowId}>{cellIds.map(cellId => {
            const cellBlocks = rowBlocks.filter(b => b.cell === cellId);
            const cell = cellBlocks[0];
            if (cell.verticalMerge === "continue") return null;
            let rowSpan = 1;
            if (cell.verticalMerge === "restart") {
              for (const nextRow of rowIds.slice(rowIndex + 1)) {
                if (!tableBlocks.some(b => b.row === nextRow && b.cell === cellId && b.verticalMerge === "continue")) break;
                rowSpan++;
              }
            }
            return <td key={cellId} colSpan={cell.colSpan ?? 1} rowSpan={rowSpan} className="px-2 py-1 align-top break-words">
              {cellBlocks.map(b => <div key={b.id}>{render(b)}</div>)}
            </td>;
          })}</tr>;
        })}
      </tbody></table>{tableBlocks.map(b => <div key={"after-" + b.id}>{afterBlock?.(b)}</div>)}</div>;
    })}
  </div>;
}

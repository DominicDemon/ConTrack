"use client";
import { useState } from "react";
import { Button } from "@/components/ui/button";

export type AddedTable = { id: string; afterBlockId: string; borders: boolean; order?: number; rows: { text: string; colSpan: number }[][] };
const empty = () => ({ text: "", colSpan: 1 });
export function createTable(afterBlockId: string, rows: number, columns: number): AddedTable {
  return { id: crypto.randomUUID(), afterBlockId, borders: true,
    rows: Array.from({ length: rows }, () => Array.from({ length: columns }, empty)) };
}
export default function WordTableEditor({ table, disabled, onChange, onDelete }: {
  table: AddedTable; disabled: boolean; onChange: (value: AddedTable) => void; onDelete: () => void;
}) {
  const [selected, setSelected] = useState<[number, number]>([0, 0]);
  const [r, c] = selected;
  const cell = table.rows[r]?.[c];
  const columns = table.rows[0].reduce((n, item) => n + item.colSpan, 0);
  function change(mutator: (rows: AddedTable["rows"]) => void) {
    const rows = table.rows.map(row => row.map(item => ({ ...item })));
    mutator(rows); onChange({ ...table, rows });
  }
  return <fieldset disabled={disabled} className="my-4 rounded border border-blue-200 p-3" style={{ fontFamily: "sans-serif", fontSize: 13 }}>
    <legend>Bảng mới · {table.rows.length} hàng × {columns} cột</legend>
    <div className="mb-3 flex flex-wrap gap-2">
      <Button type="button" size="sm" variant="outline" disabled={table.rows.length >= 50} onClick={() => {change(rows => rows.splice(r + 1, 0, Array.from({length: columns}, empty))); setSelected([r + 1, 0]);}}>Thêm hàng dưới</Button>
      <Button type="button" size="sm" variant="outline" disabled={table.rows.length <= 1} onClick={() => {change(rows => rows.splice(r, 1)); setSelected([0, 0]);}}>Xóa hàng chọn</Button>
      <Button type="button" size="sm" variant="outline" disabled={columns >= 12} onClick={() => change(rows => rows.forEach(row => row.push(empty())))}>Thêm cột cuối</Button>
      <Button type="button" size="sm" variant="outline" disabled={columns <= 1} onClick={() => {change(rows => rows.forEach(row => {const last = row[row.length - 1]; if (last.colSpan > 1) last.colSpan--; else row.pop();})); setSelected([0, 0]);}}>Xóa cột cuối</Button>
      <Button type="button" size="sm" variant="outline" disabled={!cell || c >= table.rows[r].length - 1} onClick={() => change(rows => {const next = rows[r][c + 1]; rows[r][c].colSpan += next.colSpan; rows[r][c].text = [rows[r][c].text, next.text].filter(Boolean).join("\n"); rows[r].splice(c + 1, 1);})}>Gộp với ô bên phải</Button>
      <Button type="button" size="sm" variant="outline" disabled={!cell || cell.colSpan <= 1} onClick={() => change(rows => {const count = rows[r][c].colSpan; rows[r][c].colSpan = 1; rows[r].splice(c + 1, 0, ...Array.from({length: count - 1}, empty));})}>Tách ô</Button>
      <label><input type="checkbox" checked={table.borders} onChange={e => onChange({...table, borders: e.target.checked})}/> Có viền khi in</label>
      <Button type="button" size="sm" variant="outline" onClick={onDelete}>Xóa bảng mới</Button>
    </div>
    <div className="mb-2 flex flex-wrap items-center gap-2">
      <span>Ô chọn: hàng {r + 1}, ô {c + 1}</span>
    </div>
    <div className="overflow-auto"><table className="w-full border-collapse"><tbody>{table.rows.map((row, ri) => <tr key={ri}>{row.map((item, ci) => <td key={ci} colSpan={item.colSpan} style={{border: table.borders ? "1px solid #64748b" : "1px dashed #d1d5db", padding: 6, background: r === ri && c === ci ? "#eff6ff" : "white"}}>
      <textarea aria-label={`Bảng mới hàng ${ri + 1} ô ${ci + 1}`} value={item.text} maxLength={20000} rows={2}
        onFocus={() => setSelected([ri, ci])}
        onChange={e => change(rows => {rows[ri][ci].text = e.target.value;})}/>
    </td>)}</tr>)}</tbody></table></div>
    {!table.borders && <p>Đường chấm chỉ giúp chọn ô khi chỉnh sửa, không xuất hiện trong bản in.</p>}
  </fieldset>;
}

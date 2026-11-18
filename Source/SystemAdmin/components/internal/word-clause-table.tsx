"use client";
import { useRef, useState, type ReactNode } from "react";
import WordDocumentLayout, { type DocumentBlock } from "./word-document-layout";
import s from "./word-clause-table.module.css";

const partyLabel = /^(địa chỉ|điện thoại|fax|đại diện|người đại diện|chức vụ|mã số thuế|tài khoản(?: số)?|số tài khoản|ngân hàng|email|tại ngân hàng)\s*:?$/iu;

export default function WordClauseTable({ blocks, party, after, select, change, readOnly }: {
  blocks:DocumentBlock[]; party:boolean; after:(b:DocumentBlock)=>ReactNode;
  select:(id:string)=>void; change:(id:string,text:string)=>void; readOnly?:boolean;
}) {
  const labels=useRef(new Map(blocks.filter(b=>partyLabel.test(b.text.trim())&&!b.images?.length).map(b=>[b.id,b.text.trim().replace(/:$/,"")])));
  const [editing,setEditing]=useState("");
  const rows=Array.from(new Set(blocks.map(b=>b.row))).map(row=>blocks.filter(b=>b.row===row));
  const columns=Math.max(...rows.map(row=>Array.from(new Set(row.map(b=>b.cell))).reduce<number>((n,cell)=>n+(row.find(b=>b.cell===cell)?.colSpan??1),0)));
  function field(b:DocumentBlock,label:string,always=false) {
    const image=b.images?.map((img,i)=><img key={i} alt="Ảnh trong ô bảng" src={`data:${img.mime};base64,${img.base64}`}/>);
    if(readOnly) return <>{image}<span className={s.text}>{b.text}</span></>;
    if(always || editing===b.id) return <>{image}<textarea className={s.input} data-word-ids={b.id} aria-label={label} rows={1}
      autoFocus={!always} value={b.text} onFocus={()=>select(b.id)} onBlur={()=>setEditing("")}
      onChange={e=>change(b.id,e.target.value)}/></>;
    return <>{image}<button type="button" className={s.cell} data-word-ids={b.id} aria-label={label}
      onClick={()=>{setEditing(b.id);select(b.id);}}>{b.text || " "}</button></>;
  }
  const probableFields=rows.filter(row=>labels.current.has(row[0].id)).length > rows.length/2;
  const paired=rows.map(row=>{
    const pairs:{label:string;values:DocumentBlock[]}[]=[];
    for(const b of row){
      const inferred=probableFields && b===row[0] && row.some(v=>v.cell!==b.cell) && b.text.trim() && !b.images?.length;
      const label=labels.current.get(b.id) ?? (inferred ? b.text.trim() : undefined);
      if (!label && !b.text.trim() && !b.images?.length && row.some(other=>other.cell===b.cell && labels.current.has(other.id))) continue;
      if(label) pairs.push({label,values:[]});
      else if(pairs.length) pairs[pairs.length-1].values.push(b);
      else return null;
    }
    return pairs.length && pairs.every(p=>p.values.length) ? pairs : null;
  });
  if(party && paired.every(row=>row!==null)) return <div className={s.parties}>
    {paired.map((row,i)=><div className={s.row} key={rows[i][0].id}>{row!.map(p=><div className={s.field} key={p.values[0].id}>
      <span>{p.label}</span>{p.values.map(b=><div key={b.id}>{field(b,p.label,true)}</div>)}</div>)}</div>)}
    {blocks.map(b=><div key={b.id} className={s.after}>{after(b)}</div>)}
  </div>;
  return <div className={s.grid} style={{"--columns":columns} as React.CSSProperties}><WordDocumentLayout blocks={blocks} afterBlock={after}
    renderBlock={b=>field(b,`Hàng ${(b.row??0)+1}, cột ${(b.cell??0)+1}`)}/></div>;
}

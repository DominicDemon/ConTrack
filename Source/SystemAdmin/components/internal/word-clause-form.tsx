"use client";
import { ArrowDown, ArrowUp, Trash2 } from "lucide-react";
import type { ReactNode } from "react";
import WordClauseTable from "./word-clause-table";
import WordDocumentLayout, { type DocumentBlock } from "./word-document-layout";
import { internalStyles as ui } from "./internal-theme";
import s from "./workspace.module.css";

export type Card = { key:string; label:string; title?:string; ids:string[]; fixed?:boolean };
export function wordCards(blocks:DocumentBlock[]) {
  const cards:Card[]=[];
  for (const b of blocks.filter(b=>b.bodyUnit != null)) {
    const text=b.text.trim(), standalone=b.id===b.bodyUnit && b.tableId==null;
    let label=standalone ? text.match(/^(Điều\s+\d+)(?=[\s.:–-]|$)/iu)?.[1] : undefined;
    let fixed=false;
    if (!label && standalone && /^(phụ\s+lục|định\s+nghĩa)(?:\s|[:.–-]|$)/iu.test(text)) label=/^phụ/iu.test(text)?"Phụ lục":"Định nghĩa";
    if (!label && standalone && /^căn cứ(?:\s+(?:vào|hợp đồng|pháp lý))?\s*[:：]?$/iu.test(text)) {label="Căn cứ hợp đồng";fixed=true;}
    if (!label && b.clauseTitle) label="Điều khoản";
    if (!label && cards.at(-1)?.label==="Căn cứ hợp đồng" && /^(hôm nay|chúng tôi gồm|bên a\b)/iu.test(text)) {cards.push({key:b.id,label:"Thông tin các bên",ids:[],fixed:true});}
    if (/^(đại diện (?:của )?bên [ab]|đại diện hai bên)\s*$/iu.test(text) && cards.at(-1)?.label!=="Chữ ký") {
      const start=cards.at(-1)?.ids.indexOf(b.bodyUnit!) ?? -1;
      const leading=start>=0 ? cards.at(-1)!.ids.splice(start):[];
      cards.push({key:"signature-"+b.id,label:"Chữ ký",ids:leading,fixed:true});
    }
    if (label) cards.push({key:b.id,label,title:b.id,ids:[],fixed});
    if (!cards.length) cards.push({key:"opening",label:"Phần mở đầu",ids:[],fixed:true});
    cards.at(-1)!.ids.push(b.id);
  }
  return cards;
}

type Props = { cards:Card[]; blocks:DocumentBlock[]; busy:boolean; active:string; select:(id:string)=>void;
  update:(cards:Card[], blocks:DocumentBlock[])=>void; after:(b:DocumentBlock)=>ReactNode; anchors:Set<string>;
  table:()=>void; image:()=>void; readOnly?:boolean };
export default function WordClauseForm({cards,blocks,busy,active,select,update,after,anchors,table,image,readOnly}:Props) {
  const byId=new Map(blocks.map(b=>[b.id,b]));
  const make=(text:string):DocumentBlock=>{const id=crypto.randomUUID();return {id,bodyUnit:id,text,location:"Nội dung"};};
  const nextNumber=Math.max(0,...cards.map(c=>Number(c.label.match(/^Điều\s+(\d+)/iu)?.[1]??0)))+1;
  function add(kind:string) {
    if (!kind) return;
    const index=cards.findIndex(c=>c.ids.includes(active));
    const label=kind==="Điều khoản"?"Điều "+nextNumber:kind;
    const title=make(label+":"), body=make("");
    const next=cards.slice(); next.splice(index<0?cards.length:index+1,0,{key:title.id,title:title.id,label,ids:[title.id,body.id]});
    update(next,[...blocks,title,body]); select(body.id);
  }
  function split(c:Card) {
    const start=c.ids.indexOf(active), b=byId.get(active);
    if (start<0 || !b || b.tableId!=null || b.bodyUnit!==b.id || c.title===active) return;
    const title=make("Điều "+nextNumber+": Điều khoản bổ sung");
    const next=cards.flatMap(x=>x!==c?[x]:[...(start?[{...c,ids:c.ids.slice(0,start)}]:[]),{key:title.id,title:title.id,label:"Điều khoản",ids:[title.id,...c.ids.slice(start)]}]);
    update(next,[...blocks,title]);select(active);
  }
  function changeText(id:string,text:string){update(cards,blocks.map(b=>b.id===id?{...b,text}:b));}
  function textGroup(c:Card, group:DocumentBlock[]) {
    const value=group.map(b=>b.text).join("\n");
    function pick(e:HTMLTextAreaElement){select(group[Math.min(group.length-1,e.value.slice(0,e.selectionStart).split("\n").length-1)].id);}
    return <label className={s.label} key={group[0].id}>{c.label==="Căn cứ hợp đồng"?"Căn cứ hợp đồng":"Nội dung"}
      <textarea data-word-ids={group.map(b=>b.id).join(" ")} className={ui.field} rows={Math.max(3,Math.min(12,group.length+1))} value={value}
        onSelect={e=>pick(e.currentTarget)} onFocus={e=>pick(e.currentTarget)} onChange={e=>{
          const lines=e.target.value.split("\n"); let prefix=0,end=0;
          while(prefix<Math.min(group.length,lines.length)&&group[prefix].text===lines[prefix]) prefix++;
          while(end<Math.min(group.length,lines.length)-prefix&&group[group.length-1-end].text===lines[lines.length-1-end]) end++;
          const middle=lines.slice(prefix,lines.length-end).map((t,i)=>i<group.length-prefix-end?{...group[prefix+i],text:t}:make(t));
          const next=[...group.slice(0,prefix),...middle,...group.slice(group.length-end)];
          const ids=c.ids.slice();ids.splice(ids.indexOf(group[0].id),group.length,...next.map(b=>b.id));
          const changed=new Map(next.map(b=>[b.id,b]));update(cards.map(x=>x===c?{...x,ids}:x),[...blocks.map(b=>changed.get(b.id)??b),...next.filter(b=>!byId.has(b.id))]);
        }}/></label>;
  }
  return <fieldset disabled={busy || readOnly} className="min-w-0 space-y-4">
    {!readOnly && !cards.some(c=>c.label==="Căn cứ hợp đồng") && <label className={s.label}>Căn cứ hợp đồng
      <textarea className={ui.field} rows={3} value="" onChange={e=>{
        const title=make("Căn cứ hợp đồng:"), body=make(e.target.value);
        const index=cards.findIndex(c=>!c.fixed);
        const next=cards.slice();next.splice(index<0?cards.length:index,0,{key:title.id,label:"Căn cứ hợp đồng",fixed:true,title:title.id,ids:[title.id,body.id]});
        update(next,[...blocks,title,body]);select(body.id);
        requestAnimationFrame(()=>document.querySelector<HTMLTextAreaElement>('[data-word-ids~="'+body.id+'"]')?.focus());
      }}/></label>}
    {cards.map((c,i)=>{
      const parts:ReactNode[]=[];let group:DocumentBlock[]=[];
      const flush=()=>{if(group.length)parts.push(textGroup(c,group));group=[];};
      const ids=c.ids.filter(id=>id!==c.title);
      for(let n=0;n<ids.length;n++){
        const b=byId.get(ids[n])!;
        if(b.tableId!=null || b.images?.length || b.bodyUnit!==b.id){
          flush();const chunk=[b];while(n+1<ids.length && b.tableId!=null && byId.get(ids[n+1])?.tableId===b.tableId)chunk.push(byId.get(ids[++n])!);
          if(b.tableId!=null){parts.push(<WordClauseTable key={b.id} blocks={chunk} party={c.label==="Thông tin các bên"} select={select} change={changeText} after={after} readOnly={readOnly}/>);continue;}
          parts.push(<WordDocumentLayout key={b.id} blocks={chunk} afterBlock={after} renderBlock={x=><>{x.images?.map((img,j)=><img key={j} src={`data:${img.mime};base64,${img.base64}`} alt="Ảnh trong hợp đồng" style={{maxWidth:"100%"}}/>)}<textarea data-word-ids={x.id} aria-label={x.tableId!=null?"Nội dung ô bảng":"Nội dung đoạn"} className={ui.field} rows={1} value={x.text} onFocus={()=>select(x.id)} onChange={e=>changeText(x.id,e.target.value)}/></>}/>);
        }else{group.push(b);if(anchors.has(b.id)){flush();parts.push(<div key={"after"+b.id}>{after(b)}</div>);}}
      }flush();
      const move=(d:number)=>{const next=cards.slice();[next[i],next[i+d]]=[next[i+d],next[i]];update(next,blocks);};
      return <section className={s.lineItem} style={{minWidth:0,gridTemplateColumns:"minmax(0,1fr)"}} key={c.key}><header><strong>{c.label}</strong>{!c.fixed&&!readOnly&&<div>
        <button type="button" disabled={!i} aria-label="Di chuyển lên" onClick={()=>move(-1)}><ArrowUp size={16}/></button>
        <button type="button" disabled={i===cards.length-1} aria-label="Di chuyển xuống" onClick={()=>move(1)}><ArrowDown size={16}/></button>
        <button type="button" aria-label="Xóa mục" onClick={()=>update(cards.filter(x=>x!==c),blocks)}><Trash2 size={16}/></button></div>}</header>
        {c.title&&!c.fixed&&<label className={s.label}>Tiêu đề điều khoản<input data-word-ids={c.title} className={ui.field} value={byId.get(c.title)!.text} onFocus={()=>select(c.title!)} onChange={e=>changeText(c.title!,e.target.value)}/></label>}
        {c.title&&after(byId.get(c.title)!)}{parts}
        {!readOnly&&c.ids.includes(active)&&<div className="flex flex-wrap gap-3"><button type="button" onClick={table}>Chèn bảng</button><button type="button" onClick={image}>Chèn ảnh</button>
        {active!==c.title && byId.get(active)?.tableId==null && <button type="button" onClick={()=>split(c)}>Tách thành điều khoản</button>}</div>}
      </section>;
    })}
    {!readOnly&&<select className={ui.field} aria-label="Thêm mục" value="" onChange={e=>add(e.target.value)}><option value="">Thêm mục…</option>{["Điều khoản","Định nghĩa","Phụ lục"].map(x=><option key={x}>{x}</option>)}</select>}
  </fieldset>;
}

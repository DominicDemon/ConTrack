"use client";
import { useEffect, useState } from "react";
import { internalApi, dateTime, type DraftRecord } from "@/services/internal-api";
import { useInternalUser } from "./internal-shell";
import { internalStyles as ui } from "./internal-theme";

type Finding = {key:string;label:string;found:boolean;location:string|null;excerpt:string|null;resolution:{decision:string;reason:string;actor:string;at:string}|null};
type View = {configured:boolean;rules:string[];rowVersion:string|null;contentHash:string|null;findings:Finding[]};
export function TemplateChecklist({id}: {id:string}) {
  const [view,setView]=useState<View|null>(null);
  const [rules,setRules]=useState<{key:string;label:string}[]>([]);
  const [selected,setSelected]=useState<string[]>([]);
  const [message,setMessage]=useState(""); const [busy,setBusy]=useState(false);
  useEffect(()=>{let active=true; Promise.all([internalApi<View>(`/contract-templates/${id}/checklist`),internalApi<{key:string;label:string}[]>("/checklist-rules")]).then(([v,r])=>{if(active){setView(v);setSelected(v.rules);setRules(r);}}).catch(e=>{if(active)setMessage(e.message);}); return()=>{active=false;};},[id]);
  async function save(){setBusy(true);setMessage("");try{await internalApi(`/contract-templates/${id}/checklist`,"PUT",{rowVersion:view?.rowVersion,rules:selected});const v=await internalApi<View>(`/contract-templates/${id}/checklist`);setView(v);setMessage("Đã lưu danh sách rà soát.");}catch(e){setMessage((e as Error).message);}finally{setBusy(false);}}
  return <details><summary>Điều khoản cần rà soát {view && !view.configured && "— Chưa cấu hình"}</summary><fieldset disabled={busy || !view} style={{display:"grid",gap:8,padding:16}}>{rules.map(rule=><label key={rule.key}><input type="checkbox" checked={selected.includes(rule.key)} onChange={e=>setSelected(old=>e.target.checked?[...old,rule.key]:old.filter(key=>key!==rule.key))}/> {rule.label}</label>)}<button type="button" className={ui.primaryButton} onClick={()=>void save()}>Lưu danh sách</button></fieldset>{message&&<p role="status">{message}</p>}</details>;
}

export default function ChecklistPanel({record,dirty,onChanged,onLocate}:{record:DraftRecord;dirty:boolean;onChanged:()=>void;onLocate:(location:string|null)=>void}){
  const user=useInternalUser();const [view,setView]=useState<View|null>(null);const [error,setError]=useState("");const [reason,setReason]=useState("");const [busy,setBusy]=useState(false);const [catalog,setCatalog]=useState<{key:string;label:string}[]>([]);const [selected,setSelected]=useState<string[]>([]);
  useEffect(()=>{let active=true;Promise.all([internalApi<View>(`/contract-drafts/${record.id}/checklist`),internalApi<{key:string;label:string}[]>("/checklist-rules")]).then(([v,r])=>{if(active){setView(v);setCatalog(r);setSelected(v.rules);setError("");}}).catch(e=>{if(active)setError(e.message);});return()=>{active=false;};},[record.id,record.rowVersion]);
  async function decide(rule:string,decision:string,rules?:string[]){if(!view)return;setBusy(true);setError("");try{await internalApi(`/contract-drafts/${record.id}/checklist`,"POST",{rowVersion:record.rowVersion,versionRowVersion:record.versionRowVersion,checklistRowVersion:view.rowVersion,contentHash:view.contentHash,rule,decision,reason,rules});onChanged();setReason("");}catch(e){setError((e as Error).message);}finally{setBusy(false);}}
  return <section className={ui.card} aria-label="Rà soát điều khoản" style={{padding:20,marginBottom:16}}><h2>Rà soát điều khoản</h2>{error&&<p role="alert">{error}</p>}{!view?<p>Đang kiểm tra nội dung…</p>:<>
    {!view.configured&&<p>Chưa cấu hình rà soát cho phiên bản này.</p>}
    {dirty&&<p>Lưu nội dung để kiểm tra lại cảnh báo.</p>}
    <fieldset disabled={busy||dirty} style={{border:0,padding:0}}>
    {user.canCheck&&!view.configured&&<div style={{display:"grid",gap:10,margin:"12px 0"}}><strong>Chọn điều khoản cần rà soát cho phiên bản này</strong><div style={{display:"grid",gap:8}}>{catalog.map(rule=><label key={rule.key}><input type="checkbox" checked={selected.includes(rule.key)} onChange={e=>setSelected(old=>e.target.checked?[...old,rule.key]:old.filter(key=>key!==rule.key))}/> {rule.label}</label>)}</div><button type="button" className={ui.primaryButton} disabled={busy} onClick={()=>void decide("","CONFIGURE",selected)}>Lưu cấu hình rà soát</button></div>}
    {user.canCheck&&record.canEdit&&<button type="button" className={ui.secondaryButton} onClick={()=>void decide("","APPLY_TEMPLATE")}>Áp dụng danh sách hiện tại của mẫu</button>}
    {user.canCheck&&<label>Ghi chú rà soát<textarea className={ui.field} value={reason} maxLength={2000} onChange={e=>setReason(e.target.value)}/></label>}
    <ul>{view.findings.map(item=><li key={item.key} style={{marginTop:16}}><strong>{item.label}</strong><p>{item.resolution?`${item.resolution.decision==="NOT_APPLICABLE"?"Không áp dụng":"Đã rà soát"} · ${item.resolution.actor}`:item.found?"Tìm thấy nội dung liên quan — cần rà soát":"Chưa tìm thấy nội dung đầy đủ về “"+item.label+"”"}</p>{item.resolution&&<p>{item.resolution.reason} · {dateTime(item.resolution.at)}</p>}{item.excerpt&&<blockquote style={{whiteSpace:"pre-wrap"}}>{item.excerpt}</blockquote>}
    <button type="button" className={ui.secondaryButton} onClick={()=>onLocate(item.location)}>Đi tới nội dung</button>
    {user.canCheck&&!item.resolution&&<><button type="button" className={ui.secondaryButton} disabled={!reason.trim()} onClick={()=>void decide(item.key,"REVIEWED")}>Đã rà soát</button><button type="button" className={ui.secondaryButton} disabled={!reason.trim()} onClick={()=>void decide(item.key,"NOT_APPLICABLE")}>Không áp dụng</button></>}
    </li>)}</ul></fieldset></>}
    {!user.canCheck&&<p>Gửi nhận xét nội bộ để đề nghị Hành chính bổ sung điều khoản.</p>}
  </section>;
}

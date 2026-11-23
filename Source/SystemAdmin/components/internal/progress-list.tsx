"use client";
import {useEffect,useState} from "react";
import Link from "next/link";
import {internalApi,dateTime,type EmployeeOption} from "@/services/internal-api";
import {useInternalUser} from "./internal-shell";
import {internalStyles as ui} from "./internal-theme";
import s from "./workspace.module.css";
const stages:Record<string,string>={DRAFT:"Chuẩn bị",OFFICE:"Hành chính kiểm tra",RETURNED:"Trả lại chỉnh sửa",APPROVED:"Chờ Sale gửi",OFFICE_SEND:"Chờ Hành chính gửi",CUSTOMER:"Chờ khách phản hồi",CHANGES:"Khách yêu cầu xử lý",FINAL_CHECK:"Hành chính duyệt bản chốt",SIGNATURE:"Chờ Sếp ký",PAPER_SIGNATURE:"Chờ nộp bản ký giấy",PAPER_REVIEW:"Chờ xác nhận bản ký giấy",CLOSED:"Đã đóng"};
type Row={id:string;code:string;name:string;customer:string;ownerId:string;owner:string;stage:string;department:string;enteredAt:string|null;lastActor:string|null;needsMe:boolean};
type Page={items:Row[];total:number;page:number;pageSize:number;counts:Record<string,number>;serverTime:string};
export default function ProgressList(){
 const user=useInternalUser(); const [data,setData]=useState<Page|null>(null);const [error,setError]=useState("");const [busy,setBusy]=useState(false);const [employees,setEmployees]=useState<EmployeeOption[]>([]);
 const [search,setSearch]=useState("");const [owner,setOwner]=useState("");const [stage,setStage]=useState("");const [days,setDays]=useState("");const [mine,setMine]=useState(false);const [page,setPage]=useState(1);const [reload,setReload]=useState(0);const [from,setFrom]=useState("");const [to,setTo]=useState("");
 const badRange=!!from&&!!to&&from>to;
 useEffect(()=>{if(user.canManageAll)internalApi<EmployeeOption[]>("/employees").then(setEmployees).catch(e=>setError(e.message));},[user.canManageAll]);
 useEffect(()=>{const timer=setInterval(()=>{if(document.visibilityState==="visible")setReload(v=>v+1);},60000);return()=>clearInterval(timer);},[]);
 useEffect(()=>{let active=true;const timer=setTimeout(()=>{setBusy(true);const params=new URLSearchParams({page:String(page),mine:String(mine)});if(search.trim())params.set("search",search.trim());if(owner)params.set("ownerId",owner);if(stage)params.set("stage",stage);if(days)params.set("waitingDays",days);if(badRange){setBusy(false);return;}if(from)params.set("from",from);if(to)params.set("to",to);internalApi<Page>(`/contract-progress?${params}`).then(v=>{if(active){setData(v);setError("");}}).catch(e=>{if(active)setError(e.message);}).finally(()=>{if(active)setBusy(false);});},250);return()=>{active=false;clearTimeout(timer);};},[search,owner,stage,days,mine,page,reload,from,to,badRange]);
 function waiting(row:Row){if(!row.enteredAt||!data)return "Chưa có dữ liệu";const hours=Math.max(0,Math.floor((Date.parse(data.serverTime+(/Z$/.test(data.serverTime)?"":"Z"))-Date.parse(row.enteredAt+(/Z$/.test(row.enteredAt)?"":"Z")))/3600000));return hours<24?`${hours} giờ`:`${Math.floor(hours/24)} ngày ${hours%24} giờ`;}
 return <main className={s.page}><div className={s.titleRow}><h1>Tiến độ hợp đồng</h1>{(user.canWrite || user.canCheck) && <Link href="/workspace/contracts/drafts/new" className={ui.primaryButton}>Tạo hợp đồng nháp</Link>}</div>
 <div className={s.toolbar}>{Object.entries(data?.counts??{}).map(([key,count])=><button key={key} className={ui.secondaryButton} onClick={()=>{setStage(key);setPage(1);}}>{stages[key]??key} ({count})</button>)}</div>
 <section className={ui.card}><div className={s.toolbar}>
 <input aria-label="Tìm hợp đồng" className={ui.field} placeholder="Tên, số hợp đồng, tên khách hàng hoặc mã số thuế" value={search} onChange={e=>{setSearch(e.target.value);setPage(1);}}/>
 <label>Tạo từ ngày<input className={ui.field} type="date" value={from} max={to||undefined} aria-invalid={badRange} onChange={e=>{setFrom(e.target.value);setPage(1);}}/></label>
 <label>Đến ngày<input className={ui.field} type="date" value={to} min={from||undefined} aria-invalid={badRange} onChange={e=>{setTo(e.target.value);setPage(1);}}/></label>
 {(from||to)&&<button className={ui.secondaryButton} onClick={()=>{setFrom("");setTo("");setPage(1);}}>Bỏ lọc ngày</button>}
 {user.canManageAll&&<select aria-label="Nhân viên phụ trách" className={ui.field} value={owner} onChange={e=>{setOwner(e.target.value);setPage(1);}}><option value="">Tất cả nhân viên</option>{employees.map(e=><option key={e.id} value={e.id}>{e.name}</option>)}</select>}
 <select aria-label="Bước xử lý" className={ui.field} value={stage} onChange={e=>{setStage(e.target.value);setPage(1);}}><option value="">Tất cả bước</option>{Object.entries(stages).map(([key,label])=><option value={key} key={key}>{label}</option>)}</select>
 <label>Chờ từ (ngày)<input className={ui.field} type="number" min={0} max={36500} value={days} onChange={e=>{setDays(e.target.value);setPage(1);}}/></label>
 <label><input type="checkbox" checked={mine} onChange={e=>{setMine(e.target.checked);setPage(1);}}/> Cần tôi xử lý</label>
 <button className={ui.secondaryButton} disabled={busy} onClick={()=>setReload(v=>v+1)}>Làm mới</button></div>
 {badRange&&<p role="alert">“Từ ngày” phải trước hoặc bằng “Đến ngày”.</p>}{error&&<p role="alert">{error}</p>}{busy&&<p role="status">Đang tải tiến độ…</p>}
 <div className={s.tableWrap}><table className={s.table}><thead><tr><th>Hợp đồng</th><th>Khách hàng</th><th>Phụ trách</th><th>Bước hiện tại</th><th>Bộ phận</th><th>Vào bước</th><th>Đã chờ</th></tr></thead><tbody>{data?.items.map(row=><tr key={row.id}><td><Link href={`/workspace/contracts/drafts/${row.id}`}>{row.name}</Link><p>{row.code}</p></td><td>{row.customer}</td><td>{row.owner}</td><td>{stages[row.stage]??row.stage}{row.lastActor&&<p>Duyệt nội bộ gần nhất · {row.lastActor}</p>}</td><td>{row.department}</td><td>{row.enteredAt?dateTime(row.enteredAt):"Chưa có dữ liệu"}</td><td>{waiting(row)}</td></tr>)}</tbody></table></div>
 {data?.total===0&&<p>Không có hợp đồng phù hợp.</p>}
 <div className={s.toolbar}><button className={ui.secondaryButton} disabled={page===1||busy} onClick={()=>setPage(p=>p-1)}>Trang trước</button><span>Trang {page} · {data?.total??0} hợp đồng</span><button className={ui.secondaryButton} disabled={busy||!data||page*25>=data.total} onClick={()=>setPage(p=>p+1)}>Trang sau</button></div></section></main>;
}

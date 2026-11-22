"use client";
import { useEffect, useState } from "react";
import NumberRule from "@/components/internal/number-rule";
import { useInternalUser } from "@/components/internal/internal-shell";
import { internalApi, type CustomerLookupResult } from "@/services/internal-api";
import { internalStyles as ui } from "@/components/internal/internal-theme";
import s from "@/components/internal/workspace.module.css";

export default function NumberRulesPage() {
  const { canCheck } = useInternalUser();
  const [search,setSearch] = useState("");
  const [rows,setRows] = useState<CustomerLookupResult[]>([]);
  const [selected,setSelected] = useState<CustomerLookupResult | null>(null);
  const [error,setError] = useState("");
  useEffect(() => {
    if (!canCheck || !search.trim()) return;
    let active = true;
    const timer = setTimeout(() => {
      void internalApi<CustomerLookupResult[]>("/customers/lookup?search="+encodeURIComponent(search.trim()))
        .then(list => { if (active) setRows(list.filter(r => r.businessId)); })
        .catch(e => { if (active) setError(e.message); });
    },250);
    return () => { active=false; clearTimeout(timer); };
  },[canCheck,search]);
  if (!canCheck) return <p role="alert">Chỉ Hành chính được quản lý quy tắc.</p>;
  return <main className={s.page}><h1>Quy tắc số hợp đồng</h1>
    <section className={ui.card}><NumberRule owner="0" manage/></section>
    <section className={ui.card}><label className={s.label}>Pháp nhân Bên A<input className={ui.field} value={search} placeholder="Tìm theo email, số điện thoại hoặc mã số thuế" onChange={e => { setSearch(e.target.value); setSelected(null); setRows([]); setError(""); }}/></label>
      {error && <p role="alert">{error}</p>}
      {rows.map(r => <button type="button" key={r.businessId} className={ui.secondaryButton} onClick={() => setSelected(r)}>{r.businessName} · {r.customer.name}</button>)}
      {selected?.businessId && <div><h2>{selected.businessName}</h2><NumberRule manage key={selected.businessId} owner={selected.businessId} customerId={selected.customer.id}/></div>}
    </section></main>;
}

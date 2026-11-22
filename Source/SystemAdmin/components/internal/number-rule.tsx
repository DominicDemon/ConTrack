"use client";
import { useEffect, useState } from "react";
import { internalApi, InternalApiError } from "@/services/internal-api";
import { internalStyles as ui } from "./internal-theme";
import s from "./workspace.module.css";

type Rule = { companyCode: string; typeCode: string; includeDay: boolean };
const blank: Rule = { companyCode: "", typeCode: "", includeDay: false };
function example(rule: Rule) {
  const parts = new Intl.DateTimeFormat("en-GB", { timeZone: "Asia/Ho_Chi_Minh", year: "numeric", month: "2-digit", day: "2-digit" }).formatToParts(new Date());
  const get = (key: string) => parts.find(p => p.type === key)?.value ?? "";
  return [rule.companyCode || "FPT", rule.typeCode || "KD", get("year") + get("month") + (rule.includeDay ? get("day") : ""), "000001"].join("-");
}

export default function NumberRule({ owner, customerId, manage, selected, choose }: {
  owner: string; customerId?: string; manage?: boolean; selected?: boolean; choose?: () => void;
}) {
  const path = "/number-rules/" + owner + (customerId ? "?customerId=" + encodeURIComponent(customerId) : "");
  const [rule, setRule] = useState<Rule | null | undefined>();
  const [draft, setDraft] = useState<Rule>(blank);
  const [editing, setEditing] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [refresh, setRefresh] = useState(0);
  useEffect(() => {
    let active = true;
    internalApi<{ rule: Rule | null }>(path).then(r => { if (active) setRule(r.rule); })
      .catch(e => { if (active) setError(e.message); });
    return () => { active = false; };
  }, [path, refresh]);
  async function save() {
    setBusy(true); setError("");
    try {
      const saved = await internalApi<Rule>(path, rule ? "PUT" : "POST", draft);
      setRule(saved); setEditing(false); choose?.();
    } catch (e) {
      setError((e as Error).message);
      if (e instanceof InternalApiError && e.status === 409) { setEditing(false); setRefresh(v => v + 1); }
    } finally { setBusy(false); }
  }
  const side = owner === "0" ? "Bên B" : "Bên A";
  return <div className={s.fields}>
    {choose ? <label><input type="radio" name="number-source" checked={!!selected} onChange={choose} disabled={owner !== "0" && !rule}/> Dùng quy tắc {side}</label> : <strong>Quy tắc {side}</strong>}
    {rule && <span className={s.muted}>Ví dụ: {example(rule)}</span>}
    {owner === "0" && rule === null && <span className={s.muted}>Mặc định: HD-ngày-số thứ tự</span>}
    {error && <p role="alert" className={ui.error}>{error}</p>}
    {rule !== undefined && !editing && (manage || (!rule && owner !== "0")) &&
      <button type="button" className={ui.secondaryButton} onClick={() => { setDraft(rule ?? blank); setEditing(true); setError(""); }}>{rule ? "Sửa quy tắc" : "Thiết lập số hợp đồng"}</button>}
    {editing && <fieldset disabled={busy} className={s.fields} onKeyDown={e => {
      if (e.key === "Enter" && e.target instanceof HTMLInputElement) { e.preventDefault(); if (!busy) void save(); }
    }}>
      <label className={s.label}>Mã công ty<input className={ui.field} maxLength={16} value={draft.companyCode} onChange={e => setDraft({...draft,companyCode:e.target.value.toUpperCase()})}/></label>
      <label className={s.label}>Mã loại hợp đồng<input className={ui.field} maxLength={10} value={draft.typeCode} onChange={e => setDraft({...draft,typeCode:e.target.value.toUpperCase()})}/></label>
      <label className={s.label}>Định dạng ngày<select className={ui.field} value={draft.includeDay ? "day" : "month"} onChange={e => setDraft({...draft,includeDay:e.target.value==="day"})}><option value="month">Năm-tháng</option><option value="day">Năm-tháng-ngày</option></select></label>
      <span>Ví dụ: {example(draft)}</span><div className={s.actions}>
        <button type="button" className={ui.primaryButton} disabled={!draft.companyCode.trim() || !draft.typeCode.trim()} onClick={() => void save()}>Lưu quy tắc</button>
        <button type="button" className={ui.secondaryButton} onClick={() => setEditing(false)}>Hủy</button>
      </div></fieldset>}
  </div>;
}

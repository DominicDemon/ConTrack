"use client";
import { useEffect, useRef, useState } from "react";
import { internalApi, InternalApiError } from "@/services/internal-api";
import { internalStyles as ui } from "./internal-theme";
import s from "./workspace.module.css";

type OwnerIdentity = { businessId: string; customerId: string };
type Rule = { companyCode: string; typeCode: string; includeDay: boolean };
const blank: Rule = { companyCode: "", typeCode: "KD", includeDay: false };
const companyCodePattern = /^[A-Z0-9]{1,16}$/;
const typeCodePattern = /^[A-Z0-9]{1,10}$/;
function example(rule: Rule) {
  const parts = new Intl.DateTimeFormat("en-GB", { timeZone: "Asia/Ho_Chi_Minh", year: "numeric", month: "2-digit", day: "2-digit" }).formatToParts(new Date());
  const get = (key: string) => parts.find(p => p.type === key)?.value ?? "";
  return [rule.companyCode || "FPT", rule.typeCode || "KD", get("year") + get("month") + (rule.includeDay ? get("day") : ""), "000001"].join("-");
}

export default function NumberRule({ owner, customerId, manage, selected, choose, openSetup, resolveOwner, onResolved }: {
  resolveOwner?: () => Promise<OwnerIdentity | undefined>; onResolved?: (identity: OwnerIdentity) => void;
  owner: string; customerId?: string; manage?: boolean; selected?: boolean; choose?: () => void; openSetup?: boolean;
}) {
  const path = "/number-rules/" + owner + (customerId ? "?customerId=" + encodeURIComponent(customerId) : "");
  const [rule, setRule] = useState<Rule | null | undefined>();
  const [draft, setDraft] = useState<Rule>(blank);
  const [editing, setEditing] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [savedNotice, setSavedNotice] = useState(false);
  const [refresh, setRefresh] = useState(0);
  const companyCode = draft.companyCode.trim().toUpperCase();
  const typeCode = draft.typeCode.trim().toUpperCase();
  const valid = companyCodePattern.test(companyCode) && typeCodePattern.test(typeCode);
  const quickSetup = owner !== "0" && !!choose && !manage;
  const previousOwner = useRef(owner);
  useEffect(() => {
    if (previousOwner.current && previousOwner.current !== owner) setDraft(blank);
    if (previousOwner.current !== owner) setSavedNotice(false);
    previousOwner.current = owner;
    setError(""); setRule(undefined); setEditing(false);
    if (!owner) { setRule(null); setEditing(true); return; }
    let active = true;
    internalApi<{ rule: Rule | null }>(path).then(r => {
      if (!active) return;
      setRule(r.rule);
      if (openSetup && !r.rule && refresh === 0) setEditing(true);
    })
      .catch(e => { if (active) setError(e.message); });
    return () => { active = false; };
  }, [path, owner, refresh, openSetup]);
  async function save() {
    if (!valid || busy) return;
    setBusy(true); setError("");
    try {
      const identity = !owner ? await resolveOwner?.() : undefined;
      if (!owner && !identity) { setError("Kiểm tra các thông tin còn thiếu trong mục Bên A."); return; }
      const target = identity ? "/number-rules/" + identity.businessId + "?customerId=" + identity.customerId : path;
      const saved = await internalApi<Rule>(target, rule ? "PUT" : "POST", { ...draft, companyCode, typeCode });
      setRule(saved); setEditing(false); setSavedNotice(true);
      if (identity) { onResolved?.(identity); setRefresh(v => v + 1); } else choose?.();
    } catch (e) {
      setError((e as Error).message);
      if (e instanceof InternalApiError && e.status === 409) { setEditing(false); setRefresh(v => v + 1); }
    } finally { setBusy(false); }
  }
  const side = owner === "0" ? "Bên B" : "Bên A";
  const formatOptions = <>
    <label className={s.label}>Mã loại hợp đồng<input className={ui.field} placeholder="Ví dụ: KD" value={draft.typeCode}
      aria-invalid={!!draft.typeCode && !typeCodePattern.test(typeCode)}
      onChange={e => setDraft({...draft,typeCode:e.target.value.toUpperCase()})}/>
      <small className={s.muted}>Tối đa 10 chữ cái A–Z hoặc chữ số.</small></label>
    <label className={s.label}>Phần ngày trong số hợp đồng<select className={ui.field} value={draft.includeDay ? "day" : "month"} onChange={e => setDraft({...draft,includeDay:e.target.value==="day"})}><option value="month">Năm-tháng</option><option value="day">Năm-tháng-ngày</option></select></label>
  </>;
  return <div className={s.fields}>
    {!choose ? <strong>Quy tắc {side}</strong> : (owner === "0" || rule) &&
      <label><input type="radio" name="number-source" checked={!!selected} onChange={() => { setSavedNotice(false); choose(); }}/>
        {owner === "0" ? " Dùng số theo công ty mình (Bên B)" : " Dùng số theo khách hàng (Bên A)"}</label>}
    {rule && <span className={s.muted}>Dạng số: {example(rule)}</span>}
    {savedNotice && (!choose || selected) && <p role="status" className={ui.notice}>{choose ? "Đã lưu cho khách hàng này. Số chính thức được cấp khi bạn lưu nháp." : `Đã lưu quy tắc ${side}.`}</p>}
    {owner === "0" && rule === null && <span className={s.muted}>Mặc định: HD-ngày-số thứ tự</span>}
    {error && <p role="alert" className={ui.error}>{error}</p>}
    {rule !== undefined && !editing && (manage || (!rule && owner !== "0")) &&
      <button type="button" className={ui.secondaryButton} onClick={() => { setDraft(rule ?? blank); setEditing(true); setError(""); setSavedNotice(false); }}>{rule ? "Sửa quy tắc" : quickSetup ? "Tạo quy tắc số cho khách hàng" : "Thiết lập số hợp đồng"}</button>}
    {editing && <fieldset disabled={busy} className={s.fields} onKeyDown={e => {
      if (e.key === "Enter" && e.target instanceof HTMLInputElement) { e.preventDefault(); if (!busy) void save(); }
    }}>
      {quickSetup && <strong>Cấu hình số hợp đồng Bên A</strong>}
      <label className={s.label}>{quickSetup ? "Mã viết tắt của khách hàng" : "Mã công ty viết tắt"}<input className={ui.field} placeholder="Ví dụ: FPT" value={draft.companyCode}
        aria-invalid={!!draft.companyCode && !companyCodePattern.test(companyCode)}
        onChange={e => setDraft({...draft,companyCode:e.target.value.toUpperCase()})}/>
        <small className={s.muted}>Tối đa 16 chữ cái A–Z hoặc chữ số, không dấu, không khoảng trắng.</small></label>
      <span>Dạng số: <strong>{example({ ...draft, companyCode, typeCode })}</strong></span>
      {formatOptions}

      <div className={s.actions}>
        <button type="button" className={ui.primaryButton} disabled={!valid} onClick={() => void save()}>{busy ? "Đang lưu…" : quickSetup ? "Lưu và dùng cho nháp này" : "Lưu quy tắc"}</button>
        <button type="button" className={ui.secondaryButton} onClick={() => setEditing(false)}>Hủy</button>
      </div></fieldset>}
  </div>;
}

import { useId } from "react";
import type { CustomerInput } from "@/services/internal-api";
import { internalStyles as ui } from "./internal-theme";

const field = "grid gap-2 text-sm font-medium";

export const newCustomer = (): CustomerInput => ({
  name: "", phone: "", email: "", company: "", taxCode: "", address: "",
});

export function customerCreateReady(value: CustomerInput) {
  return Boolean(value.partyType && value.name.trim() && value.phone.trim() &&
    (value.partyType === "PERSON" || (value.company.trim() && value.taxCode.trim())));
}

export function CustomerCreateFields({ value, onChange, busy }: {
  value: CustomerInput;
  onChange: (value: CustomerInput) => void;
  busy: boolean;
}) {
  const business = value.partyType === "BUSINESS";
  const group = useId();

  return <fieldset disabled={busy} className="grid min-w-0 gap-4">
    <legend className="mb-2 text-sm font-medium">Loại khách hàng</legend>
    <div className="flex flex-wrap gap-4">
      <label className="flex items-center gap-2 text-sm">
        <input type="radio" name={group} checked={value.partyType === "PERSON"}
          onChange={() => onChange({ ...value, partyType: "PERSON", company: "", taxCode: "" })}/>
        Cá nhân
      </label>
      <label className="flex items-center gap-2 text-sm">
        <input type="radio" name={group} checked={business}
          onChange={() => onChange({ ...value, partyType: "BUSINESS" })}/>
        Doanh nghiệp
      </label>
    </div>
    {value.partyType && <div className="grid gap-4 sm:grid-cols-2">
      {business && <>
        <label className={`${field} sm:col-span-2`}>Tên pháp lý doanh nghiệp *
          <input className={ui.field} maxLength={1000} value={value.company} onChange={e => onChange({ ...value, company: e.target.value })}/>
        </label>
        <label className={field}>Mã số thuế *
          <input className={ui.field} maxLength={20} value={value.taxCode} onChange={e => onChange({ ...value, taxCode: e.target.value })}/>
        </label>
      </>}
      <label className={business ? field : `${field} sm:col-span-2`}>{business ? "Người đại diện *" : "Họ và tên *"}
        <input className={ui.field} maxLength={200} value={value.name} onChange={e => onChange({ ...value, name: e.target.value })}/>
      </label>
      <label className={field}>Số điện thoại *
        <input className={ui.field} type="tel" maxLength={20} value={value.phone} onChange={e => onChange({ ...value, phone: e.target.value })}/>
      </label>
      <label className={field}>Email
        <input className={ui.field} inputMode="email" maxLength={255} value={value.email} onChange={e => onChange({ ...value, email: e.target.value })}/>
      </label>
      <label className={`${field} sm:col-span-2`}>{business ? "Địa chỉ doanh nghiệp" : "Địa chỉ"}
        <input className={ui.field} maxLength={1000} value={value.address} onChange={e => onChange({ ...value, address: e.target.value })}/>
      </label>
    </div>}
  </fieldset>;
}

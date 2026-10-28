const INVOICE_PATH = /^\/billing\/invoices\/[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

/** Whitelist canonical destinations; never decode or follow nested return URLs. */
export function getAuthReturnPath(value: string | null): string | null {
  if (!value || value.length > 512 || /[%\\#\s]/.test(value)) return null;
  if (value === "/billing/invoices" || INVOICE_PATH.test(value)) return value;
  if (!/^\/pricing(?:\?|$)/.test(value)) return null;
  const url = new URL(value, "https://contrack.invalid");
  if ([...url.searchParams.keys()].some(key => !["plan", "months"].includes(key))) return null;
  if ([...url.searchParams.keys()].some(key => url.searchParams.getAll(key).length !== 1)) return null;
  const plan = url.searchParams.get("plan");
  if (plan === "trial" && !url.searchParams.has("months")) return "/pricing?plan=trial";
  const rawMonths = url.searchParams.get("months");
  const months = rawMonths === null ? 12 : Number(rawMonths);
  if ((plan === null || plan === "paid") && Number.isInteger(months) && months >= 1 && months <= 24)
    return `/pricing?plan=paid&months=${months}`;
  return null;
}

export function authHref(path: "/" | "/change-password", returnTo?: string | null,
  tenantCode?: string | null, flags: Record<string, string> = {}) {
  const query = new URLSearchParams(flags);
  const safe = getAuthReturnPath(returnTo ?? null);
  if (safe) query.set("returnTo", safe);
  if (tenantCode && /^[a-zA-Z0-9_-]{1,50}$/.test(tenantCode)) query.set("tenantCode", tenantCode);
  return `${path}${query.size ? `?${query}` : ""}`;
}

export function authDestination(returnTo: string | null, tenantCode: string): string | null {
  const safe = getAuthReturnPath(returnTo);
  if (!safe || (safe !== "/billing/invoices" && !INVOICE_PATH.test(safe))) return safe;
  return `${safe}?${new URLSearchParams({ tenantCode })}`;
}

export function currentAuthHref(path: "/" | "/change-password", flags: Record<string, string> = {}, tenantCode?: string) {
  const query = new URLSearchParams(window.location.search);
  const destination = getAuthReturnPath(window.location.pathname) ?? getAuthReturnPath(query.get("returnTo"));
  return authHref(path, destination, query.get("tenantCode") || tenantCode, flags);
}

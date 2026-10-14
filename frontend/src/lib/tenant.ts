/**
 * Xác định tenant từ hostname theo quy ước <tenant>.<domain> (BR-21).
 * Trên localhost (không có subdomain) dùng tenant dev từ VITE_DEV_TENANT.
 */
export function resolveTenantCode(hostname: string, devTenant?: string): string | null {
  const host = hostname.toLowerCase();
  if (host === 'localhost' || /^\d{1,3}(\.\d{1,3}){3}$/.test(host)) {
    return devTenant?.trim() || null;
  }
  const parts = host.split('.');
  if (parts.length < 3) {
    return null; // tên miền gốc, ví dụ contrack.vn: trang Product Owner / đăng ký
  }
  const sub = parts[0];
  return sub === 'www' || sub === 'app' ? null : sub;
}

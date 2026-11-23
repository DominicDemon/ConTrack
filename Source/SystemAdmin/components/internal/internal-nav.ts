import {
  Building2,
  Contact,
  FileSignature,
  FileChartColumnIncreasing,
  Files,
  KeyRound,
  LockKeyhole,
  LayoutDashboard,
  LifeBuoy,
  Mail,
  Package,
  ScanLine,
  Search,
  UsersRound,
  type LucideIcon,
} from "lucide-react";

/** One screen of the internal workspace (screens not moved to MASTER yet are listed in Backend/docs/REFACTOR-NOI-BO.md). permissions: any one grants it; empty = everyone. */
export type NavItem = { label: string; icon: LucideIcon; href: string; permissions: string[]; exclude?: string };

/** Order follows the departments: Ban Giám Đốc, Kinh doanh, Kỹ thuật, Kế toán, Hành chính - Pháp chế (005-department-roles.sql). */
export const NAV_ITEMS: NavItem[] = [
  { label: "Tổng quan", icon: LayoutDashboard, href: "/workspace", permissions: [] },
  { label: "Quản lý văn bản", icon: Files, href: "/workspace/templates", permissions: ["template.manage"] },
  { label: "Loại văn bản", icon: Files, href: "/workspace/document-categories", permissions: ["template.manage"] },
  { label: "Định dạng hợp đồng", icon: Files, href: "/workspace/contract-format", permissions: ["template.manage"] },
  { label: "Quy tắc số hợp đồng", icon: Files, href: "/workspace/number-rules", permissions: ["contract.check"] },
  { label: "Nhân viên & phân quyền", icon: UsersRound, href: "/workspace/admin/employees", permissions: ["employee.manage"] },
  { label: "Phòng ban", icon: Building2, href: "/workspace/admin/departments", permissions: ["department.manage"] },
  { label: "Quản lý hợp đồng", icon: FileSignature, href: "/workspace/contracts", permissions: ["draft.write", "contract.check", "contract.approve", "draft.manage.all"] },
  { label: "Báo cáo hợp đồng", icon: FileChartColumnIncreasing, href: "/workspace/contract-reports", permissions: ["draft.manage.all"] },
  { label: "Bản ký giấy", icon: FileSignature, href: "/workspace/signed-contracts", permissions: ["contract.signed_evidence.view"], exclude: "/workspace/signed-contracts/search" },
  { label: "Hợp đồng giấy scan", icon: ScanLine, href: "/workspace/scanned-contracts", permissions: ["contract.scanned.view"] },
  { label: "Tìm hợp đồng", icon: Search, href: "/workspace/signed-contracts/search", permissions: ["contract.signed_evidence.view", "contract.scanned.view"] },
  { label: "Lịch sử gửi email", icon: Mail, href: "/workspace/scan-emails", permissions: ["contract.signed_evidence.view", "contract.scanned.view", "audit.security", "system.encryption_key.manage"] },
  { label: "Key của tôi", icon: LockKeyhole, href: "/workspace/my-key", permissions: ["contract.signed_evidence.view", "contract.scanned.view"] },
  { label: "Quản lý key", icon: KeyRound, href: "/workspace/encryption-keys", permissions: ["system.encryption_key.manage"] },
  { label: "Khách hàng", icon: Contact, href: "/workspace/customers", permissions: ["customer.manage", "customer.manage.all"] },
  { label: "Sản phẩm và dịch vụ", icon: Package, href: "/workspace/catalog", permissions: ["product.read", "product.manage"] },
  { label: "Hỗ trợ khách hàng", icon: LifeBuoy, href: "/workspace/support-tickets", permissions: ["ticket.handle", "ticket.manage.all"] },
];

/** The legal role receives only existing contract workflow screens, based on its assigned permission. */
const ADMIN_OFFICER_NAV_ITEMS: NavItem[] = [
  { label: "Tổng quan", icon: LayoutDashboard, href: "/workspace", permissions: [] },
  { label: "Quản lý hợp đồng", icon: FileSignature, href: "/workspace/contracts", permissions: ["legal.manage", "contract.read"] },
  { label: "Quản lý văn bản", icon: Files, href: "/workspace/templates", permissions: ["template.read", "template.manage"] },
  // Legal keeps the signed paper copies and the scans: the same screens as the main menu, each still behind its permission.
  ...NAV_ITEMS.filter(item => ["/workspace/signed-contracts", "/workspace/scanned-contracts", "/workspace/signed-contracts/search",
    "/workspace/scan-emails", "/workspace/my-key", "/workspace/encryption-keys"].includes(item.href)),
  { label: "Hỗ trợ khách hàng", icon: LifeBuoy, href: "/workspace/support-tickets", permissions: ["ticket.handle", "ticket.manage.all"] },
];

/** Screens a set of permissions opens, in menu order. */
export const screensFor = (permissions: string[]) => {
  const items = permissions.includes("legal.manage") ? ADMIN_OFFICER_NAV_ITEMS : NAV_ITEMS;
  return items.filter(item => item.permissions.length === 0 || item.permissions.some(p => permissions.includes(p)));
};

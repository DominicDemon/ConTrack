import {
  Building2,
  Contact,
  FileSignature,
  Files,
  KeyRound,
  LayoutDashboard,
  LifeBuoy,
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
  { label: "Nhân viên & phân quyền", icon: UsersRound, href: "/workspace/admin/employees", permissions: ["employee.manage"] },
  { label: "Phòng ban", icon: Building2, href: "/workspace/admin/departments", permissions: ["department.manage"] },
  { label: "Hợp đồng nháp", icon: Files, href: "/workspace/contracts/drafts", permissions: ["draft.write"] },
  { label: "Bản ký giấy", icon: FileSignature, href: "/workspace/signed-contracts", permissions: ["contract.signed_evidence.view"], exclude: "/workspace/signed-contracts/search" },
  { label: "Hợp đồng giấy scan", icon: ScanLine, href: "/workspace/scanned-contracts", permissions: ["contract.scanned.view"] },
  { label: "Tìm hợp đồng", icon: Search, href: "/workspace/signed-contracts/search", permissions: ["contract.signed_evidence.view", "contract.scanned.view"] },
  { label: "Key mở tệp", icon: KeyRound, href: "/workspace/encryption-keys", permissions: ["system.encryption_key.manage"] },
  { label: "Khách hàng", icon: Contact, href: "/workspace/customers", permissions: ["customer.manage"] },
  { label: "Sản phẩm và dịch vụ", icon: Package, href: "/workspace/catalog", permissions: ["product.read", "product.manage"] },
  { label: "Hỗ trợ khách hàng", icon: LifeBuoy, href: "/workspace/support-tickets", permissions: ["ticket.handle", "ticket.manage.all"] },
];

/** Screens a set of permissions opens, in menu order. */
export const screensFor = (permissions: string[]) =>
  NAV_ITEMS.filter(item => item.permissions.length === 0 || item.permissions.some(p => permissions.includes(p)));

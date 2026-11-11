import { Building2, FileSignature, FileStack, Files, History, LayoutDashboard, LifeBuoy, Package, Receipt, Scale, ShieldAlert, UsersRound, Contact, type LucideIcon } from "lucide-react";

/** One screen of the internal workspace. href null = not moved to MASTER yet ("Sắp có"). permissions: any one grants it; empty = everyone. */
export type NavItem = { label: string; icon: LucideIcon; href: string | null; permissions: string[] };

/** Order follows the departments: Ban Giám Đốc, Kinh doanh, Kỹ thuật, Kế toán, Hành chính - Pháp chế (005-department-roles.sql). */
export const NAV_ITEMS: NavItem[] = [
  { label: "Tổng quan", icon: LayoutDashboard, href: "/workspace", permissions: [] },
  { label: "Duyệt & ký hợp đồng", icon: FileSignature, href: null, permissions: ["contract.approve"] },
  { label: "Nhân viên & phân quyền", icon: UsersRound, href: "/workspace/admin/employees", permissions: ["employee.manage"] },
  { label: "Phòng ban", icon: Building2, href: "/workspace/admin/departments", permissions: ["department.manage"] },
  { label: "Nhật ký bảo mật", icon: ShieldAlert, href: null, permissions: ["audit.security"] },
  { label: "Hợp đồng nháp", icon: Files, href: "/workspace/contracts/drafts", permissions: ["draft.write"] },
  { label: "Hợp đồng", icon: Files, href: null, permissions: ["contract.read"] },
  { label: "Khách hàng", icon: Contact, href: "/workspace/customers", permissions: ["customer.manage"] },
  { label: "Sản phẩm và dịch vụ", icon: Package, href: "/workspace/catalog", permissions: ["product.read", "product.manage"] },
  { label: "Nhật ký hợp đồng", icon: History, href: null, permissions: ["audit.contract"] },
  { label: "Hỗ trợ khách hàng", icon: LifeBuoy, href: "/workspace/support-tickets", permissions: ["ticket.handle", "ticket.manage.all"] },
  { label: "Hóa đơn & thanh toán", icon: Receipt, href: null, permissions: ["finance.manage"] },
  { label: "Mẫu hợp đồng", icon: FileStack, href: null, permissions: ["template.read", "template.manage"] },
  { label: "Hồ sơ pháp lý", icon: Scale, href: null, permissions: ["legal.manage"] },
];

/** Screens a set of permissions opens, in menu order. */
export const screensFor = (permissions: string[]) =>
  NAV_ITEMS.filter(item => item.permissions.length === 0 || item.permissions.some(p => permissions.includes(p)));

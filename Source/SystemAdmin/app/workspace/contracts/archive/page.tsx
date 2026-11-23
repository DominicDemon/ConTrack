import { redirect } from "next/navigation";
/** Kho hợp đồng is part of Quản lý hợp đồng: its list shows every contract in any status. */
export default function ContractArchivePage() { redirect("/workspace/contracts/drafts"); }

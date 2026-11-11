import { ContractList } from "@/components/signed-scans/contract-list";

/** Contracts and the state of their paper signing. */
export default function SignedContractsPage() {
  return (
    <>
      <div>
        <h1 className="text-2xl font-bold tracking-tight text-foreground">Danh sách Hợp đồng</h1>
        <p className="mt-1 text-sm text-muted-foreground">
          Hợp đồng ký ngoài hệ thống: mở hợp đồng, tải bản ký giấy cho phiên bản đã thống nhất, kiểm tra chữ ký và dấu rồi
          xác nhận để ghi nhận hợp đồng đã ký.
        </p>
      </div>
      <ContractList />
    </>
  );
}

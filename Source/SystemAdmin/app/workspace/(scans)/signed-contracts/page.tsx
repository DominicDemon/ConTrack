import { ContractList } from "@/components/signed-scans/contract-list";

/** The paper-signing queue: contracts signed on paper, waiting for their scan or for it to be checked. */
export default function SignedContractsPage() {
  return (
    <>
      <div>
        <h1 className="text-2xl font-bold tracking-tight text-foreground">Bản ký giấy</h1>
        <p className="mt-1 text-sm text-muted-foreground">
          Hợp đồng ký trên giấy (khách doanh nghiệp): in hợp đồng đã thống nhất, hai bên ký và đóng dấu, rồi scan và tải lên tại đây.
          Hành chính kiểm tra chữ ký, con dấu và xác nhận để ghi nhận hợp đồng đã ký. Hợp đồng ký online không nằm trong danh sách này.
        </p>
      </div>
      <ContractList />
    </>
  );
}

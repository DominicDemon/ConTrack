import { ReceiptEntry } from "@/components/billing/receipt-entry";

export default async function Page({ params }: { params: Promise<{ publicId: string }> }) {
  const { publicId } = await params;
  return <ReceiptEntry publicId={publicId} />;
}

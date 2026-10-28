import { PaymentReceipt } from "@/components/billing/payment-receipt";

export default async function Page({ params }: { params: Promise<{ publicId: string }> }) {
  const { publicId } = await params;
  return <PaymentReceipt publicId={publicId} />;
}

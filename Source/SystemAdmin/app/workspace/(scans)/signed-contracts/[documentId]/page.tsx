import { notFound } from "next/navigation";
import { ContractPaperSigningPage } from "./contract-paper-signing-page";

export default async function ContractPage({ params }: { params: Promise<{ documentId: string }> }) {
  const { documentId } = await params;
  const id = Number(documentId);
  if (!Number.isSafeInteger(id) || id <= 0) notFound();
  return <ContractPaperSigningPage documentId={documentId} />;
}

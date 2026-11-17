import { notFound } from "next/navigation";
import { ScannedJobPage } from "@/components/scanned-contracts/scanned-job-page";

export default async function Page({ params }: { params: Promise<{ jobId: string }> }) {
  const { jobId } = await params;
  const id = Number(jobId);
  if (!Number.isSafeInteger(id) || id <= 0) notFound();
  return <ScannedJobPage jobId={jobId} />;
}

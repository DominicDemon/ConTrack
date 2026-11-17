"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { ArrowLeft } from "lucide-react";
import { SignedEvidencePanel } from "@/components/signed-scans/signed-evidence-panel";
import { ContractStatusBadge } from "@/components/signed-scans/status-badge";
import { Button } from "@/components/ui/button";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import type { SignedEvidenceOverview } from "@/services/scans/signed-scans";

/** The contract page of the old app, reduced to its signing tab. */
export function ContractPaperSigningPage({ documentId }: { documentId: string }) {
  const router = useRouter();
  const [overview, setOverview] = useState<SignedEvidenceOverview | null>(null);

  return (
    <>
      <div>
        <Button variant="ghost" className="mb-2 -ml-3 text-muted-foreground" onClick={() => router.push("/workspace/signed-contracts")}>
          <ArrowLeft className="mr-2 size-4" />
          Quay lại danh sách
        </Button>
        <div className="flex flex-wrap items-center gap-2">
          <h1 className="text-2xl font-bold tracking-tight text-foreground">
            {overview ? (overview.documentCode ?? "Chưa cấp mã") : `Hợp đồng #${documentId}`}
          </h1>
          {overview && <ContractStatusBadge status={overview.contractStatus} />}
        </div>
        {overview && <p className="mt-2 max-w-3xl text-sm text-muted-foreground">{overview.documentName}</p>}
      </div>
      <Tabs defaultValue="signature" className="space-y-4">
        <TabsList className="flex h-auto w-full flex-wrap justify-start">
          <TabsTrigger value="signature">Ký hợp đồng</TabsTrigger>
        </TabsList>
        <TabsContent value="signature">
          <SignedEvidencePanel documentId={documentId} onOverviewChange={setOverview} />
        </TabsContent>
      </Tabs>
    </>
  );
}

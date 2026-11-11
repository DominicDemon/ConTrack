import type * as React from "react";
import { Badge } from "@/components/ui/badge";
import { cn } from "@/lib/utils";
import { ContractStatus, statusClasses } from "@/services/contract-api";
import type { ContractStatusKey, SignedEvidenceStatus } from "@/services/scans/signed-scans";
import { contractStatusLabel, EVIDENCE_STATUS_LABELS, type StatusTone } from "./format";

// The colours of the old app's status badges (services/contract-api.ts statusClasses).
const TONE_CLASSES: Record<StatusTone, string> = {
  success:
    "bg-emerald-50 text-emerald-700 border-emerald-200 dark:bg-emerald-500/10 dark:text-emerald-400 dark:border-emerald-500/20",
  pending:
    "bg-amber-50 text-amber-700 border-amber-200 dark:bg-amber-500/10 dark:text-amber-400 dark:border-amber-500/20",
  info: "bg-blue-50 text-blue-700 border-blue-200 dark:bg-blue-500/10 dark:text-blue-400 dark:border-blue-500/20",
  neutral:
    "bg-slate-50 text-slate-700 border-slate-200 dark:bg-slate-500/10 dark:text-slate-400 dark:border-slate-500/20",
  danger: "bg-red-50 text-red-700 border-red-200 dark:bg-red-500/10 dark:text-red-400 dark:border-red-500/20",
};

/** A status always reads as words; the color only backs them up. */
export function StatusBadge({
  tone,
  children,
  className,
}: {
  tone: StatusTone;
  children: React.ReactNode;
  className?: string;
}) {
  return (
    <Badge variant="outline" className={cn(TONE_CLASSES[tone], className)}>
      {children}
    </Badge>
  );
}

/** The API sends the contract status as text; "Failed" is the old app's "Rejected". */
const OLD_STATUS: Partial<Record<ContractStatusKey, ContractStatus>> = {
  Draft: ContractStatus.Draft,
  Negotiating: ContractStatus.Negotiating,
  PendingApproval: ContractStatus.PendingApproval,
  PendingSignature: ContractStatus.PendingSignature,
  Signed: ContractStatus.Signed,
  Completed: ContractStatus.Completed,
  Cancelled: ContractStatus.Cancelled,
  Failed: ContractStatus.Rejected,
};

/** The contract status badge of the old app. */
export function ContractStatusBadge({ status }: { status: string }) {
  const old = OLD_STATUS[status as ContractStatusKey];
  return (
    <Badge variant="outline" className={old === undefined ? TONE_CLASSES.neutral : statusClasses[old]}>
      {contractStatusLabel(status)}
    </Badge>
  );
}

const EVIDENCE_TONES: Record<SignedEvidenceStatus, StatusTone> = {
  PendingReview: "pending",
  Accepted: "success",
  Rejected: "danger",
  Superseded: "neutral",
};

export function EvidenceStatusBadge({ status }: { status: SignedEvidenceStatus }) {
  return <StatusBadge tone={EVIDENCE_TONES[status]}>{EVIDENCE_STATUS_LABELS[status]}</StatusBadge>;
}


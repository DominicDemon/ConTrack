import type * as React from "react";
import { Badge } from "@/components/ui/badge";
import { cn } from "@/lib/utils";
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

/** Contract status badge colours matching the contract portal palette. */
const CONTRACT_STATUS_CLASSES: Partial<Record<ContractStatusKey, string>> = {
  Draft:
    "bg-amber-50 text-amber-700 border-amber-200 dark:bg-amber-500/10 dark:text-amber-400 dark:border-amber-500/20",
  Negotiating:
    "bg-blue-50 text-blue-700 border-blue-200 dark:bg-blue-500/10 dark:text-blue-400 dark:border-blue-500/20",
  PendingApproval:
    "bg-indigo-50 text-indigo-700 border-indigo-200 dark:bg-indigo-500/10 dark:text-indigo-400 dark:border-indigo-500/20",
  PendingSignature:
    "bg-purple-50 text-purple-700 border-purple-200 dark:bg-purple-500/10 dark:text-purple-400 dark:border-purple-500/20",
  Signed:
    "bg-emerald-50 text-emerald-700 border-emerald-200 dark:bg-emerald-500/10 dark:text-emerald-400 dark:border-emerald-500/20",
  Completed:
    "bg-slate-50 text-slate-700 border-slate-200 dark:bg-slate-500/10 dark:text-slate-400 dark:border-slate-500/20",
  Cancelled:
    "bg-red-50 text-red-700 border-red-200 dark:bg-red-500/10 dark:text-red-400 dark:border-red-500/20",
  Failed:
    "bg-red-50 text-red-700 border-red-200 dark:bg-red-500/10 dark:text-red-400 dark:border-red-500/20",
};

/** The contract status badge of the contract portal. */
export function ContractStatusBadge({ status }: { status: string }) {
  const classes = CONTRACT_STATUS_CLASSES[status as ContractStatusKey];
  return (
    <Badge variant="outline" className={classes ?? TONE_CLASSES.neutral}>
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


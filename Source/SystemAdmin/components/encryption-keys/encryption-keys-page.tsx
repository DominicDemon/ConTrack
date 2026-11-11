"use client";

import { useCallback, useEffect, useState } from "react";
import { Check, Copy, FileKey2, KeyRound, Loader2 } from "lucide-react";
import { formatDateTime } from "@/components/signed-scans/format";
import { StatusBadge } from "@/components/signed-scans/status-badge";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Skeleton } from "@/components/ui/skeleton";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { toast } from "@/components/ui/sonner";
import { encryptionKeysApi, type EmployeeKeyStatus, type IssuedKey } from "@/services/scans/encryption-keys";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { getScanErrorMessage } from "@/services/scans/http";

/** The key is shown once; the administrator gives it to the employee in person or by a safe channel. */
function IssuedKeyDialog({ issued, onClose }: { issued: IssuedKey | null; onClose: () => void }) {
  const [copied, setCopied] = useState(false);

  const copy = async () => {
    if (!issued) return;
    try {
      await navigator.clipboard.writeText(issued.key);
      setCopied(true);
    } catch {
      toast.error("Không sao chép được. Hãy chép tay key.");
    }
  };

  return (
    <Dialog
      open={issued !== null}
      onOpenChange={(open) => {
        if (!open) {
          setCopied(false);
          onClose();
        }
      }}
    >
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>Key của {issued?.displayName}</DialogTitle>
          <DialogDescription>
            Key chỉ hiện một lần. Đưa trực tiếp cho nhân viên; hệ thống không lưu key nên không xem lại được.
          </DialogDescription>
        </DialogHeader>
        <div className="flex items-center justify-between gap-2 rounded-lg border bg-muted/40 px-4 py-3">
          <code className="font-mono text-xl tracking-widest break-all select-all">{issued?.key}</code>
          <Button variant="outline" size="icon" aria-label="Sao chép key" onClick={() => void copy()}>
            {copied ? <Check className="size-4" /> : <Copy className="size-4" />}
          </Button>
        </div>
        {issued?.recoveredFiles != null ? (
          <p className="text-sm text-emerald-700 dark:text-emerald-400">
            {issued.recoveredFiles > 0
              ? `Key mới đã mở được ${issued.recoveredFiles} tệp cũ của nhân viên.`
              : "Key mới đã mở được mọi tệp cũ của nhân viên."}
          </p>
        ) : (
          <p className="text-xs text-muted-foreground">
            Mất key thì cấp key mới, rồi bấm “Mở lại tệp cũ” để key mới mở được các tệp cũ.
          </p>
        )}
        <DialogFooter>
          <Button onClick={onClose}>Đã đưa key</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

/**
 * After a key is reissued, the administrator gives the company recovery private key (recovery-private.pem) so the
 * employee's new key opens the files their old key opened. The file goes with this one request and is not kept.
 */
function RecoverFilesDialog({
  employee,
  onClose,
  onDone,
}: {
  employee: EmployeeKeyStatus | null;
  onClose: () => void;
  onDone: () => void;
}) {
  const [file, setFile] = useState<File | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const close = () => {
    if (busy) return;
    setFile(null);
    setError(null);
    onClose();
  };

  const submit = async () => {
    if (!employee || !file) return;
    setBusy(true);
    setError(null);
    try {
      const result = await encryptionKeysApi.recoverFiles(employee.employeeId, file);
      const parts = [`Đã mở lại ${result.granted} tệp cho key mới của ${employee.displayName}.`];
      if (result.notRecoverable > 0) parts.push(`${result.notRecoverable} tệp lưu trước khi có khóa dự phòng này nên không mở lại được.`);
      toast.success(parts.join(" "));
      setFile(null);
      onDone();
      onClose();
    } catch (recoverError) {
      setError(getScanErrorMessage(recoverError, "Không mở lại được tệp."));
    } finally {
      setBusy(false);
    }
  };

  return (
    <Dialog open={employee !== null} onOpenChange={(open) => !open && close()}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>Mở lại tệp cũ cho {employee?.displayName}</DialogTitle>
          <DialogDescription>
            Chọn tệp khóa dự phòng của công ty (recovery-private.pem). Hệ thống dùng nó để cấp lại cho key mới đúng những tệp
            key cũ của nhân viên đã mở được, rồi bỏ đi ngay — không lưu lại.
          </DialogDescription>
        </DialogHeader>
        {employee && (
          <p className="text-sm">
            Số tệp chưa mở được bằng key mới: <span className="font-semibold">{employee.filesToRecover}</span>
          </p>
        )}
        <div className="space-y-2">
          <Label htmlFor="recovery-key-file">Khóa dự phòng</Label>
          <Input
            id="recovery-key-file"
            type="file"
            accept=".pem"
            disabled={busy}
            onChange={(event) => {
              setFile(event.target.files?.[0] ?? null);
              setError(null);
            }}
          />
        </div>
        {error && (
          <Alert variant="destructive">
            <AlertDescription>{error}</AlertDescription>
          </Alert>
        )}
        <DialogFooter>
          <Button variant="outline" disabled={busy} onClick={close}>
            Hủy
          </Button>
          <Button disabled={!file || busy} onClick={() => void submit()}>
            {busy && <Loader2 className="size-4 animate-spin" />}
            Mở lại tệp
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

/** Administrators issue each employee the 12-character key that opens encrypted scans. */
export function EncryptionKeysPage() {
  const [rows, setRows] = useState<EmployeeKeyStatus[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [confirming, setConfirming] = useState<EmployeeKeyStatus | null>(null);
  const [issuingId, setIssuingId] = useState<string | null>(null);
  const [issued, setIssued] = useState<IssuedKey | null>(null);
  const [recovering, setRecovering] = useState<EmployeeKeyStatus | null>(null);
  const [recoveringId, setRecoveringId] = useState<string | null>(null);
  const [serverKeepsRecoveryKey, setServerKeepsRecoveryKey] = useState(false);

  const load = useCallback(async () => {
    try {
      setRows(await encryptionKeysApi.list());
      setError(null);
    } catch (loadError) {
      setError(getScanErrorMessage(loadError, "Không tải được danh sách nhân viên."));
    }
  }, []);

  useEffect(() => {
    void load();
    encryptionKeysApi
      .mine()
      .then((mine) => setServerKeepsRecoveryKey(mine.serverKeepsRecoveryKey))
      .catch(() => setServerKeepsRecoveryKey(false));
  }, [load]);

  /** One click when the server keeps the recovery key; otherwise the administrator picks the key file. */
  const recover = async (row: EmployeeKeyStatus) => {
    if (!serverKeepsRecoveryKey) {
      setRecovering(row);
      return;
    }
    setRecoveringId(row.employeeId);
    try {
      const result = await encryptionKeysApi.recoverFiles(row.employeeId);
      const parts = [`Đã mở lại ${result.granted} tệp cũ cho ${row.displayName}.`];
      if (result.notRecoverable > 0) parts.push(`${result.notRecoverable} tệp lưu trước khi có khóa dự phòng nên không mở lại được.`);
      toast.success(parts.join(" "));
      void load();
    } catch (recoverError) {
      toast.error(getScanErrorMessage(recoverError, "Không mở lại được tệp."));
    } finally {
      setRecoveringId(null);
    }
  };

  const issue = async (row: EmployeeKeyStatus) => {
    setConfirming(null);
    setIssuingId(row.employeeId);
    try {
      setIssued(await encryptionKeysApi.issue(row.employeeId));
      void load();
    } catch (issueError) {
      toast.error(getScanErrorMessage(issueError, "Không cấp được key."));
    } finally {
      setIssuingId(null);
    }
  };

  return (
    <>
      <div>
        <h1 className="flex items-center gap-2 text-2xl font-bold tracking-tight text-foreground">
          <KeyRound className="size-6 text-primary" />
          Key mở tệp
        </h1>
        <p className="mt-1 max-w-3xl text-sm text-muted-foreground">
          Bản scan được mã hóa khi lưu. Mỗi nhân viên cần một key 12 ký tự để mở tệp mình được phép xem. Cấp lại key thì key
          cũ thôi dùng được.
        </p>
      </div>

      {error && (
        <Alert variant="destructive">
          <AlertTitle>Không tải được</AlertTitle>
          <AlertDescription>{error}</AlertDescription>
        </Alert>
      )}

      <Card>
        <CardHeader>
          <CardTitle>Nhân viên</CardTitle>
          <CardDescription>Nhân viên đang hoạt động và tình trạng key của họ.</CardDescription>
        </CardHeader>
        <CardContent>
          {!rows ? (
            <Skeleton className="h-24 w-full" />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Nhân viên</TableHead>
                  <TableHead>Key</TableHead>
                  <TableHead className="hidden sm:table-cell">Cấp lúc</TableHead>
                  <TableHead className="pr-4 text-right">Thao tác</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {rows.map((row) => (
                  <TableRow key={row.employeeId}>
                    <TableCell>
                      <span className="font-medium">{row.displayName}</span>
                      <span className="block text-xs text-muted-foreground">{row.username}</span>
                    </TableCell>
                    <TableCell>
                      {row.hasKey ? <StatusBadge tone="success">Đã cấp</StatusBadge> : <StatusBadge tone="neutral">Chưa cấp</StatusBadge>}
                      {row.filesToRecover > 0 && (
                        <span className="mt-1 block text-xs text-amber-600 dark:text-amber-400">
                          {row.filesToRecover} tệp cũ chưa mở được
                        </span>
                      )}
                    </TableCell>
                    <TableCell className="hidden text-sm text-muted-foreground sm:table-cell">
                      {row.issuedAt ? `${formatDateTime(row.issuedAt)}${row.issuedBy ? ` · ${row.issuedBy}` : ""}` : "—"}
                    </TableCell>
                    <TableCell className="space-x-2 pr-4 text-right whitespace-nowrap">
                      {row.filesToRecover > 0 && (
                        <Button
                          variant="outline"
                          size="sm"
                          disabled={issuingId !== null || recoveringId !== null}
                          onClick={() => void recover(row)}
                        >
                          {recoveringId === row.employeeId ? <Loader2 className="size-4 animate-spin" /> : <FileKey2 className="size-4" />}
                          Mở lại tệp cũ
                        </Button>
                      )}
                      <Button
                        variant="outline"
                        size="sm"
                        disabled={issuingId !== null}
                        onClick={() => (row.hasKey ? setConfirming(row) : void issue(row))}
                      >
                        {issuingId === row.employeeId && <Loader2 className="size-4 animate-spin" />}
                        {row.hasKey ? "Cấp lại" : "Cấp key"}
                      </Button>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      <AlertDialog open={confirming !== null} onOpenChange={(open) => !open && setConfirming(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Cấp lại key cho {confirming?.displayName}?</AlertDialogTitle>
            <AlertDialogDescription>
              {serverKeepsRecoveryKey
                ? "Key cũ thôi dùng được ngay. Key mới tự mở được các tệp mà key cũ đã mở."
                : "Key cũ thôi dùng được ngay. Sau khi cấp, bấm “Mở lại tệp cũ” và chọn khóa dự phòng của công ty để key mới mở được các tệp key cũ đã mở."}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Hủy</AlertDialogCancel>
            <AlertDialogAction onClick={() => confirming && void issue(confirming)}>Cấp lại</AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      <IssuedKeyDialog issued={issued} onClose={() => setIssued(null)} />
      <RecoverFilesDialog employee={recovering} onClose={() => setRecovering(null)} onDone={() => void load()} />
    </>
  );
}

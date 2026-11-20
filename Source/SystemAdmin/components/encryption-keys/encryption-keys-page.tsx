"use client";

import { useCallback, useEffect, useState } from "react";
import { FileKey2, KeyRound, Loader2 } from "lucide-react";
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
import { encryptionKeysApi, type EmployeeKeyStatus } from "@/services/scans/encryption-keys";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { getScanErrorMessage } from "@/services/scans/http";

/**
 * After an employee sets a new key (they forgot the old one), the administrator gives the company recovery private key (recovery-private.pem) so the
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

/**
 * Each employee sets their own key on "Key của tôi"; the administrator sees who has one, reopens old files for a new
 * key when the server does not keep the recovery key, and revokes a key (an employee who leaves, a key someone else knows).
 */
export function EncryptionKeysPage() {
  const [rows, setRows] = useState<EmployeeKeyStatus[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [confirming, setConfirming] = useState<EmployeeKeyStatus | null>(null);
  const [revokingId, setRevokingId] = useState<string | null>(null);
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

  const revoke = async (row: EmployeeKeyStatus) => {
    setConfirming(null);
    setRevokingId(row.employeeId);
    try {
      await encryptionKeysApi.revoke(row.employeeId);
      toast.success(`Đã thu hồi key của ${row.displayName}.`);
      void load();
    } catch (revokeError) {
      toast.error(getScanErrorMessage(revokeError, "Không thu hồi được key."));
    } finally {
      setRevokingId(null);
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
          Bản scan được mã hóa khi lưu. Mỗi nhân viên được xem bản scan tự tạo key của mình ở mục “Key của tôi” (xác nhận
          bằng mật khẩu đăng nhập và mã gửi qua email); quản trị không biết key của ai. Thu hồi key khi nhân viên nghỉ việc
          hoặc key có thể đã lộ.
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
          <CardDescription>Nhân viên được xem bản scan, hoặc đang giữ key, và tình trạng key của họ.</CardDescription>
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
                  <TableHead className="hidden sm:table-cell">Đặt lúc</TableHead>
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
                      {row.hasKey ? <StatusBadge tone="success">Đã tạo</StatusBadge> : <StatusBadge tone="neutral">Chưa tạo</StatusBadge>}
                      {row.hasKey && !row.mayHoldKey && (
                        <span className="mt-1 block text-xs text-amber-600 dark:text-amber-400">Không còn quyền xem bản scan</span>
                      )}
                      {row.filesToRecover > 0 && (
                        <span className="mt-1 block text-xs text-amber-600 dark:text-amber-400">
                          {row.filesToRecover} tệp cũ chưa mở được
                        </span>
                      )}
                    </TableCell>
                    <TableCell className="hidden text-sm text-muted-foreground sm:table-cell">
                      {row.setAt ? formatDateTime(row.setAt) : "—"}
                    </TableCell>
                    <TableCell className="space-x-2 pr-4 text-right whitespace-nowrap">
                      {row.filesToRecover > 0 && (
                        <Button
                          variant="outline"
                          size="sm"
                          disabled={revokingId !== null || recoveringId !== null}
                          onClick={() => void recover(row)}
                        >
                          {recoveringId === row.employeeId ? <Loader2 className="size-4 animate-spin" /> : <FileKey2 className="size-4" />}
                          Mở lại tệp cũ
                        </Button>
                      )}
                      {row.hasKey && (
                        <Button
                          variant="outline"
                          size="sm"
                          disabled={revokingId !== null}
                          onClick={() => setConfirming(row)}
                        >
                          {revokingId === row.employeeId && <Loader2 className="size-4 animate-spin" />}
                          Thu hồi
                        </Button>
                      )}
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
            <AlertDialogTitle>Thu hồi key của {confirming?.displayName}?</AlertDialogTitle>
            <AlertDialogDescription>
              Key thôi mở được tệp ngay. Tệp vẫn còn nguyên: người khác vẫn mở được, và nếu nhân viên còn quyền xem bản scan,
              họ có thể tự tạo key mới ở mục “Key của tôi”.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Hủy</AlertDialogCancel>
            <AlertDialogAction onClick={() => confirming && void revoke(confirming)}>Thu hồi</AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      <RecoverFilesDialog employee={recovering} onClose={() => setRecovering(null)} onDone={() => void load()} />
    </>
  );
}

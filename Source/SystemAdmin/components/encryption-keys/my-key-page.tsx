"use client";

import Link from "next/link";
import { useEffect, useId, useState, type FormEvent } from "react";
import { Check, Eye, EyeOff, KeyRound, Loader2, MailCheck, X } from "lucide-react";
import { useFileKey } from "@/components/internal/file-key";
import { formatDateTime } from "@/components/signed-scans/format";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Skeleton } from "@/components/ui/skeleton";
import { toast } from "@/components/ui/sonner";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { FILE_KEY_MAX, fileKeyRules, isGoodFileKey } from "@/lib/file-key-policy";
import { encryptionKeysApi, type MyFileKey, type SetFileKeyResult } from "@/services/scans/encryption-keys";
import { getScanErrorMessage } from "@/services/scans/http";

/** A password-like field with a show/hide button. */
function SecretInput({
  id,
  value,
  onChange,
  disabled,
  autoComplete = "off",
  maxLength = FILE_KEY_MAX,
}: {
  id: string;
  value: string;
  onChange: (value: string) => void;
  disabled?: boolean;
  autoComplete?: string;
  maxLength?: number;
}) {
  const [visible, setVisible] = useState(false);
  return (
    <div className="flex gap-2">
      <Input
        id={id}
        type={visible ? "text" : "password"}
        autoComplete={autoComplete}
        spellCheck={false}
        maxLength={maxLength}
        value={value}
        disabled={disabled}
        onChange={(event) => onChange(event.target.value)}
        className="font-mono"
      />
      <Button type="button" variant="outline" size="icon" aria-label={visible ? "Ẩn" : "Hiện"} onClick={() => setVisible(!visible)}>
        {visible ? <EyeOff className="size-4" /> : <Eye className="size-4" />}
      </Button>
    </div>
  );
}

/** The new key typed twice, with the rules ticked off while typing. */
function NewKeyFields({
  value,
  confirm,
  onValue,
  onConfirm,
  disabled,
}: {
  value: string;
  confirm: string;
  onValue: (value: string) => void;
  onConfirm: (value: string) => void;
  disabled?: boolean;
}) {
  const keyId = useId();
  const confirmId = useId();
  const rules = fileKeyRules(value);
  return (
    <>
      <div className="space-y-2">
        <Label htmlFor={keyId}>Key mới</Label>
        <SecretInput id={keyId} value={value} onChange={onValue} disabled={disabled} autoComplete="new-password" />
        <ul className="space-y-1 text-xs" aria-live="polite">
          {rules.map((rule) => (
            <li key={rule.label} className={`flex items-center gap-1.5 ${rule.ok ? "text-emerald-700 dark:text-emerald-400" : "text-muted-foreground"}`}>
              {rule.ok ? <Check className="size-3.5" /> : <X className="size-3.5" />}
              {rule.label}
            </li>
          ))}
        </ul>
      </div>
      <div className="space-y-2">
        <Label htmlFor={confirmId}>Nhập lại key mới</Label>
        <SecretInput id={confirmId} value={confirm} onChange={onConfirm} disabled={disabled} autoComplete="new-password" />
        {confirm.length > 0 && confirm !== value && <p className="text-xs text-destructive">Hai lần nhập chưa giống nhau.</p>}
      </div>
    </>
  );
}

const newKeyProblem = (value: string, confirm: string) =>
  !isGoodFileKey(value) ? "Key mới chưa đạt các yêu cầu bên dưới ô nhập." : value !== confirm ? "Hai lần nhập key mới chưa giống nhau." : null;

const doneMessage = (result: SetFileKeyResult) =>
  result.recoveredFiles != null && result.recoveredFiles > 0 ? ` Key mới đã mở lại được ${result.recoveredFiles} tệp cũ.` : "";

/**
 * Creating the first key, or a new one after forgetting the old: the login password, a code sent to the employee's
 * email, then the new key.
 */
function VerifiedKeyForm({ mine, replacing, onDone }: { mine: MyFileKey; replacing: boolean; onDone: () => void }) {
  const passwordId = useId();
  const codeId = useId();
  const [password, setPassword] = useState("");
  const [code, setCode] = useState("");
  const [value, setValue] = useState("");
  const [confirm, setConfirm] = useState("");
  const [sentTo, setSentTo] = useState<string | null>(null);
  const [wait, setWait] = useState(0);
  const [busy, setBusy] = useState(false);
  const [problem, setProblem] = useState<string | null>(null);

  useEffect(() => {
    if (wait <= 0) return;
    const timer = setTimeout(() => setWait(wait - 1), 1000);
    return () => clearTimeout(timer);
  }, [wait]);

  const sendCode = async () => {
    if (!password) {
      setProblem("Nhập mật khẩu đăng nhập trước.");
      return;
    }
    setBusy(true);
    setProblem(null);
    try {
      setSentTo(await encryptionKeysApi.sendCode(password));
      setWait(60);
    } catch (sendError) {
      setProblem(getScanErrorMessage(sendError, "Không gửi được mã."));
    } finally {
      setBusy(false);
    }
  };

  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const wrong = !/^\d{6}$/.test(code.trim()) ? "Nhập mã 6 số đã gửi tới email." : newKeyProblem(value, confirm);
    if (wrong) {
      setProblem(wrong);
      return;
    }
    setBusy(true);
    setProblem(null);
    try {
      const result = await encryptionKeysApi.setKey(password, code.trim(), value);
      toast.success((replacing ? "Đã đặt key mới. Key cũ không còn dùng được." : "Đã tạo key.") + doneMessage(result));
      onDone();
    } catch (setError) {
      setProblem(getScanErrorMessage(setError, "Không đặt được key."));
    } finally {
      setBusy(false);
    }
  };

  if (!mine.email) {
    return (
      <Alert>
        <AlertTitle>Chưa có email nhận mã</AlertTitle>
        <AlertDescription>
          Tài khoản của bạn chưa có email hợp lệ. Thêm email ở{" "}
          <Link href="/workspace/profile" className="text-primary hover:underline">Hồ sơ của tôi</Link> rồi quay lại.
        </AlertDescription>
      </Alert>
    );
  }

  return (
    <form onSubmit={(event) => void submit(event)} className="max-w-md space-y-4">
      <div className="space-y-2">
        <Label htmlFor={passwordId}>Mật khẩu đăng nhập</Label>
        <SecretInput id={passwordId} value={password} onChange={setPassword} disabled={busy || sentTo !== null} autoComplete="current-password" maxLength={200} />
      </div>
      {sentTo === null ? (
        <Button type="button" disabled={busy} onClick={() => void sendCode()}>
          {busy ? <Loader2 className="size-4 animate-spin" /> : <MailCheck className="size-4" />}
          Gửi mã xác nhận tới {mine.email}
        </Button>
      ) : (
        <>
          <div className="space-y-2">
            <Label htmlFor={codeId}>Mã xác nhận (6 số, gửi tới {sentTo})</Label>
            <div className="flex gap-2">
              <Input
                id={codeId}
                inputMode="numeric"
                autoComplete="one-time-code"
                maxLength={6}
                value={code}
                disabled={busy}
                onChange={(event) => setCode(event.target.value.replace(/\D/g, ""))}
                className="w-32 font-mono tracking-widest"
              />
              <Button type="button" variant="outline" disabled={busy || wait > 0} onClick={() => void sendCode()}>
                {wait > 0 ? `Gửi lại sau ${wait} giây` : "Gửi lại mã"}
              </Button>
            </div>
            <p className="text-xs text-muted-foreground">Mã có hiệu lực 5 phút. Không thấy thư thì xem cả mục Spam.</p>
          </div>
          <NewKeyFields value={value} confirm={confirm} onValue={setValue} onConfirm={setConfirm} disabled={busy} />
          <Button type="submit" disabled={busy}>
            {busy && <Loader2 className="size-4 animate-spin" />}
            {replacing ? "Đặt key mới" : "Tạo key"}
          </Button>
        </>
      )}
      {problem && (
        <p role="alert" className="text-sm text-destructive">
          {problem}
        </p>
      )}
    </form>
  );
}

/** Changing the key while it is still known: every file stays open. */
function ChangeKeyForm({ onDone }: { onDone: () => void }) {
  const currentId = useId();
  const [current, setCurrent] = useState("");
  const [value, setValue] = useState("");
  const [confirm, setConfirm] = useState("");
  const [busy, setBusy] = useState(false);
  const [problem, setProblem] = useState<string | null>(null);

  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const wrong = !current ? "Nhập key hiện tại." : newKeyProblem(value, confirm) ?? (value === current ? "Key mới phải khác key hiện tại." : null);
    if (wrong) {
      setProblem(wrong);
      return;
    }
    setBusy(true);
    setProblem(null);
    try {
      await encryptionKeysApi.changeKey(current, value);
      toast.success("Đã đổi key. Từ giờ mở tệp bằng key mới.");
      onDone();
    } catch (changeError) {
      setProblem(getScanErrorMessage(changeError, "Không đổi được key."));
    } finally {
      setBusy(false);
    }
  };

  return (
    <form onSubmit={(event) => void submit(event)} className="max-w-md space-y-4">
      <div className="space-y-2">
        <Label htmlFor={currentId}>Key hiện tại</Label>
        <SecretInput id={currentId} value={current} onChange={setCurrent} disabled={busy} />
      </div>
      <NewKeyFields value={value} confirm={confirm} onValue={setValue} onConfirm={setConfirm} disabled={busy} />
      <Button type="submit" disabled={busy}>
        {busy && <Loader2 className="size-4 animate-spin" />}
        Đổi key
      </Button>
      {problem && (
        <p role="alert" className="text-sm text-destructive">
          {problem}
        </p>
      )}
    </form>
  );
}

/** Page "Key của tôi": each employee who can see scans creates, changes or resets their own key. */
export function MyKeyPage() {
  const { forgetKey } = useFileKey();
  const [mine, setMine] = useState<MyFileKey | null>(null);
  const [error, setError] = useState<string | null>(null);
  // Remounts the forms after a change, so nothing typed stays in them.
  const [round, setRound] = useState(0);

  useEffect(() => {
    encryptionKeysApi.mine().then(
      (loaded) => {
        setMine(loaded);
        setError(null);
      },
      (loadError: unknown) => setError(getScanErrorMessage(loadError, "Không tải được thông tin key.")),
    );
  }, [round]);

  const done = () => {
    forgetKey();
    setRound((value) => value + 1);
  };

  return (
    <>
      <div>
        <h1 className="flex items-center gap-2 text-2xl font-bold tracking-tight text-foreground">
          <KeyRound className="size-6 text-primary" />
          Key của tôi
        </h1>
        <p className="mt-1 max-w-3xl text-sm text-muted-foreground">
          Bản scan được mã hóa khi lưu. Bạn tự đặt key để mở các tệp mình được xem; chỉ bạn biết key, hệ thống và quản trị
          không xem được. Không ghi key lên giấy dán ở bàn hay gửi qua chat.
        </p>
      </div>

      {error && (
        <Alert variant="destructive">
          <AlertTitle>Không tải được</AlertTitle>
          <AlertDescription>{error}</AlertDescription>
        </Alert>
      )}

      {!mine ? (
        !error && <Skeleton className="h-40 w-full" />
      ) : !mine.mayHoldKey ? (
        <Alert>
          <AlertDescription>Bạn không có quyền xem bản scan nên không cần key mở tệp.</AlertDescription>
        </Alert>
      ) : !mine.hasKey ? (
        <Card>
          <CardHeader>
            <CardTitle>Tạo key</CardTitle>
            <CardDescription>
              Xác nhận bằng mật khẩu đăng nhập và mã gửi tới email của bạn, rồi đặt key. Bạn sẽ nhập key này mỗi khi mở bản scan.
            </CardDescription>
          </CardHeader>
          <CardContent>
            <VerifiedKeyForm key={round} mine={mine} replacing={false} onDone={done} />
          </CardContent>
        </Card>
      ) : (
        <Card>
          <CardHeader>
            <CardTitle>Key đang dùng</CardTitle>
            <CardDescription>{mine.setAt ? `Đặt lúc ${formatDateTime(mine.setAt)}.` : "Bạn đã có key."}</CardDescription>
          </CardHeader>
          <CardContent>
            <Tabs defaultValue="change">
              <TabsList>
                <TabsTrigger value="change">Đổi key</TabsTrigger>
                <TabsTrigger value="forgot">Quên key</TabsTrigger>
              </TabsList>
              <TabsContent value="change" className="pt-4">
                <p className="mb-4 text-sm text-muted-foreground">Còn nhớ key hiện tại: đổi sang key mới, mọi tệp vẫn mở được.</p>
                <ChangeKeyForm key={round} onDone={done} />
              </TabsContent>
              <TabsContent value="forgot" className="pt-4">
                <p className="mb-4 text-sm text-muted-foreground">
                  Quên key: xác nhận bằng mật khẩu đăng nhập và mã gửi qua email rồi đặt key mới; key cũ thôi dùng được.
                  {mine.serverKeepsRecoveryKey
                    ? " Các tệp cũ được mở lại cho key mới ngay."
                    : " Các tệp cũ sẽ mở lại được khi quản trị dùng khóa dự phòng, hoặc khi đồng nghiệp mở tệp đó."}
                </p>
                <VerifiedKeyForm key={round} mine={mine} replacing onDone={done} />
              </TabsContent>
            </Tabs>
          </CardContent>
        </Card>
      )}
    </>
  );
}

"use client";

import { useEffect, useId, useState, type FormEvent } from "react";
import Link from "next/link";
import { Eye, EyeOff, KeyRound, Loader2, Mail, MailCheck, UserRound } from "lucide-react";
import { useInternalUser } from "@/components/internal/internal-shell";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Skeleton } from "@/components/ui/skeleton";
import { toast } from "@/components/ui/sonner";
import { dateTime, internalApi } from "@/services/internal-api";

/** GET /api/internal/profile. */
interface Profile {
  code: string;
  username: string;
  name: string;
  email: string | null;
  emailVerifiedAt: string | null;
  phone: string | null;
  lastLoginAt: string | null;
}

/** Same rule as the server (EmployeeAdminRules.IsValidPassword). */
const passwordProblem = (value: string) =>
  value.length < 8 || value.length > 100 || !/\p{L}/u.test(value) || !/\d/.test(value)
    ? "Mật khẩu mới cần từ 8 đến 100 ký tự, có cả chữ và số."
    : null;

const message = (error: unknown, fallback: string) => (error instanceof Error && error.message ? error.message : fallback);

function SecretInput({ id, value, onChange, disabled, autoComplete }: {
  id: string;
  value: string;
  onChange: (value: string) => void;
  disabled?: boolean;
  autoComplete: string;
}) {
  const [visible, setVisible] = useState(false);
  return (
    <div className="flex gap-2">
      <Input id={id} type={visible ? "text" : "password"} autoComplete={autoComplete} maxLength={200} value={value} disabled={disabled}
        onChange={(event) => onChange(event.target.value)} />
      <Button type="button" variant="outline" size="icon" aria-label={visible ? "Ẩn" : "Hiện"} onClick={() => setVisible(!visible)}>
        {visible ? <EyeOff className="size-4" /> : <Eye className="size-4" />}
      </Button>
    </div>
  );
}

/** New email: login password, then a code sent to the new address proves it is the employee's. */
function EmailForm({ profile, onSaved }: { profile: Profile; onSaved: (profile: Profile) => void }) {
  const emailId = useId();
  const passwordId = useId();
  const codeId = useId();
  const [newEmail, setNewEmail] = useState("");
  const [password, setPassword] = useState("");
  const [code, setCode] = useState("");
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
    const address = newEmail.trim();
    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(address)) return setProblem("Nhập email mới hợp lệ.");
    if (!password) return setProblem("Nhập mật khẩu đăng nhập.");
    setBusy(true);
    setProblem(null);
    try {
      setSentTo((await internalApi<{ sentTo: string }>("/profile/email/code", "POST", { password, newEmail: address })).sentTo);
      setWait(60);
    } catch (error) {
      setProblem(message(error, "Không gửi được mã."));
    } finally {
      setBusy(false);
    }
  };

  const confirm = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (!/^\d{6}$/.test(code.trim())) return setProblem("Nhập mã 6 số đã gửi tới email mới.");
    setBusy(true);
    setProblem(null);
    try {
      onSaved(await internalApi<Profile>("/profile/email", "POST", { code: code.trim() }));
      toast.success("Đã đổi email.");
    } catch (error) {
      setProblem(message(error, "Không đổi được email."));
    } finally {
      setBusy(false);
    }
  };

  return (
    <form onSubmit={(event) => void confirm(event)} className="max-w-md space-y-4">
      <div className="space-y-2">
        <Label htmlFor={emailId}>{profile.email ? "Email mới" : "Email"}</Label>
        <Input id={emailId} type="email" autoComplete="email" maxLength={100} value={newEmail} disabled={busy || sentTo !== null}
          onChange={(event) => setNewEmail(event.target.value)} placeholder="ten@congty.vn" />
      </div>
      <div className="space-y-2">
        <Label htmlFor={passwordId}>Mật khẩu đăng nhập</Label>
        <SecretInput id={passwordId} value={password} onChange={setPassword} disabled={busy || sentTo !== null} autoComplete="current-password" />
      </div>
      {sentTo === null ? (
        <Button type="button" disabled={busy} onClick={() => void sendCode()}>
          {busy ? <Loader2 className="size-4 animate-spin" /> : <MailCheck className="size-4" />}
          Gửi mã tới email mới
        </Button>
      ) : (
        <>
          <div className="space-y-2">
            <Label htmlFor={codeId}>Mã xác nhận (6 số, gửi tới {sentTo})</Label>
            <div className="flex flex-wrap gap-2">
              <Input id={codeId} inputMode="numeric" autoComplete="one-time-code" maxLength={6} value={code} disabled={busy}
                onChange={(event) => setCode(event.target.value.replace(/\D/g, ""))} className="w-32 font-mono tracking-widest" />
              <Button type="button" variant="outline" disabled={busy || wait > 0} onClick={() => void sendCode()}>
                {wait > 0 ? `Gửi lại sau ${wait} giây` : "Gửi lại mã"}
              </Button>
              <Button type="button" variant="ghost" disabled={busy} onClick={() => { setSentTo(null); setCode(""); setProblem(null); }}>
                Sửa email
              </Button>
            </div>
            <p className="text-xs text-muted-foreground">Mã có hiệu lực 5 phút. Không thấy thư thì xem cả mục Spam.</p>
          </div>
          <Button type="submit" disabled={busy}>
            {busy && <Loader2 className="size-4 animate-spin" />}
            Xác nhận đổi email
          </Button>
        </>
      )}
      {problem && <p role="alert" className="text-sm text-destructive">{problem}</p>}
    </form>
  );
}

/** Changes the login password while it is known (POST /auth/change-password). */
function PasswordForm({ onSaved }: { onSaved: () => void }) {
  const currentId = useId();
  const nextId = useId();
  const confirmId = useId();
  const [current, setCurrent] = useState("");
  const [next, setNext] = useState("");
  const [again, setAgain] = useState("");
  const [busy, setBusy] = useState(false);
  const [problem, setProblem] = useState<string | null>(null);

  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const wrong = !current ? "Nhập mật khẩu hiện tại." : passwordProblem(next)
      ?? (next !== again ? "Hai lần nhập mật khẩu mới chưa giống nhau." : next === current ? "Mật khẩu mới phải khác mật khẩu hiện tại." : null);
    if (wrong) return setProblem(wrong);
    setBusy(true);
    setProblem(null);
    try {
      await internalApi("/auth/change-password", "POST", { currentPassword: current, newPassword: next });
      toast.success("Đã đổi mật khẩu. Lần đăng nhập sau dùng mật khẩu mới.");
      onSaved();
    } catch (error) {
      setProblem(message(error, "Không đổi được mật khẩu."));
    } finally {
      setBusy(false);
    }
  };

  return (
    <form onSubmit={(event) => void submit(event)} className="max-w-md space-y-4">
      <div className="space-y-2">
        <Label htmlFor={currentId}>Mật khẩu hiện tại</Label>
        <SecretInput id={currentId} value={current} onChange={setCurrent} disabled={busy} autoComplete="current-password" />
      </div>
      <div className="space-y-2">
        <Label htmlFor={nextId}>Mật khẩu mới</Label>
        <SecretInput id={nextId} value={next} onChange={setNext} disabled={busy} autoComplete="new-password" />
        <p className="text-xs text-muted-foreground">Từ 8 đến 100 ký tự, có cả chữ và số. Không dùng lại key mở tệp làm mật khẩu.</p>
      </div>
      <div className="space-y-2">
        <Label htmlFor={confirmId}>Nhập lại mật khẩu mới</Label>
        <SecretInput id={confirmId} value={again} onChange={setAgain} disabled={busy} autoComplete="new-password" />
        {again.length > 0 && again !== next && <p className="text-xs text-destructive">Hai lần nhập chưa giống nhau.</p>}
      </div>
      <Button type="submit" disabled={busy}>
        {busy && <Loader2 className="size-4 animate-spin" />}
        Đổi mật khẩu
      </Button>
      {problem && <p role="alert" className="text-sm text-destructive">{problem}</p>}
    </form>
  );
}

function Row({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="grid gap-1 py-2 sm:grid-cols-[10rem_1fr]">
      <dt className="text-sm text-muted-foreground">{label}</dt>
      <dd className="text-sm [overflow-wrap:anywhere]">{children}</dd>
    </div>
  );
}

/** Page "Hồ sơ của tôi": the employee's account, their email and their password. */
export function ProfilePage() {
  const user = useInternalUser();
  const [profile, setProfile] = useState<Profile | null>(null);
  const [error, setError] = useState<string | null>(null);
  // Remounts the forms after a save, so nothing typed stays in them.
  const [round, setRound] = useState(0);

  useEffect(() => {
    internalApi<Profile>("/profile").then(
      (loaded) => { setProfile(loaded); setError(null); },
      (loadError: unknown) => setError(message(loadError, "Không tải được hồ sơ.")),
    );
  }, []);

  const canHoldKey = user.permissions.includes("contract.signed_evidence.view") || user.permissions.includes("contract.scanned.view");

  return (
    <div className="space-y-6 p-2 lg:p-6">
      <div>
        <h1 className="flex items-center gap-2 text-2xl font-bold tracking-tight text-foreground">
          <UserRound className="size-6 text-primary" />
          Hồ sơ của tôi
        </h1>
        <p className="mt-1 text-sm text-muted-foreground">Thông tin tài khoản, email nhận thông báo và mật khẩu đăng nhập.</p>
      </div>

      {error && (
        <Alert variant="destructive">
          <AlertTitle>Không tải được</AlertTitle>
          <AlertDescription>{error}</AlertDescription>
        </Alert>
      )}

      {!profile ? (
        !error && <Skeleton className="h-40 w-full" />
      ) : (
        <>
          <Card>
            <CardHeader>
              <CardTitle>Tài khoản</CardTitle>
              <CardDescription>Họ tên, phòng ban và chức vụ do quản trị cập nhật.</CardDescription>
            </CardHeader>
            <CardContent>
              <dl className="divide-y">
                <Row label="Họ tên">{profile.name}</Row>
                <Row label="Tên đăng nhập">{profile.username}</Row>
                <Row label="Mã nhân viên">{profile.code}</Row>
                <Row label="Phòng ban">{user.department ?? "—"} · {user.isManager ? "Trưởng phòng" : "Nhân viên"}</Row>
                <Row label="Số điện thoại">{profile.phone ?? "—"}</Row>
                <Row label="Đăng nhập gần nhất">{profile.lastLoginAt ? dateTime(profile.lastLoginAt) : "—"}</Row>
              </dl>
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2"><Mail className="size-5" />Email</CardTitle>
              <CardDescription>
                Email nhận thông báo{canHoldKey ? " và mã xác nhận khi đặt key mở tệp" : ""}. Đổi email cần mật khẩu đăng nhập và mã
                gửi tới email mới; email cũ sẽ nhận thư báo.
              </CardDescription>
            </CardHeader>
            <CardContent className="space-y-4">
              <p className="flex flex-wrap items-center gap-2 text-sm">
                Hiện tại: <span className="font-medium">{profile.email ?? "Chưa có"}</span>
                {profile.email && (profile.emailVerifiedAt
                  ? <Badge variant="secondary">Đã xác nhận</Badge>
                  : <Badge variant="outline">Chưa xác nhận</Badge>)}
              </p>
              <EmailForm key={`email-${round}`} profile={profile} onSaved={(saved) => { setProfile(saved); setRound(round + 1); }} />
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2"><KeyRound className="size-5" />Mật khẩu đăng nhập</CardTitle>
              <CardDescription>
                Quên mật khẩu thì nhờ Ban Giám Đốc cấp lại mật khẩu tạm.
                {canHoldKey && <> Key mở tệp scan là riêng, đổi ở mục <Link href="/workspace/my-key" className="text-primary hover:underline">Key của tôi</Link>.</>}
              </CardDescription>
            </CardHeader>
            <CardContent>
              <PasswordForm key={`password-${round}`} onSaved={() => setRound(round + 1)} />
            </CardContent>
          </Card>
        </>
      )}
    </div>
  );
}

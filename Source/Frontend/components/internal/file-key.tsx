"use client";

import axios from "axios";
import { createContext, useCallback, useContext, useId, useMemo, useRef, useState, type FormEvent, type ReactNode } from "react";
import { Eye, EyeOff, KeyRound } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";

/** The header the API reads the employee's 12-character key from (FileKeyHeader). */
export const FILE_KEY_HEADER = "X-File-Key";

/** Thrown when the person closes the key dialog. */
export class FileKeyCancelled extends Error {
  constructor() {
    super("Đã hủy nhập key.");
  }
}

const KEY_CODES = ["FileKeyRequired", "FileKeyWrong"] as const;

/** The failure code of an internal API error; a file download answers errors as a Blob. */
export const fileKeyErrorCode = async (error: unknown): Promise<string | null> => {
  if (!axios.isAxiosError(error) || !error.response) return null;
  let data: unknown = error.response.data;
  if (typeof Blob !== "undefined" && data instanceof Blob) {
    try {
      data = JSON.parse(await data.text());
    } catch {
      return null;
    }
  }
  const title = data && typeof data === "object" ? (data as { code?: unknown }).code : null;
  return typeof title === "string" ? title : null;
};

/** The readable message of an error, also when the body came back as a Blob. */
export const blobErrorMessage = async (error: unknown, fallback: string): Promise<string> => {
  if (axios.isAxiosError(error) && error.response && typeof Blob !== "undefined" && error.response.data instanceof Blob) {
    try {
      const body = JSON.parse(await error.response.data.text()) as { message?: unknown };
      if (typeof body.message === "string" && body.message.trim()) return body.message;
    } catch {
      // fall through
    }
  }
  if (axios.isAxiosError(error) && error.response?.data && typeof error.response.data === "object") {
    const detail = (error.response.data as { message?: unknown }).message;
    if (typeof detail === "string" && detail.trim()) return detail;
  }
  if (axios.isAxiosError(error) && !error.response) return "Không kết nối được máy chủ. Vui lòng thử lại.";
  return fallback;
};

/** For a preview tab opened before the file is fetched: it must not cover the dialog that asks for the key again. */
interface AroundAsk {
  beforeAsk: () => void;
  afterAsk: () => void;
}

interface FileKeyValue {
  /** The key typed earlier in this tab, or asks for it. Rejects with FileKeyCancelled when the dialog is closed. */
  ensureKey: () => Promise<string>;
  /**
   * Runs a call that may need the key. Tries the remembered key first; when the server asks for a key or says it
   * is wrong, asks for it again and tries once more.
   */
  withFileKey: <T>(call: (key: string | undefined) => Promise<T>, around?: AroundAsk) => Promise<T>;
  forgetKey: () => void;
}

const FileKeyContext = createContext<FileKeyValue | null>(null);

export function useFileKey(): FileKeyValue {
  const value = useContext(FileKeyContext);
  if (!value) throw new Error("useFileKey must be used inside FileKeyProvider.");
  return value;
}

/**
 * Holds the employee's 12-character key in memory for this tab only: never in localStorage, a cookie or the URL,
 * so closing the tab forgets it.
 */
export function FileKeyProvider({ children }: { children: ReactNode }) {
  const remembered = useRef<string | null>(null);
  const pending = useRef<{ resolve: (key: string) => void; reject: (error: Error) => void } | null>(null);
  const [open, setOpen] = useState(false);
  const [problem, setProblem] = useState<string | null>(null);
  const [value, setValue] = useState("");
  const [visible, setVisible] = useState(false);
  const inputId = useId();

  const ask = useCallback((message: string | null) => {
    setProblem(message);
    setValue("");
    setVisible(false);
    setOpen(true);
    return new Promise<string>((resolve, reject) => {
      pending.current = { resolve, reject };
    });
  }, []);

  const ensureKey = useCallback(async () => remembered.current ?? ask(null), [ask]);

  const forgetKey = useCallback(() => {
    remembered.current = null;
  }, []);

  const withFileKey = useCallback(
    async <T,>(call: (key: string | undefined) => Promise<T>, around?: AroundAsk): Promise<T> => {
      try {
        return await call(remembered.current ?? undefined);
      } catch (error) {
        const code = await fileKeyErrorCode(error);
        if (!code || !(KEY_CODES as readonly string[]).includes(code)) throw error;
        remembered.current = null;
        around?.beforeAsk();
        const key = await ask(code === "FileKeyWrong" ? "Key không đúng. Hãy nhập lại." : null);
        around?.afterAsk();
        try {
          return await call(key);
        } catch (again) {
          if ((await fileKeyErrorCode(again)) === "FileKeyWrong") remembered.current = null;
          throw again;
        }
      }
    },
    [ask],
  );

  const submit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const key = value.trim();
    if (key.length !== 12) {
      setProblem("Key gồm đúng 12 ký tự.");
      return;
    }
    remembered.current = key;
    setOpen(false);
    pending.current?.resolve(key);
    pending.current = null;
  };

  const close = (next: boolean) => {
    if (next) return;
    setOpen(false);
    pending.current?.reject(new FileKeyCancelled());
    pending.current = null;
  };

  const context = useMemo(() => ({ ensureKey, withFileKey, forgetKey }), [ensureKey, withFileKey, forgetKey]);

  return (
    <FileKeyContext.Provider value={context}>
      {children}
      <Dialog open={open} onOpenChange={close}>
        <DialogContent className="sm:max-w-md">
          <form onSubmit={submit} className="space-y-4">
            <DialogHeader>
              <DialogTitle className="flex items-center gap-2">
                <KeyRound className="size-5 text-primary" />
                Nhập key mở tệp
              </DialogTitle>
              <DialogDescription>
                Bản scan được mã hóa. Nhập key 12 ký tự quản trị đã cấp cho bạn. Key chỉ được giữ trong tab này.
              </DialogDescription>
            </DialogHeader>
            <div className="space-y-2">
              <Label htmlFor={inputId}>Key 12 ký tự</Label>
              <div className="flex gap-2">
                <Input
                  id={inputId}
                  type={visible ? "text" : "password"}
                  autoComplete="off"
                  spellCheck={false}
                  maxLength={32}
                  value={value}
                  onChange={(event) => {
                    setValue(event.target.value);
                    setProblem(null);
                  }}
                  aria-invalid={problem ? true : undefined}
                  autoFocus
                  className="font-mono"
                />
                <Button
                  type="button"
                  variant="outline"
                  size="icon"
                  aria-label={visible ? "Ẩn key" : "Hiện key"}
                  onClick={() => setVisible(!visible)}
                >
                  {visible ? <EyeOff className="size-4" /> : <Eye className="size-4" />}
                </Button>
              </div>
              {problem && (
                <p role="alert" className="text-sm text-destructive">
                  {problem}
                </p>
              )}
            </div>
            <DialogFooter>
              <Button type="button" variant="outline" onClick={() => close(false)}>
                Hủy
              </Button>
              <Button type="submit">Mở tệp</Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>
    </FileKeyContext.Provider>
  );
}

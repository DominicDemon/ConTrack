import { scanApi } from "../http";

export interface EmployeeKeyStatus {
  employeeId: string;
  username: string;
  displayName: string;
  /** The employee can see scans, so they may set a key. */
  mayHoldKey: boolean;
  hasKey: boolean;
  /** When the employee set or last changed their key. */
  setAt: string | null;
  /** Files an older key of the employee opened that the current key cannot open yet. */
  filesToRecover: number;
}

export interface RecoveredFiles {
  employeeId: string;
  granted: number;
  alreadyOpen: number;
  notRecoverable: number;
}

/** The signed-in employee's own key (page "Key của tôi"). */
export interface MyFileKey {
  mayHoldKey: boolean;
  hasKey: boolean;
  setAt: string | null;
  encryptionEnabled: boolean;
  serverKeepsRecoveryKey: boolean;
  /** Masked address the confirmation code goes to; null when the account has no valid email. */
  email: string | null;
}

export interface SetFileKeyResult {
  setAt: string;
  /** An older key was replaced (the employee forgot it). */
  replaced: boolean;
  /** Older files the new key opens at once; null when they come back later. */
  recoveredFiles: number | null;
}

export const encryptionKeysApi = {
  async list(): Promise<EmployeeKeyStatus[]> {
    const { data } = await scanApi.get<EmployeeKeyStatus[]>("/encryption-keys");
    return data;
  },

  /** Stops the employee's key (they left, or the key may be known); they can set a new one later. */
  async revoke(employeeId: string): Promise<void> {
    await scanApi.post(`/encryption-keys/${employeeId}/revoke`);
  },

  /**
   * Reopens old files for the employee's current key. Without a file the server uses the recovery key it keeps;
   * a given file is used for this request only and not kept.
   */
  async recoverFiles(employeeId: string, recoveryKey?: File): Promise<RecoveredFiles> {
    const form = new FormData();
    if (recoveryKey) form.append("recoveryKey", recoveryKey);
    const { data } = await scanApi.post<RecoveredFiles>(`/encryption-keys/${employeeId}/recover-files`, form);
    return data;
  },

  async mine(): Promise<MyFileKey> {
    const { data } = await scanApi.get<MyFileKey>("/encryption-keys/me");
    return data;
  },

  /** Checks the login password and emails a 6-digit code; answers the masked address. */
  async sendCode(password: string): Promise<string> {
    const { data } = await scanApi.post<{ sentTo: string }>("/encryption-keys/me/code", { password });
    return data.sentTo;
  },

  /** Creates the first key, or a new one after forgetting the old: login password and the emailed code. */
  async setKey(password: string, code: string, newKey: string): Promise<SetFileKeyResult> {
    const { data } = await scanApi.post<SetFileKeyResult>("/encryption-keys/me", { password, code, newKey });
    return data;
  },

  /** Changes the key while it is still known; every file stays open. */
  async changeKey(currentKey: string, newKey: string): Promise<SetFileKeyResult> {
    const { data } = await scanApi.post<SetFileKeyResult>("/encryption-keys/me/change", { currentKey, newKey });
    return data;
  },
};

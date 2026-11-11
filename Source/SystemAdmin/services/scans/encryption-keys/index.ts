import { scanApi } from "../http";

export interface EmployeeKeyStatus {
  employeeId: string;
  username: string;
  displayName: string;
  hasKey: boolean;
  issuedAt: string | null;
  issuedBy: string | null;
  /** Files an older key of the employee opened that the current key cannot open yet. */
  filesToRecover: number;
}

export interface RecoveredFiles {
  employeeId: string;
  granted: number;
  alreadyOpen: number;
  notRecoverable: number;
}

/** A key issued just now: shown once, never again. */
export interface IssuedKey {
  employeeId: string;
  displayName: string;
  key: string;
  issuedAt: string;
  /** Old files the new key opens right away; null when the server keeps no recovery key. */
  recoveredFiles: number | null;
}

export const encryptionKeysApi = {
  async list(): Promise<EmployeeKeyStatus[]> {
    const { data } = await scanApi.get<EmployeeKeyStatus[]>("/encryption-keys");
    return data;
  },

  async issue(employeeId: string): Promise<IssuedKey> {
    const { data } = await scanApi.post<IssuedKey>(`/encryption-keys/${employeeId}`);
    return data;
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

  async mine(): Promise<{ hasKey: boolean; encryptionEnabled: boolean; serverKeepsRecoveryKey: boolean }> {
    const { data } = await scanApi.get<{ hasKey: boolean; encryptionEnabled: boolean; serverKeepsRecoveryKey: boolean }>(
      "/encryption-keys/me",
    );
    return data;
  },
};

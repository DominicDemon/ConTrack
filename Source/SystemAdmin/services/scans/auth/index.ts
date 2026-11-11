import { scanApi } from "../http";

/** The signed-in employee, with the permissions of their roles and all those they depend on. */
export interface ScanUser {
  employeeId: string;
  username: string;
  displayName: string;
  permissions: string[];
}

/** Permission codes of SYSTEM_PERMISSION for the paper-signing evidence. */
export const SIGNING_PERMISSIONS = {
  view: "contract.signed_evidence.view",
  upload: "contract.signed_evidence.upload",
  review: "contract.signed_evidence.review",
} as const;

/** Permission codes for old paper contracts scanned in. */
export const SCANNED_PERMISSIONS = {
  view: "contract.scanned.view",
  upload: "contract.scanned.upload",
  confirm: "contract.scanned.confirm",
} as const;

/** Issuing the 12-character file keys. */
export const ENCRYPTION_KEY_PERMISSIONS = {
  manage: "system.encryption_key.manage",
} as const;

export const scanAccessApi = {
  async me(): Promise<ScanUser> {
    const { data } = await scanApi.get<ScanUser>("/scan-access");
    return data;
  },
};

import { httpSignedEvidenceApi } from "./api";
import type { SignedEvidenceApi } from "./types";

export * from "./types";

/** Paper-signing evidence. */
export const signedEvidenceApi: SignedEvidenceApi = httpSignedEvidenceApi;

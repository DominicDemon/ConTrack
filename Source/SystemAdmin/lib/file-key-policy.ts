/**
 * The rules of a key an employee sets for themselves, shown while typing. The server checks them again
 * (Backend FileKeyPolicy.cs) and also that the key is not the login password.
 */
export const FILE_KEY_MIN = 12;
export const FILE_KEY_MAX = 64;

const COMMON_WORDS = [
  "password", "passw0rd", "matkhau", "123456", "654321", "qwerty", "asdfgh", "zxcvbn", "abc123", "iloveyou",
  "admin", "contrack", "welcome", "letmein", "111111", "000000", "123123", "1q2w3e", "baomat", "hopdong",
];

export interface FileKeyOwner {
  username?: string | null;
  email?: string | null;
  phone?: string | null;
}

export interface FileKeyRule {
  label: string;
  ok: boolean;
}

const hasRun = (key: string) => {
  const lower = key.toLowerCase();
  const alnum = /[a-z0-9]/;
  for (let i = 0; i + 3 < lower.length; i++) {
    const steps = [1, 2, 3].map((j) => lower.charCodeAt(i + j) - lower.charCodeAt(i + j - 1));
    if (steps.every((step) => step === 0)) return true;
    const ordered = steps.every((step) => step === 1) || steps.every((step) => step === -1);
    if (ordered && alnum.test(lower[i]) && alnum.test(lower[i + 3])) return true;
  }
  return false;
};

const ownerParts = (owner: FileKeyOwner) => {
  const parts: string[] = [];
  const username = owner.username?.trim().toLowerCase();
  if (username && username.length >= 3) parts.push(username);
  const mailbox = owner.email?.split("@")[0]?.trim().toLowerCase();
  if (mailbox && mailbox.length >= 3) parts.push(mailbox);
  const digits = (owner.phone ?? "").replace(/\D/g, "");
  if (digits.length >= 6) parts.push(digits.slice(-6));
  return parts;
};

/** Each rule with whether the key meets it, in the order shown under the field. */
export function fileKeyRules(key: string, owner: FileKeyOwner = {}): FileKeyRule[] {
  const lower = key.toLowerCase();
  return [
    { label: `Từ ${FILE_KEY_MIN} đến ${FILE_KEY_MAX} ký tự`, ok: key.length >= FILE_KEY_MIN && key.length <= FILE_KEY_MAX },
    { label: "Chỉ chữ không dấu, số, ký tự đặc biệt; không dấu cách", ok: key.length > 0 && /^[\x21-\x7e]+$/.test(key) },
    { label: "Có chữ hoa và chữ thường", ok: /[A-Z]/.test(key) && /[a-z]/.test(key) },
    { label: "Có số và ký tự đặc biệt (!@#$…)", ok: /[0-9]/.test(key) && /[^A-Za-z0-9]/.test(key) },
    { label: "Không có 4 ký tự lặp hoặc liền thứ tự (aaaa, 1234, abcd)", ok: key.length > 0 && !hasRun(key) },
    {
      label: "Không chứa từ dễ đoán, tên đăng nhập, email hay số điện thoại",
      ok: key.length > 0 && !COMMON_WORDS.some((word) => lower.includes(word)) && !ownerParts(owner).some((part) => lower.includes(part)),
    },
  ];
}

export const isGoodFileKey = (key: string, owner: FileKeyOwner = {}) => fileKeyRules(key, owner).every((rule) => rule.ok);

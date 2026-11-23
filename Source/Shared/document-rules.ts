/**
 * What a document of one type needs before it can be sent. Mirror of Backend DocumentRules.cs: change both together.
 * Fields the template itself marks required are checked on top of these.
 */
export type DocumentRule = {
  type: number;
  effectiveLabel: string; effectiveRequired: boolean;
  /** The second date exists for this document; otherwise it is hidden and cleared. */
  expireShown: boolean; expireLabel: string; expireRequired: boolean;
  itemsLabel: string; itemsRequired: boolean;
  /** Lines carry a price; a handover list only names what is handed over and how many. */
  itemPrices: boolean;
  /** The items add up to more than zero (receipt, payment request). */
  amountRequired: boolean;
  /** The payment schedule is part of this document; otherwise it is hidden and cleared. */
  milestones: boolean;
  /** true: always; null: only once the document has items (appendix). */
  milestonesRequired: boolean | null;
  /** Names the instalment of its contract it asks to be paid (payment request). */
  parentMilestone: boolean;
};

const contract: DocumentRule = { type: 1, effectiveLabel: "Ngày hiệu lực", effectiveRequired: false, expireShown: true, expireLabel: "Ngày hết hạn", expireRequired: true,
  itemsLabel: "Hạng mục hợp đồng", itemsRequired: true, itemPrices: true, amountRequired: false, milestones: true, milestonesRequired: true, parentMilestone: false };

const rules: Record<number, DocumentRule> = {
  1: contract,
  2: { type: 2, effectiveLabel: "Ngày hiệu lực", effectiveRequired: true, expireShown: true, expireLabel: "Ngày hết hạn", expireRequired: false,
    itemsLabel: "Hạng mục điều chỉnh", itemsRequired: false, itemPrices: true, amountRequired: false, milestones: true, milestonesRequired: null, parentMilestone: false },
  3: { type: 3, effectiveLabel: "Ngày bàn giao", effectiveRequired: true, expireShown: false, expireLabel: "", expireRequired: false,
    itemsLabel: "Danh mục bàn giao", itemsRequired: true, itemPrices: false, amountRequired: false, milestones: false, milestonesRequired: false, parentMilestone: false },
  4: { type: 4, effectiveLabel: "Ngày thu", effectiveRequired: true, expireShown: false, expireLabel: "", expireRequired: false,
    itemsLabel: "Khoản thu", itemsRequired: true, itemPrices: true, amountRequired: true, milestones: false, milestonesRequired: false, parentMilestone: false },
  5: { type: 5, effectiveLabel: "Ngày đề nghị", effectiveRequired: false, expireShown: true, expireLabel: "Hạn thanh toán", expireRequired: true,
    itemsLabel: "Khoản đề nghị thanh toán", itemsRequired: true, itemPrices: true, amountRequired: true, milestones: false, milestonesRequired: false, parentMilestone: true },
};

export const documentRule = (type: number | null | undefined): DocumentRule => rules[type ?? 1] ?? contract;
export const needsMilestones = (rule: DocumentRule, itemCount: number) => rule.milestones && (rule.milestonesRequired ?? itemCount > 0);

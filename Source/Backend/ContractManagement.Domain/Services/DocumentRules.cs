namespace ContractManagement.Domain.Internal;

/// <summary>
/// What a document of one type needs before it can be sent: a contract needs an end date, items and a payment schedule,
/// a handover record a handover date and the list of what is handed over, a receipt an amount and the day it was received…
/// Fields a template itself marks required are checked on top of these. Mirrored in Shared/document-rules.ts.
/// </summary>
/// <param name="ItemsRequired">At least one line in the items table.</param>
/// <param name="AmountRequired">The items add up to more than zero (receipt, payment request).</param>
/// <param name="ItemPrices">Lines carry a price; a handover list only names what is handed over and how many.</param>
/// <param name="Milestones">The payment schedule is part of this document; otherwise it is cleared.</param>
/// <param name="MilestonesRequired">A schedule is needed: always (contract) or only once the document has items (appendix).</param>
/// <param name="ExpireShown">The second date exists for this document; otherwise it is cleared.</param>
/// <param name="ParentMilestone">Names the instalment of its contract it asks to be paid (payment request).</param>
public sealed record DocumentRule(int Type, string EffectiveLabel, bool EffectiveRequired, bool ExpireShown, string ExpireLabel, bool ExpireRequired,
    string ItemsLabel, bool ItemsRequired, bool ItemPrices, bool AmountRequired, bool Milestones, bool? MilestonesRequired, bool ParentMilestone)
{
    public bool NeedsMilestones(int itemCount) => Milestones && (MilestonesRequired ?? itemCount > 0);
}

public static class DocumentRules
{
    public const int Contract = 1, Appendix = 2, Handover = 3, Receipt = 4, PaymentRequest = 5;

    public static readonly DocumentRule ContractRule = new(Contract, "Ngày hiệu lực", false, true, "Ngày hết hạn", true,
        "Hạng mục hợp đồng", true, true, false, true, true, false);

    public static DocumentRule For(int? type) => type switch
    {
        Appendix => new(Appendix, "Ngày hiệu lực", true, true, "Ngày hết hạn", false,
            "Hạng mục điều chỉnh", false, true, false, true, null, false),
        Handover => new(Handover, "Ngày bàn giao", true, false, "", false,
            "Danh mục bàn giao", true, false, false, false, false, false),
        Receipt => new(Receipt, "Ngày thu", true, false, "", false,
            "Khoản thu", true, true, true, false, false, false),
        PaymentRequest => new(PaymentRequest, "Ngày đề nghị", false, true, "Hạn thanh toán", true,
            "Khoản đề nghị thanh toán", true, true, true, false, false, true),
        _ => ContractRule
    };
}

namespace ContractManagement.Domain.Entities.Master;

/// <summary>A document type (contract, appendix, handover record…) and what a document of it needs before it is sent
/// (020-document-categories.sql). Ids 1-5 are the built-in types; 1 (contract) is fixed.</summary>
public partial class DocumentCategory
{
    public int DocumentCategoryId { get; set; }
    public string DocumentCategoryName { get; set; } = null!;
    public bool IsActive { get; set; }
    public string EffectiveLabel { get; set; } = null!;
    public bool EffectiveRequired { get; set; }
    public bool ExpireShown { get; set; }
    public string ExpireLabel { get; set; } = "";
    public bool ExpireRequired { get; set; }
    public string ItemsLabel { get; set; } = null!;
    public bool ItemsRequired { get; set; }
    public bool ItemPrices { get; set; }
    public bool AmountRequired { get; set; }
    public bool Milestones { get; set; }
    /// <summary>null: a payment schedule is needed only once the document has items.</summary>
    public bool? MilestonesRequired { get; set; }
    public bool ParentMilestone { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? UpdatedDate { get; set; }
}

namespace ContractManagement.Infrastructure.Persistence.Application.Models;

public sealed class CONTRACT_TEMPLATE_APPENDIX_TERM
{
    public int TemplateAppendixTermId { get; set; }
    public int TemplateAppendixId { get; set; }
    public string TermCode { get; set; } = null!;
    public string TermTitle { get; set; } = null!;
    public string? TermTitleEn { get; set; }
    public string? TermContent { get; set; }
    public string? TermContentEn { get; set; }

    public byte BilingualContentMode { get; set; } = 1;
    public int DisplayOrder { get; set; }
    public int CreatedEmployeeId { get; set; }
    public DateTime CreatedDate { get; set; }
    public int? UpdatedEmployeeId { get; set; }
    public DateTime? UpdatedDate { get; set; }
    public byte[] RowVersion { get; set; } = null!;
}



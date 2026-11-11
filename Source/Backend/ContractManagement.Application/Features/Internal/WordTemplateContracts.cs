using ContractManagement.Domain.Internal;

namespace ContractManagement.Application.Features.Internal;

public sealed record WordBlock(string Id, string Location, string Text,
    int? TableId = null, int? Row = null, int? Cell = null, int ColSpan = 1, string? VerticalMerge = null,
    List<WordImagePreview>? Images = null);
public sealed record WordImagePreview(string Mime, string Base64);
public sealed record WordExtraction(string Name, string DocumentBase64, List<WordBlock> Blocks, List<DraftField> CoreFields)
{
    public string SourceToken { get; init; } = "";
}
public sealed record WordTableCell(string Text, int ColSpan = 1);
public sealed record WordTable(string Id, string AfterBlockId, bool Borders, List<List<WordTableCell>> Rows, int Order = 0);
public sealed record WordContent(string Id, string AfterBlockId, string Kind, string Title, string Text,
    string? ImageBase64 = null, decimal WidthCm = 12, string Align = "center", int Order = 0);
public sealed record WordTemplateInput(string FileName, string Base64, string DocumentBase64, string Name,
    int DocumentType, List<WordBlock> Blocks, List<DraftField> Fields, Dictionary<string, string> SampleValues, string SourceToken,
    List<WordTable>? Tables = null, List<WordContent>? Additions = null, bool ManualContent = false);
public sealed record WordPreview(string PdfBase64, string PreviewToken);
public sealed record WordActivation(WordTemplateInput Input, string PreviewToken);
public sealed record WordDraftPreview(long? DraftId, DraftContent Content, bool Pdf = true);
public sealed record WordStoredPreview(bool Pdf = true);

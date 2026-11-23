namespace ContractManagement.Domain.Entities.Master;

/// <summary>The one row (id 1) holding the company's contract print format as JSON (021-contract-format.sql).</summary>
public partial class ContractFormatSetting
{
    public int SettingId { get; set; }
    public string SettingValue { get; set; } = null!;
    public DateTime UpdatedDate { get; set; }
    public long? UpdatedEmployeeId { get; set; }
}

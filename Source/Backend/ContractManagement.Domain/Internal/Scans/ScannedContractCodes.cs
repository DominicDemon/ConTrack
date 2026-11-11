namespace ContractManagement.Domain.Internal.Scans;

/// <summary>
/// Values written to CONTRACTS for an old paper contract brought in by scanning. It is only archived, so it
/// gets no contract_status or approval_status; source_type (ONLINE/SCANNED in MasterDB-Optimize_v1.sql) marks it.
/// </summary>
public static class ScannedContractCodes
{
    public const string SourceScanned = "SCANNED";

    public const string DefaultCurrency = "VND";

    /// <summary>FILE_STORAGE.objecttype of a scan waiting for its contract, then of the contract's file.</summary>
    public const string FileObjectBatch = "OCR_BATCH";
    public const string FileObjectContract = "CONTRACTS";
}

using ContractManagement.Domain.Internal.Scans;
namespace ContractManagement.Application.Features.Internal.Scans;

/// <summary>
/// SYSTEM_PERMISSION.system_permission_code of the paper-signing evidence (F18, F19). The rows are
/// created by Artifacts/Database/Master/007-scan-permissions.sql.
/// </summary>
public static class SigningPermissions
{
    /// <summary>List contracts, open the evidence, download the file, search by keyword.</summary>
    public const string View = "contract.signed_evidence.view";

    /// <summary>Upload, replace the scan, add or remove keywords (F18 steps 1 to 3, F19).</summary>
    public const string Upload = "contract.signed_evidence.upload";

    /// <summary>
    /// Check the signatures and stamps and accept or reject (F18 steps 4 and 5). A checker works on
    /// every contract, so this permission also widens the data scope to all contracts.
    /// </summary>
    public const string Review = "contract.signed_evidence.review";
}

/// <summary>
/// SYSTEM_PERMISSION codes of the old paper contracts brought in by scanning (CONTRACTS with
/// source_type SCANNED). Created by Artifacts/Database/Master/007-scan-permissions.sql.
/// </summary>
public static class ScannedContractPermissions
{
    /// <summary>See scanned contracts and their files.</summary>
    public const string View = "contract.scanned.view";

    /// <summary>Upload scans of old paper contracts for OCR.</summary>
    public const string Upload = "contract.scanned.upload";

    /// <summary>Check what OCR read and create the contract; also sees every batch.</summary>
    public const string Confirm = "contract.scanned.confirm";
}

/// <summary>SYSTEM_PERMISSION code for giving employees their 12-character file key.</summary>
public static class EncryptionKeyPermissions
{
    /// <summary>Issue (or replace) the 12-character key of an employee and see who has one.</summary>
    public const string Manage = "system.encryption_key.manage";
}

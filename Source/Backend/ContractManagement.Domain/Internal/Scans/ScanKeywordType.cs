namespace ContractManagement.Domain.Internal.Scans;

/// <summary>Kinds of keyword OCR reads from a signed paper scan so the contract can be searched by them.</summary>
public enum ScanKeywordType : byte
{
    ContractCode = 1,
    TaxCode = 2,
    PhoneNumber = 3,

    /// <summary>The name of a party (company or person), kept as written on the paper.</summary>
    CustomerName = 4,

    /// <summary>An email address printed on the paper, lower case. Optional: many contracts carry none.</summary>
    Email = 5
}

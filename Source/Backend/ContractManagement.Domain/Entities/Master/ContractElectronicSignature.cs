using System;
using System.Collections.Generic;

namespace ContractManagement.Domain.Entities.Master;

public partial class ContractElectronicSignature
{
    public long ContractElectronicSignatureId { get; set; }

    public long ContractDocumentId { get; set; }

    public long ContractVersionId { get; set; }

    public int ContractElectronicPartytype { get; set; }

    public string ContractElectronicSignertype { get; set; } = null!;

    public long? EmployeeId { get; set; }

    public string ContractElectronicSignername { get; set; } = null!;

    public string? ContractElectronicSignertitle { get; set; }

    public string ContractElectronicSignerphone { get; set; } = null!;

    public string? ContractElectronicSigneremail { get; set; }

    public string ContractElectronicSignaturetype { get; set; } = null!;

    public string? ContractElectronicSignatureimagestoragekey { get; set; }

    public int? ContractElectronicCustomerotpchallengeid { get; set; }

    public int ContractElectronicStatus { get; set; }

    public DateTime? ContractElectronicSignedat { get; set; }

    public string? ContractElectronicIpaddress { get; set; }

    public string? ContractElectronicUseragent { get; set; }

    public string? ContractElectronicDocumentHashAtSigning { get; set; }

    public int? ContractElectronicPagenumber { get; set; }

    public decimal? ContractElectronicPositionx { get; set; }

    public decimal? ContractElectronicPositiony { get; set; }

    public decimal? ContractElectronicWidth { get; set; }

    public decimal? ContractElectronicHeight { get; set; }

    public string? ContractElectronicRejectReason { get; set; }

    public DateTime CreatedAt { get; set; }

    public byte[] Rowversion { get; set; } = null!;
}

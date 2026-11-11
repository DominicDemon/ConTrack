using System;
using System.Collections.Generic;

namespace ContractManagement.Domain.Entities.Master;

public partial class OcrJob
{
    public long OcrJobId { get; set; }

    public long OcrBatchId { get; set; }

    public long FileStorageFileid { get; set; }

    public long? ContractDocumentId { get; set; }

    public decimal? OcrJobAvgConfidenceScore { get; set; }

    public string OcrJobProcessingStatus { get; set; } = null!;

    public string? OcrText { get; set; }

    public int? PageCount { get; set; }

    public string? ScanQuality { get; set; }

    public string? ErrorMessage { get; set; }

    public long? ScannedEmployeeId { get; set; }

    public DateTime ScannedAt { get; set; }

    public DateTime? CompletedAt { get; set; }
}

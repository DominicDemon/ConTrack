using System;
using System.Collections.Generic;

namespace ContractManagement.Infrastructure.Persistence.ReverseEngineered.Entities;

public partial class OCR_JOB
{
    public long ocr_job_id { get; set; }

    public long ocr_batch_id { get; set; }

    public long file_storage_fileid { get; set; }

    public long? contract_document_id { get; set; }

    public decimal? ocr_job_avg_confidence_score { get; set; }

    public string ocr_job_processing_status { get; set; } = null!;

    public string? ocr_text { get; set; }

    public int? page_count { get; set; }

    public string? scan_quality { get; set; }

    public string? error_message { get; set; }

    public long? scanned_employee_id { get; set; }

    public DateTime scanned_at { get; set; }

    public DateTime? completed_at { get; set; }
}

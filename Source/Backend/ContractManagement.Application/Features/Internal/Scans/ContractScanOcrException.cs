using ContractManagement.Domain.Internal.Scans;
namespace ContractManagement.Application.Features.Internal.Scans;

public static class ContractScanOcrErrorCodes
{
    public const string EngineUnavailable = "OcrEngineUnavailable";
    public const string EngineFailed = "OcrEngineFailed";
    public const string Timeout = "OcrTimeout";
    public const string UnsupportedFile = "OcrUnsupportedFile";
    public const string PdfUnreadable = "OcrPdfUnreadable";
}

public sealed class ContractScanOcrException : Exception
{
    public ContractScanOcrException(string failureCode, string message, bool isClientError = false)
        : base(message)
    {
        FailureCode = failureCode;
        IsClientError = isClientError;
    }

    public string FailureCode { get; }

    /// <summary>True when the uploaded file is at fault (400); false when the OCR service is (503).</summary>
    public bool IsClientError { get; }
}

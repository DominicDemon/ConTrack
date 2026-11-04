namespace ContractManagement.Domain.Entities.Master;

public partial class SmsProviderCallback
{
    public long SmsProviderCallbackId { get; set; }
    public long? SmsOutboxId { get; set; }
    public string? SmsProviderCallbackProviderSmsId { get; set; }
    public string? SmsProviderCallbackRequestId { get; set; }
    public string? SmsProviderCallbackSendStatus { get; set; }
    public string SmsProviderCallbackRawQuery { get; set; } = null!;
    public string? SmsProviderCallbackSourceIp { get; set; }
    public bool SmsProviderCallbackIsProcessed { get; set; }
    public DateTime ReceivedAt { get; set; }
}

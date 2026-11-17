namespace ContractManagement.API.Domains.CustomerAccess;

public sealed class CustomerOtpOptions
{
    public const string SectionName = "CustomerOtp";

    public string? HashKey { get; set; }

    public string? EncryptionKey { get; set; }

    public string Provider { get; set; } = "Fake";

    public string? ProviderEndpoint { get; set; }

    public string? ProviderApiKey { get; set; }

    public string? GsmModemPortName { get; set; }

    public int GsmModemBaudRate { get; set; } = 115200;

    public int GsmModemCommandTimeoutSeconds { get; set; } = 15;

    public int MaxDeliveryAttempts { get; set; } = 3;

    public int RetryDelaySeconds { get; set; } = 30;

    public bool UsesSmtp => string.Equals(Provider, "Smtp", StringComparison.OrdinalIgnoreCase);

    public bool UsesGsmModem => string.Equals(Provider, "GsmModem", StringComparison.OrdinalIgnoreCase);

    public bool HasValidGsmModemSettings => !string.IsNullOrWhiteSpace(GsmModemPortName)
        && GsmModemBaudRate is >= 1200 and <= 921600
        && GsmModemCommandTimeoutSeconds is >= 3 and <= 60;
}

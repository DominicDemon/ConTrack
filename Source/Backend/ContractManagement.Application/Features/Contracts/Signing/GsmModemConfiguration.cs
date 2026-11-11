namespace ContractManagement.Application.Features.Contracts.Signing;

public sealed class GsmModemConfiguration
{
    public const string SectionName = "GsmModem";

    public string? PortName { get; set; }
    public int BaudRate { get; set; } = 115200;
    public int CommandTimeoutSeconds { get; set; } = 15;

    public bool IsValid => !string.IsNullOrWhiteSpace(PortName)
        && BaudRate is >= 1200 and <= 921600
        && CommandTimeoutSeconds is >= 3 and <= 60;
}

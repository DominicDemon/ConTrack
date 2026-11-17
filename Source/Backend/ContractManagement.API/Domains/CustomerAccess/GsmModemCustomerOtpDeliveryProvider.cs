using System.IO.Ports;
using System.Text;
using System.Text.RegularExpressions;
using ContractManagement.Application.Features.Contracts.Signing;
using Microsoft.Extensions.Options;

namespace ContractManagement.API.Domains.CustomerAccess;

/// <summary>Sends one-segment Unicode OTP SMS through the configured physical GSM modem.</summary>
public sealed class GsmModemCustomerOtpDeliveryProvider : ICustomerOtpDeliveryProvider, IGsmModemSmsProvider, IDisposable
{
    private static readonly Regex ValidPhone = new(@"^\+?[0-9]{8,15}$", RegexOptions.CultureInvariant);
    private readonly GsmModemConfiguration _options;
    private readonly ILogger<GsmModemCustomerOtpDeliveryProvider> _logger;
    private readonly SemaphoreSlim _portLock = new(1, 1);
    private bool _disposed;

    public GsmModemCustomerOtpDeliveryProvider(
        IOptions<GsmModemConfiguration> options,
        ILogger<GsmModemCustomerOtpDeliveryProvider> logger)
    {
        _options = options.Value;
        _logger = logger;
        if (!_options.IsValid)
        {
            throw new InvalidOperationException("GSM modem port, baud rate, and timeout configuration are invalid.");
        }
    }

    public async Task DeliverAsync(CustomerOtpDeliveryMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (!ValidPhone.IsMatch(message.PhoneNumberNormalized))
        {
            throw new ArgumentException("The customer phone number is not valid for SMS delivery.", nameof(message));
        }

        if (!Regex.IsMatch(message.Otp, "^[0-9]{6}$", RegexOptions.CultureInvariant))
        {
            throw new ArgumentException("The OTP must contain exactly six digits.", nameof(message));
        }

        await _portLock.WaitAsync(cancellationToken);
        try
        {
            await Task.Run(() => SendSynchronously(message, cancellationToken), cancellationToken);
        }
        finally
        {
            _portLock.Release();
        }
    }

    private void SendSynchronously(CustomerOtpDeliveryMessage message, CancellationToken cancellationToken)
    {
        using var port = new SerialPort(
            _options.PortName!,
            _options.BaudRate,
            Parity.None,
            8,
            StopBits.One)
        {
            Handshake = Handshake.None,
            ReadTimeout = 250,
            WriteTimeout = _options.CommandTimeoutSeconds * 1000,
            NewLine = "\r",
            DtrEnable = true,
            RtsEnable = true
        };

        port.Open();
        port.DiscardInBuffer();
        SendCommand(port, "AT", "OK", cancellationToken);
        SendCommand(port, "ATE0", "OK", cancellationToken);
        var sim = SendCommand(port, "AT+CPIN?", "OK", cancellationToken);
        if (!sim.Contains("READY", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The GSM modem SIM is not ready; check the SIM PIN and status.");
        }

        var registration = SendCommand(port, "AT+CREG?", "OK", cancellationToken);
        if (!Regex.IsMatch(registration, @"\+CREG:\s*\d,\s*[15](?:\D|$)", RegexOptions.CultureInvariant))
        {
            throw new InvalidOperationException("The GSM modem is not registered on a mobile network.");
        }

        SendCommand(port, "AT+CMGF=1", "OK", cancellationToken);
        SendCommand(port, "AT+CSCS=\"UCS2\"", "OK", cancellationToken);

        var phoneHex = Convert.ToHexString(Encoding.BigEndianUnicode.GetBytes(message.PhoneNumberNormalized));
        port.Write($"AT+CMGS=\"{phoneHex}\"\r");
        _ = ReadUntil(port, ">", cancellationToken);
        var expiry = message.ExpiresAt?.ToUniversalTime().ToString("HH:mm 'UTC'") ?? "5 minutes";
        var text = $"ConTrack OTP: {message.Otp}. Valid until {expiry}. Do not share this code.";
        var bodyHex = Convert.ToHexString(Encoding.BigEndianUnicode.GetBytes(text));
        port.Write(bodyHex);
        port.Write(((char)26).ToString());

        try
        {
            var result = ReadUntil(port, "OK", cancellationToken);
            if (!result.Contains("+CMGS:", StringComparison.OrdinalIgnoreCase))
            {
                throw new GsmSmsDeliveryUncertainException(
                    "The modem returned no SMS reference after accepting the message.",
                    new TimeoutException("Missing +CMGS response."));
            }
            _logger.LogInformation("GSM modem accepted an OTP SMS on port {PortName}.", _options.PortName);
        }
        catch (Exception exception) when (exception is TimeoutException or OperationCanceledException or IOException)
        {
            // Once Ctrl+Z has been sent, a timeout cannot distinguish a lost SMS from a lost modem reply.
            throw new GsmSmsDeliveryUncertainException(
                "The modem accepted the SMS command but its final delivery response was not confirmed.", exception);
        }
    }

    private string SendCommand(SerialPort port, string command, string expected, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        port.Write(command + "\r");
        return ReadUntil(port, expected, cancellationToken);
    }

    private string ReadUntil(SerialPort port, string expected, CancellationToken cancellationToken)
    {
        var response = new StringBuilder();
        var deadline = DateTime.UtcNow.AddSeconds(_options.CommandTimeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                if (port.BytesToRead > 0)
                {
                    response.Append(port.ReadExisting());
                    var text = response.ToString();
                    if (text.Contains("ERROR", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException("The GSM modem rejected an AT command.");
                    }

                    if (text.Contains(expected, StringComparison.OrdinalIgnoreCase))
                    {
                        return text;
                    }
                }
            }
            catch (TimeoutException)
            {
                // Keep polling until the command deadline.
            }

            Thread.Sleep(50);
        }

        throw new TimeoutException($"GSM modem response timed out while waiting for {expected}.");
    }

    public Task SendOtpAsync(
        string phoneNumber,
        string otp,
        DateTime expiresAtUtc,
        CancellationToken cancellationToken = default) =>
        DeliverAsync(new CustomerOtpDeliveryMessage(phoneNumber, otp, ExpiresAt: expiresAtUtc), cancellationToken);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _portLock.Dispose();
        _disposed = true;
    }
}


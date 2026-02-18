using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Whispr.Core.Models;
using Whispr.Core.Protocol;

namespace Whispr.Client.Services;

/// <summary>
/// Manages the TLS control channel to the server.
/// </summary>
public sealed class ConnectionService : IDisposable
{
    private const int HeartbeatIntervalMs = 25000; // 25 seconds - keep under server's 60s timeout

    private TcpClient? _tcpClient;
    private SslStream? _stream;
    private CancellationTokenSource? _heartbeatCts;
    private bool _disposed;

    /// <summary>
    /// Connects to the server and establishes TLS.
    /// </summary>
    /// <param name="host">Server hostname or IP.</param>
    /// <param name="port">Control port (default 8443).</param>
    /// <param name="allowDevCert">If true, accept self-signed certs for localhost (dev only).</param>
    /// <param name="acceptUnverifiedCert">If true, accept any certificate (user explicitly confirmed despite validation failure).</param>
    /// <param name="pinnedCertHash">Optional base64-encoded SHA256 of server cert's SPKI for certificate pinning.</param>
    public async Task ConnectAsync(string host, int port = 8443, bool allowDevCert = false, bool acceptUnverifiedCert = false, string? pinnedCertHash = null, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _tcpClient = new TcpClient();
        await _tcpClient.ConnectAsync(host, port, ct);

        RemoteCertificateValidationCallback? callback = null;
        if (!string.IsNullOrEmpty(pinnedCertHash))
            callback = CreatePinValidator(pinnedCertHash);
        else if (acceptUnverifiedCert)
            callback = AcceptAnyCertValidator;
        else if (allowDevCert)
            callback = CreateDevCertValidator(host);

        var checkRevocation = callback is null;
        _stream = new SslStream(_tcpClient.GetStream(), leaveInnerStreamOpen: false, callback);
        await _stream.AuthenticateAsClientAsync(host, null, System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13, checkRevocation);

        _heartbeatCts = new CancellationTokenSource();
        _ = RunHeartbeatAsync(_heartbeatCts.Token);
    }

    private async Task RunHeartbeatAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested && !_disposed)
            {
                await Task.Delay(HeartbeatIntervalMs, ct);
                if (_disposed || _stream is null) break;
                try
                {
                    await SendAsync(MessageTypes.Ping, new { }, ct);
                }
                catch { break; }
            }
        }
        catch (OperationCanceledException) { }
    }

    private static readonly RemoteCertificateValidationCallback AcceptAnyCertValidator =
        (_, _, _, _) => true;

    private static RemoteCertificateValidationCallback CreatePinValidator(string expectedHashBase64)
    {
        byte[] expectedHash;
        try
        {
            expectedHash = Convert.FromBase64String(expectedHashBase64);
        }
        catch
        {
            return (_, _, _, _) => false;
        }
        if (expectedHash.Length != 32) return (_, _, _, _) => false;

        return (_, certificate, _, _) =>
        {
            if (certificate is null) return false;
            try
            {
                using var cert = new X509Certificate2((X509Certificate)certificate);
                var spki = cert.PublicKey.ExportSubjectPublicKeyInfo();
                var actualHash = SHA256.HashData(spki);
                return CryptographicOperations.FixedTimeEquals(expectedHash, actualHash);
            }
            catch
            {
                return false;
            }
        };
    }

    private static RemoteCertificateValidationCallback CreateDevCertValidator(string host) =>
        (_, certificate, chain, sslPolicyErrors) =>
        {
            if ((host == "localhost" || host == "127.0.0.1") && sslPolicyErrors != SslPolicyErrors.None)
                return true;
            return sslPolicyErrors == SslPolicyErrors.None;
        };

    /// <summary>
    /// Sends a control message.
    /// </summary>
    public async Task SendAsync(ControlMessage message, CancellationToken ct = default)
    {
        if (_stream is null)
            throw new InvalidOperationException("Not connected.");

        var bytes = ControlProtocol.Serialize(message);
        await _stream.WriteAsync(bytes, ct);
    }

    /// <summary>
    /// Sends a typed message.
    /// </summary>
    public async Task SendAsync<T>(string type, T payload, CancellationToken ct = default)
    {
        var bytes = ControlProtocol.Serialize(type, payload);
        if (_stream is null)
            throw new InvalidOperationException("Not connected.");
        await _stream.WriteAsync(bytes, ct);
    }

    /// <summary>
    /// Reads the next control message.
    /// </summary>
    public async Task<ControlMessage?> ReadAsync(CancellationToken ct = default)
    {
        if (_stream is null)
            throw new InvalidOperationException("Not connected.");

        return await ControlProtocol.TryReadAsync(_stream, ct);
    }

    /// <summary>
    /// Whether connected to the server.
    /// </summary>
    public bool IsConnected => _stream is not null && _tcpClient?.Connected == true;

    public void Dispose()
    {
        if (_disposed) return;
        _heartbeatCts?.Cancel();
        _heartbeatCts?.Dispose();
        _stream?.Dispose();
        _tcpClient?.Dispose();
        _disposed = true;
    }
}

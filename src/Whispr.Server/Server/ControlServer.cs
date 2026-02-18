using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using Whispr.Core.Protocol;
using Whispr.Server.Handlers;

namespace Whispr.Server.Server;

/// <summary>
/// TCP/TLS control channel server.
/// </summary>
public sealed class ControlServer(ServerOptions options, ControlMessageRouter handler)
{
    private readonly int _port = options.ControlPort;
    private readonly X509Certificate2 _certificate = LoadCertificate(options.CertificatePath, options.CertificatePassword);
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;

    private static X509Certificate2 LoadCertificate(string path, string password)
    {
        var fullPath = Path.IsPathRooted(path) ? path : Path.Combine(AppContext.BaseDirectory, path);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"Certificate not found: {fullPath}. Generate with: dotnet dev-certs https -ep cert.pfx -p \"\" (in Server project dir)");

        var data = File.ReadAllBytes(fullPath);
        return X509CertificateLoader.LoadPkcs12(data, password, X509KeyStorageFlags.EphemeralKeySet);
    }

    public async Task StartAsync(CancellationToken ct = default)
    {
        _listener = new TcpListener(IPAddress.Any, _port);
        _listener.Start();
        ServerLog.Info($"Control server listening on port {_port} (TLS)");

        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        while (!_cts.Token.IsCancellationRequested)
        {
            try
            {
                var tcpClient = await _listener.AcceptTcpClientAsync(_cts.Token);
                var remote = tcpClient.Client.RemoteEndPoint?.ToString() ?? "?";
                ServerLog.Info($"Client connected from {remote}");
                _ = HandleClientAsync(tcpClient, _cts.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                ServerLog.Error($"Accept error: {ex.Message}");
            }
        }
    }

    public void Stop()
    {
        _cts?.Cancel();
        _listener?.Stop();
    }

    private const int ReceiveTimeoutMs = 60000; // 60 seconds - disconnect if no data received

    private async Task HandleClientAsync(TcpClient tcpClient, CancellationToken ct)
    {
        var state = new SessionState();
        try
        {
            tcpClient.ReceiveTimeout = ReceiveTimeoutMs;

            await using var stream = new SslStream(tcpClient.GetStream(), leaveInnerStreamOpen: false);
            await stream.AuthenticateAsServerAsync(_certificate, clientCertificateRequired: false, checkCertificateRevocation: false);

            while (!ct.IsCancellationRequested)
            {
                var message = await ControlProtocol.TryReadAsync(stream, ct);
                if (message is null)
                    break;

                await handler.HandleAsync(message, stream, state, ct);
            }
        }
        catch (Exception ex)
        {
            ServerLog.Error($"Client error: {ex.Message}");
        }
        finally
        {
            if (state.User is not null)
            {
                ServerLog.Info($"Client disconnected: {state.User.Username}");
                handler.OnClientDisconnected(state);
            }
            tcpClient.Dispose();
        }
    }
}

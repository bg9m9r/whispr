using Whispr.Client.Services;
using Whispr.Client.ViewModels;
using Whispr.Core.Protocol;

namespace Whispr.Client.Tests.Fakes;

/// <summary>
/// No-op implementation of ILoginViewHost for unit testing.
/// Records calls for assertion. Configurable for cert dialogs.
/// </summary>
public sealed class FakeLoginViewHost : ILoginViewHost
{
    public int ShowChannelViewCallCount { get; private set; }
    public int CloseCallCount { get; private set; }
    public int ShowUntrustedCertWarningCallCount { get; private set; }
    public int ShowUnverifiedCertRetryCallCount { get; private set; }
    public int ShowErrorCallCount { get; private set; }
    public string? LastErrorMessage { get; private set; }

    /// <summary>When true, ShowUntrustedCertWarningAsync returns true (user accepts).</summary>
    public bool CertWarningReturnsTrue { get; set; }

    /// <summary>When true, ShowUnverifiedCertRetryDialogAsync returns true (user retries).</summary>
    public bool CertRetryReturnsTrue { get; set; }

    public void ShowChannelView(ConnectionService connection, AuthService auth, RoomJoinedResult roomJoined, ServerStatePayload serverState, string host)
    {
        ShowChannelViewCallCount++;
        connection.Dispose();
    }

    public void Close() => CloseCallCount++;

    public Task<bool> ShowUntrustedCertWarningAsync(string host, int port)
    {
        ShowUntrustedCertWarningCallCount++;
        return Task.FromResult(CertWarningReturnsTrue);
    }

    public Task<bool> ShowUnverifiedCertRetryDialogAsync(string host, int port)
    {
        ShowUnverifiedCertRetryCallCount++;
        return Task.FromResult(CertRetryReturnsTrue);
    }

    public Task ShowErrorAsync(string message)
    {
        ShowErrorCallCount++;
        LastErrorMessage = message;
        return Task.CompletedTask;
    }
}

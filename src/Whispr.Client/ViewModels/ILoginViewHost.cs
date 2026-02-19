using Whispr.Client.Services;
using Whispr.Core.Protocol;

namespace Whispr.Client.ViewModels;

/// <summary>
/// Host callbacks for LoginViewModel: navigation and dialogs.
/// Implemented by LoginView.
/// </summary>
public interface ILoginViewHost
{
    void ShowChannelView(ConnectionService connection, AuthService auth, ChannelJoinedResult channelJoined, ServerStatePayload serverState, string host);
    void Close();
    /// <summary>Returns (user confirmed continue, user checked "Save my decision").</summary>
    Task<(bool confirmed, bool saveDecision)> ShowUntrustedCertWarningAsync(string host, int port);
    /// <summary>Returns (user confirmed connect anyway, user checked "Save my decision").</summary>
    Task<(bool confirmed, bool saveDecision)> ShowUnverifiedCertRetryDialogAsync(string host, int port);
    Task ShowErrorAsync(string message);
}

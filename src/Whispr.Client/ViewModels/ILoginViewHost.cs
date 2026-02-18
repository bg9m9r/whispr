using Whispr.Client.Services;
using Whispr.Core.Protocol;

namespace Whispr.Client.ViewModels;

/// <summary>
/// Host callbacks for LoginViewModel: navigation and dialogs.
/// Implemented by LoginView.
/// </summary>
public interface ILoginViewHost
{
    void ShowChannelView(ConnectionService connection, AuthService auth, RoomJoinedResult roomJoined, ServerStatePayload serverState, string host);
    void Close();
    Task<bool> ShowUntrustedCertWarningAsync(string host, int port);
    Task<bool> ShowUnverifiedCertRetryDialogAsync(string host, int port);
    Task ShowErrorAsync(string message);
}

namespace Whispr.Client.ViewModels;

/// <summary>
/// Host callbacks for ChannelViewModel: navigation, dialogs, and window actions.
/// Implemented by ChannelView.
/// </summary>
public interface IChannelViewHost
{
    void ShowSettings();
    void ShowLogin();
    Task ShowPermissionsWindowAsync(Guid userId, string username);
    Task ShowChannelPermissionsWindowAsync(Guid channelId, string channelName);
    void RestartAudioWithNewSettings();
}

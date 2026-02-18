namespace Whispr.Client.ViewModels;

/// <summary>
/// Host callbacks for SettingsViewModel.
/// Implemented by SettingsView, which delegates to MainWindow.
/// </summary>
public interface ISettingsViewHost
{
    void MuteRoomAudioForMicTest();
    void UnmuteRoomAudioForMicTest();
    void RefreshLayout();
    void ShowSettingsBack();
}

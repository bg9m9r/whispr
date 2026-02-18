using Whispr.Client.Services;
using Whispr.Client.Tests.Fakes;
using Whispr.Client.ViewModels;
using Xunit;

namespace Whispr.Client.Tests.ViewModels;

public sealed class SettingsViewModelTests
{
    [Fact]
    public void Labels_FormatCorrectly()
    {
        var audioSettings = new FakeAudioSettings
        {
            MicCutoffDelayMs = 150,
            NoiseGateOpen = 20,
            NoiseGateClose = 10,
            NoiseGateHoldMs = 300
        };
        var deviceProvider = new FakeAudioDeviceProvider { CaptureDevices = [], PlaybackDevices = [] };
        var host = new FakeSettingsViewHost();

        var vm = new SettingsViewModel(host, audioSettings, deviceProvider);

        Assert.Equal("150 ms", vm.CutoffDelayLabel);
        Assert.Equal("20", vm.NoiseGateOpenLabel);
        Assert.Equal("10", vm.NoiseGateCloseLabel);
        Assert.Equal("300 ms", vm.NoiseGateHoldLabel);
        Assert.Equal("20", vm.TestThresholdLabel);
    }

    [Fact]
    public void SaveCommand_CallsAudioSettingsSave()
    {
        var audioSettings = new FakeAudioSettings
        {
            VoiceActivated = false,
            MicCutoffDelayMs = 200,
            NoiseGateOpen = 15,
            NoiseGateClose = 8,
            NoiseGateHoldMs = 240
        };
        var deviceProvider = new FakeAudioDeviceProvider { CaptureDevices = [], PlaybackDevices = [] };
        var host = new FakeSettingsViewHost();

        var vm = new SettingsViewModel(host, audioSettings, deviceProvider);

        vm.VoiceActivated = true;
        vm.CutoffDelayMs = 100;
        vm.SaveCommand.Execute(null);

        Assert.True(audioSettings.SaveCalled);
        Assert.True(audioSettings.VoiceActivated);
        Assert.Equal(100, audioSettings.MicCutoffDelayMs);
    }

    [Fact]
    public void SaveCommand_SetsSavedTextVisible()
    {
        var audioSettings = new FakeAudioSettings();
        var deviceProvider = new FakeAudioDeviceProvider { CaptureDevices = [], PlaybackDevices = [] };
        var host = new FakeSettingsViewHost();

        var vm = new SettingsViewModel(host, audioSettings, deviceProvider);

        Assert.False(vm.SavedTextVisible);
        vm.SaveCommand.Execute(null);
        Assert.True(vm.SavedTextVisible);
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var audioSettings = new FakeAudioSettings();
        var deviceProvider = new FakeAudioDeviceProvider { CaptureDevices = [], PlaybackDevices = [] };
        var host = new FakeSettingsViewHost();

        var vm = new SettingsViewModel(host, audioSettings, deviceProvider);
        vm.Dispose();
    }
}

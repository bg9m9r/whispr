using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Whispr.Client.Services;

namespace Whispr.Client.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject, IDisposable
{
    private const string DefaultLabel = "(Default)";

    private readonly ISettingsViewHost _host;
    private readonly IAudioSettings _audioSettings;
    private readonly IAudioDeviceProvider _deviceProvider;
    private readonly MicTestService _micTest = new();
    private System.Threading.Timer? _hideSavedTimer;
    private bool _updatingThreshold;

    [ObservableProperty]
    private bool _voiceActivated;

    [ObservableProperty]
    private bool _noiseSuppression;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CutoffDelayLabel))]
    private int _cutoffDelayMs = 200;
    public string CutoffDelayLabel => $"{CutoffDelayMs} ms";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NoiseGateOpenLabel))]
    private int _noiseGateOpen = 15;
    public string NoiseGateOpenLabel => $"{NoiseGateOpen}";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NoiseGateCloseLabel))]
    private int _noiseGateClose = 8;
    public string NoiseGateCloseLabel => $"{NoiseGateClose}";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NoiseGateHoldLabel))]
    private int _noiseGateHoldMs = 240;
    public string NoiseGateHoldLabel => $"{NoiseGateHoldMs} ms";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TestThresholdLabel))]
    private int _testThreshold = 15;
    public string TestThresholdLabel => $"{TestThreshold}";

    [ObservableProperty]
    private int _testMicLevel;

    [ObservableProperty]
    private string _testMicStatus = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TestMicButtonText))]
    private bool _isTestRunning;
    public string TestMicButtonText => IsTestRunning ? "Stop test" : "Start test";

    [ObservableProperty]
    private bool _savedTextVisible;

    public List<string> InputDevices { get; }
    public List<string> OutputDevices { get; }

    [ObservableProperty]
    private string _selectedInputDevice = DefaultLabel;

    [ObservableProperty]
    private string _selectedOutputDevice = DefaultLabel;

    public SettingsViewModel(ISettingsViewHost host, IAudioSettings audioSettings, IAudioDeviceProvider deviceProvider)
    {
        _host = host;
        _audioSettings = audioSettings;
        _deviceProvider = deviceProvider;

        var (_, capture, playback, voiceActivated, micCutoffDelayMs, noiseSuppression, noiseGateOpen, noiseGateClose, noiseGateHoldMs) = _audioSettings.Load();
        VoiceActivated = voiceActivated;
        NoiseSuppression = noiseSuppression;
        CutoffDelayMs = micCutoffDelayMs;
        NoiseGateOpen = noiseGateOpen;
        NoiseGateClose = noiseGateClose;
        NoiseGateHoldMs = noiseGateHoldMs;
        TestThreshold = noiseGateOpen;

        InputDevices = [DefaultLabel, .. _deviceProvider.GetCaptureDevices(null)];
        OutputDevices = [DefaultLabel, .. _deviceProvider.GetPlaybackDevices(null)];
        SelectedInputDevice = string.IsNullOrEmpty(capture) ? DefaultLabel : (InputDevices.Contains(capture) ? capture : DefaultLabel);
        SelectedOutputDevice = string.IsNullOrEmpty(playback) ? DefaultLabel : (OutputDevices.Contains(playback) ? playback : DefaultLabel);

        _micTest.OnLevel += level => TestMicLevel = level;
        _micTest.OnFailed += msg => TestMicStatus = msg;
    }

    partial void OnNoiseGateOpenChanged(int value)
    {
        if (!_updatingThreshold)
        {
            _updatingThreshold = true;
            TestThreshold = value;
            _updatingThreshold = false;
        }
    }

    partial void OnTestThresholdChanged(int value)
    {
        if (!_updatingThreshold)
        {
            _updatingThreshold = true;
            NoiseGateOpen = value;
            _updatingThreshold = false;
        }
    }

    private static string? ResolveDevice(string? selected, string defaultLabel)
    {
        if (string.IsNullOrEmpty(selected) || selected == defaultLabel) return null;
        return selected;
    }

    [RelayCommand]
    private void ToggleTestMic()
    {
        if (IsTestRunning)
        {
            _micTest.Stop();
            _host.UnmuteRoomAudioForMicTest();
            IsTestRunning = false;
            TestMicStatus = "";
        }
        else
        {
            var captureDevice = ResolveDevice(SelectedInputDevice, DefaultLabel);
            var playbackDevice = ResolveDevice(SelectedOutputDevice, DefaultLabel);
            _micTest.Start(captureDevice, playbackDevice, NoiseGateOpen, NoiseGateClose, NoiseGateHoldMs, NoiseSuppression);
            _host.MuteRoomAudioForMicTest();
            IsTestRunning = true;
            TestMicStatus = "Listening… (room muted, playback = what others hear)";
        }
    }

    [RelayCommand]
    private void Save()
    {
        _audioSettings.Save(
            null,
            ResolveDevice(SelectedInputDevice, DefaultLabel),
            ResolveDevice(SelectedOutputDevice, DefaultLabel),
            VoiceActivated,
            CutoffDelayMs,
            NoiseSuppression,
            NoiseGateOpen,
            NoiseGateClose,
            NoiseGateHoldMs);
        SavedTextVisible = true;
        _host.RefreshLayout();
        _hideSavedTimer?.Dispose();
        _hideSavedTimer = new System.Threading.Timer(_ => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            SavedTextVisible = false;
            _host.RefreshLayout();
        }), null, 3000, System.Threading.Timeout.Infinite);
    }

    [RelayCommand]
    private void Back()
    {
        CloseSettings();
    }

    [RelayCommand]
    private void CloseWithoutSaving()
    {
        CloseSettings();
    }

    private void CloseSettings()
    {
        if (IsTestRunning)
        {
            _micTest.Stop();
            _host.UnmuteRoomAudioForMicTest();
            IsTestRunning = false;
        }
        _host.ShowSettingsBack();
    }

    public void Dispose()
    {
        _micTest.Dispose();
    }
}

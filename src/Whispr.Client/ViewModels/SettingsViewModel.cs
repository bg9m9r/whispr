using System.Collections.Generic;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Whispr.Client.Services;
using Whispr.Core;

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

    /// <summary>Transmit mode: "voice" | "ptt" | "open".</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsVoiceActivationMode))]
    [NotifyPropertyChangedFor(nameof(IsPushToTalkMode))]
    [NotifyPropertyChangedFor(nameof(IsOpenTransmitMode))]
    [NotifyPropertyChangedFor(nameof(IsVoiceActivationSectionEnabled))]
    [NotifyPropertyChangedFor(nameof(IsPushToTalkSectionEnabled))]
    private string _transmitMode = "ptt";

    /// <summary>True when Voice activation is selected.</summary>
    public bool IsVoiceActivationMode { get => TransmitMode == "voice"; set { if (value) TransmitMode = "voice"; } }

    /// <summary>True when Push-to-talk is selected.</summary>
    public bool IsPushToTalkMode { get => TransmitMode == "ptt"; set { if (value) TransmitMode = "ptt"; } }

    /// <summary>True when Open transmit is selected.</summary>
    public bool IsOpenTransmitMode { get => TransmitMode == "open"; set { if (value) TransmitMode = "open"; } }

    /// <summary>True when voice activation settings (noise gate, etc.) should be enabled.</summary>
    public bool IsVoiceActivationSectionEnabled => TransmitMode == "voice";

    /// <summary>True when push-to-talk settings (key, cutoff) should be enabled.</summary>
    public bool IsPushToTalkSectionEnabled => TransmitMode == "ptt";

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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PttKeyOrButtonDisplay))]
    private string _pttKeyOrButton = "Key:V";

    [ObservableProperty]
    private bool _isListeningForPttKey;

    /// <summary>Client version for display (e.g. settings footer).</summary>
    public string ClientVersion => VersionHelper.GetVersion(typeof(SettingsViewModel).Assembly);

    /// <summary>Display string for version in settings.</summary>
    public string ClientVersionDisplay => $"Version {ClientVersion}";

    /// <summary>Short label for the current PTT binding, e.g. "V", "Space", "Middle mouse".</summary>
    public string PttKeyOrButtonDisplay => PttKeyOrButtonToDisplay(PttKeyOrButton);

    public SettingsViewModel(ISettingsViewHost host, IAudioSettings audioSettings, IAudioDeviceProvider deviceProvider)
    {
        _host = host;
        _audioSettings = audioSettings;
        _deviceProvider = deviceProvider;

        var (_, capture, playback, transmitMode, micCutoffDelayMs, noiseSuppression, noiseGateOpen, noiseGateClose, noiseGateHoldMs, pttKeyOrButton) = _audioSettings.Load();
        TransmitMode = transmitMode is "voice" or "ptt" or "open" ? transmitMode : "ptt";
        NoiseSuppression = noiseSuppression;
        CutoffDelayMs = micCutoffDelayMs;
        NoiseGateOpen = noiseGateOpen;
        NoiseGateClose = noiseGateClose;
        NoiseGateHoldMs = noiseGateHoldMs;
        TestThreshold = noiseGateOpen;
        PttKeyOrButton = pttKeyOrButton ?? "Key:V";

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
    private void StartListeningForPttKey()
    {
        IsListeningForPttKey = true;
    }

    /// <summary>Called by the view when a key or mouse button was captured as the PTT binding.</summary>
    public void SetPttBinding(string keyOrButton)
    {
        PttKeyOrButton = keyOrButton;
        IsListeningForPttKey = false;
    }

    private static string PttKeyOrButtonToDisplay(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "V";
        if (value.StartsWith("Key:", StringComparison.OrdinalIgnoreCase))
        {
            var key = value.Length > 4 ? value[4..] : "";
            return key switch
            {
                "Space" => "Space",
                "Return" => "Enter",
                "Back" => "Backspace",
                _ => key
            };
        }
        if (value.StartsWith("Mouse:", StringComparison.OrdinalIgnoreCase))
        {
            var btn = value.Length > 6 ? value[6..] : "";
            return btn switch
            {
                "Left" => "Left mouse",
                "Right" => "Right mouse",
                "Middle" => "Middle mouse",
                "XButton1" => "Side button 1",
                "XButton2" => "Side button 2",
                _ => btn.Length > 0 ? btn : "Mouse"
            };
        }
        return value;
    }

    [RelayCommand]
    private void Save()
    {
        _audioSettings.Save(
            null,
            ResolveDevice(SelectedInputDevice, DefaultLabel),
            ResolveDevice(SelectedOutputDevice, DefaultLabel),
            TransmitMode,
            CutoffDelayMs,
            NoiseSuppression,
            NoiseGateOpen,
            NoiseGateClose,
            NoiseGateHoldMs,
            PttKeyOrButton);
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

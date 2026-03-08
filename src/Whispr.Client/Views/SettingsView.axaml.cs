using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.VisualTree;
using Whispr.Client.Services;
using Whispr.Client.ViewModels;

namespace Whispr.Client.Views;

public partial class SettingsView : UserControl, ISettingsViewHost
{
    private readonly MainWindow? _window;
    private readonly SettingsViewModel? _viewModel;

    public SettingsView()
    {
        InitializeComponent();
    }

    public SettingsView(MainWindow window) : this()
    {
        _window = window;
        _viewModel = new SettingsViewModel(this, new DefaultAudioSettings(), new DefaultAudioDeviceProvider());
        DataContext = _viewModel;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        Unloaded += (_, _) =>
        {
            if (_viewModel is not null)
            {
                _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
                _viewModel.Dispose();
            }
            RemovePttKeyListenHandlers();
        };
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SettingsViewModel.IsListeningForPttKey)) return;
        if (_viewModel?.IsListeningForPttKey == true)
            AttachPttKeyListenHandlers();
    }

    private void AttachPttKeyListenHandlers()
    {
        var w = _window ?? throw new InvalidOperationException("SettingsView not initialized");
        w.AddHandler(InputElement.KeyDownEvent, OnPttKeyListenKeyDown, handledEventsToo: true);
        w.AddHandler(InputElement.PointerPressedEvent, OnPttKeyListenPointerPressed, handledEventsToo: true);
    }

    private void RemovePttKeyListenHandlers()
    {
        if (_window is null) return;
        _window.RemoveHandler(InputElement.KeyDownEvent, OnPttKeyListenKeyDown);
        _window.RemoveHandler(InputElement.PointerPressedEvent, OnPttKeyListenPointerPressed);
    }

    private void OnPttKeyListenKeyDown(object? sender, KeyEventArgs e)
    {
        var key = e.Key;
        if (key == Avalonia.Input.Key.None) return;
        var s = "Key:" + key;
        _viewModel?.SetPttBinding(s);
        RemovePttKeyListenHandlers();
        e.Handled = true;
    }

    private void OnPttKeyListenPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var props = e.GetCurrentPoint(this).Properties;
        string? button = null;
        if (props.IsLeftButtonPressed) button = "Mouse:Left";
        else if (props.IsRightButtonPressed) button = "Mouse:Right";
        else if (props.IsMiddleButtonPressed) button = "Mouse:Middle";
        else if (props.IsXButton1Pressed) button = "Mouse:XButton1";
        else if (props.IsXButton2Pressed) button = "Mouse:XButton2";
        if (button == null) return;
        var hit = e.Source as Visual;
        while (hit != null)
        {
            if (hit is Button btn && btn.Name == "SetPttKeyButton")
                return;
            hit = hit.GetVisualParent();
        }
        _viewModel?.SetPttBinding(button);
        RemovePttKeyListenHandlers();
        e.Handled = true;
    }

    void ISettingsViewHost.MuteRoomAudioForMicTest() => (_window ?? throw new InvalidOperationException("SettingsView not initialized")).MuteRoomAudioForMicTest();
    void ISettingsViewHost.UnmuteRoomAudioForMicTest() => (_window ?? throw new InvalidOperationException("SettingsView not initialized")).UnmuteRoomAudioForMicTest();
    void ISettingsViewHost.RefreshLayout() => (_window ?? throw new InvalidOperationException("SettingsView not initialized")).RefreshLayout();
    void ISettingsViewHost.ShowSettingsBack() => (_window ?? throw new InvalidOperationException("SettingsView not initialized")).ShowSettingsBack();

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        var src = e.Source as Control;
        while (src != null)
        {
            if (src is TextBox or Button or ComboBox or Slider or ToggleButton or CheckBox or ProgressBar)
                return;
            src = src.Parent as Control;
        }
        var w = this.FindAncestorOfType<Window>();
        if (w != null) w.BeginMoveDrag(e);
    }
}

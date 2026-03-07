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
    private readonly MainWindow _window;
    private readonly SettingsViewModel _viewModel;

    public SettingsView(MainWindow window)
    {
        _window = window;
        _viewModel = new SettingsViewModel(this, new DefaultAudioSettings(), new DefaultAudioDeviceProvider());
        DataContext = _viewModel;
        InitializeComponent();
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        Unloaded += (_, _) =>
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            RemovePttKeyListenHandlers();
            _viewModel.Dispose();
        };
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SettingsViewModel.IsListeningForPttKey)) return;
        if (_viewModel.IsListeningForPttKey)
            AttachPttKeyListenHandlers();
    }

    private void AttachPttKeyListenHandlers()
    {
        _window.AddHandler(InputElement.KeyDownEvent, OnPttKeyListenKeyDown, handledEventsToo: true);
        _window.AddHandler(InputElement.PointerPressedEvent, OnPttKeyListenPointerPressed, handledEventsToo: true);
    }

    private void RemovePttKeyListenHandlers()
    {
        _window.RemoveHandler(InputElement.KeyDownEvent, OnPttKeyListenKeyDown);
        _window.RemoveHandler(InputElement.PointerPressedEvent, OnPttKeyListenPointerPressed);
    }

    private void OnPttKeyListenKeyDown(object? sender, KeyEventArgs e)
    {
        var key = e.Key;
        if (key == Avalonia.Input.Key.None) return;
        var s = "Key:" + key;
        _viewModel.SetPttBinding(s);
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
        _viewModel.SetPttBinding(button);
        RemovePttKeyListenHandlers();
        e.Handled = true;
    }

    void ISettingsViewHost.MuteRoomAudioForMicTest() => _window.MuteRoomAudioForMicTest();
    void ISettingsViewHost.UnmuteRoomAudioForMicTest() => _window.UnmuteRoomAudioForMicTest();
    void ISettingsViewHost.RefreshLayout() => _window.RefreshLayout();
    void ISettingsViewHost.ShowSettingsBack() => _window.ShowSettingsBack();

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

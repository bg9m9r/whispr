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
        Unloaded += (_, _) => _viewModel.Dispose();
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

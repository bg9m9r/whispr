using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Whispr.Client.Services;
using Whispr.Client.ViewModels;
using Whispr.Core.Protocol;

namespace Whispr.Client.Views;

public partial class MainWindow : Window
{
    private ConnectionService? _connection;
    private AuthService? _auth;
    private string _serverHost = "localhost";
    private Control? _previousContent;

    public MainWindow()
    {
        var vm = new MainWindowViewModel();
        vm.CloseRequested += () => Close();
        DataContext = vm;
        InitializeComponent();
        SetContent(new LoginView(this));
        Opened += OnOpened;
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is Button) return;
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        Opened -= OnOpened;
        try
        {
            var screen = Screens.Primary;
            if (screen != null)
                MaxHeight = screen.WorkingArea.Height;
        }
        catch { /* ignore if screen API fails */ }
    }

    private void SetContent(Control content)
    {
        ContentHost.Content = content;
        HeaderBar.IsVisible = content is not LoginView;
        if (content is ChannelView)
        {
            SizeToContent = SizeToContent.Manual;
            Width = 920;
            Height = 620;
        }
        else
        {
            SizeToContent = SizeToContent.WidthAndHeight;
        }
    }

    /// <summary>
    /// Mutes room audio (transmit + playback) while mic test runs. Call UnmuteRoomAudioForMicTest when test stops.
    /// </summary>
    public void MuteRoomAudioForMicTest()
    {
        if (_previousContent is ChannelView cv)
            cv.MuteAudioForMicTest();
    }

    /// <summary>
    /// Unmutes room audio after mic test ends.
    /// </summary>
    public void UnmuteRoomAudioForMicTest()
    {
        if (_previousContent is ChannelView cv)
            cv.UnmuteAudioForMicTest();
    }

    /// <summary>
    /// Forces the window to re-measure and resize to fit content (e.g. when "Settings saved" appears).
    /// </summary>
    public void RefreshLayout()
    {
        ContentHost.InvalidateMeasure();
        InvalidateMeasure();
    }

    public void ShowSettings()
    {
        _previousContent = ContentHost.Content as Control;
        SetContent(new SettingsView(this));
    }

    public void ShowSettingsBack()
    {
        if (_previousContent is ChannelView channelView)
            channelView.RestartAudioWithNewSettings();
        SetContent(_previousContent ?? new LoginView(this));
        _previousContent = null;
    }

    public void ShowLogin()
    {
        _connection?.Dispose();
        _connection = null;
        _auth = null;
        SetContent(new LoginView(this));
    }

    public void ShowChannelView(ConnectionService connection, AuthService auth, ChannelJoinedResult channelResult, ServerStatePayload serverState, string serverHost)
    {
        _connection = connection;
        _auth = auth;
        _serverHost = serverHost;
        SetContent(new ChannelView(this, connection, auth, channelResult, serverState, serverHost));
    }
}

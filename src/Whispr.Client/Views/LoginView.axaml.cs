using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Whispr.Client.Services;
using Whispr.Client.ViewModels;
using Whispr.Core.Protocol;

namespace Whispr.Client.Views;

public partial class LoginView : UserControl, ILoginViewHost
{
    private readonly MainWindow _window;
    private readonly LoginViewModel _viewModel;

    public LoginView(MainWindow window)
    {
        _window = window;
        _viewModel = new LoginViewModel(this, new LoginService(), new ServerSettingsStore(), new CredentialStore());
        DataContext = _viewModel;
        InitializeComponent();
        AddHandler(KeyDownEvent, OnKeyDown, handledEventsToo: true);
    }

    void ILoginViewHost.ShowChannelView(ConnectionService connection, AuthService auth, ChannelJoinedResult channelJoined, ServerStatePayload serverState, string host)
    {
        _window.ShowChannelView(connection, auth, channelJoined, serverState, host);
    }

    void ILoginViewHost.Close() => _window.Close();

    async Task<bool> ILoginViewHost.ShowUntrustedCertWarningAsync(string host, int port)
    {
        var message = $"You are about to connect to {host}:{port} which may use a self-signed or unverified certificate.\n\n" +
                      "Only proceed if you trust this server and have verified its identity. " +
                      "Connecting to an untrusted server could expose your credentials and audio to interception.";
        return await DialogService.ShowYesNoAsync("Security Warning", message, "Continue", "Cancel", isWarning: false);
    }

    async Task<bool> ILoginViewHost.ShowUnverifiedCertRetryDialogAsync(string host, int port)
    {
        var message = $"The server's certificate for {host}:{port} could not be validated.\n\n" +
                      "For production servers, use a trusted certificate (e.g., from Let's Encrypt). " +
                      "Only connect anyway if you have verified this server's identity.\n\n" +
                      "Connecting to an untrusted server could expose your credentials and audio to interception.";
        return await DialogService.ShowYesNoAsync("Certificate Error", message, "Connect anyway", "Cancel", isWarning: true);
    }

    async Task ILoginViewHost.ShowErrorAsync(string message)
    {
        await DialogService.ShowOkAsync("Error", message);
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        var src = e.Source as Control;
        while (src != null)
        {
            if (src is TextBox or Button or ComboBox or Slider or ToggleButton)
                return;
            src = src.Parent as Control;
        }
        var w = this.FindAncestorOfType<Window>();
        if (w != null) w.BeginMoveDrag(e);
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => _window.Close();

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            _viewModel.ConnectCommand.Execute(null);
        }
    }
}

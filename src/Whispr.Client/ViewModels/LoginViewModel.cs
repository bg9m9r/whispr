using Whispr.Client.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Whispr.Client.Services;
using Whispr.Core.Protocol;

namespace Whispr.Client.ViewModels;

public sealed partial class LoginViewModel : ObservableObject
{
    private readonly ILoginViewHost _host;
    private readonly ILoginService _loginService;
    private readonly IServerSettingsStore _serverSettings;
    private readonly ICredentialStore _credentialStore;

    [ObservableProperty]
    private string _hostText = "localhost";

    [ObservableProperty]
    private string _portText = "8443";

    [ObservableProperty]
    private string _username = "";

    [ObservableProperty]
    private string _password = "";

    [ObservableProperty]
    private bool _isConnecting;

    [ObservableProperty]
    private bool _rememberMe;

    [ObservableProperty]
    private bool _hasSavedCredentials;

    public LoginViewModel(ILoginViewHost host, ILoginService loginService, IServerSettingsStore serverSettings, ICredentialStore credentialStore)
    {
        _host = host;
        _loginService = loginService;
        _serverSettings = serverSettings;
        _credentialStore = credentialStore;
        LoadSavedCredentials();
    }

    private void LoadSavedCredentials()
    {
        var settings = _serverSettings.Load();
        if (settings.LastHost is { } host)
        {
            HostText = host;
            PortText = settings.LastPort.ToString();
        }
        RememberMe = settings.RememberMe;

        var entry = settings.Servers.FirstOrDefault(s => s.Host == (settings.LastHost ?? "") && s.Port == settings.LastPort);
        if (entry is not null && !string.IsNullOrEmpty(entry.Username))
        {
            Username = entry.Username;
            if (entry.RememberPassword)
            {
                try
                {
                    var service = CredentialServiceKey(entry.Host, entry.Port);
                    var pwd = _credentialStore.Retrieve(service, entry.Username);
                    if (pwd is not null)
                        Password = pwd;
                }
                catch
                {
                    // Keychain may be unavailable (e.g. Linux without libsecret)
                }
            }
        }

        HasSavedCredentials = settings.Servers.Count > 0;
    }

    private static string CredentialServiceKey(string host, int port) => $"whisper://{host}:{port}";

    private void SaveCredentials(string host, int port, string username, string password)
    {
        var settings = _serverSettings.Load();
        var servers = settings.Servers.Where(s => !(s.Host == host && s.Port == port)).ToList();
        servers.Add(new ServerEntry(host, port, username, RememberMe));
        _serverSettings.Save(new ServerSettings(host, port, RememberMe, servers));

        var service = CredentialServiceKey(host, port);
        try
        {
            if (RememberMe)
                _credentialStore.Store(service, username, password);
            else
                _credentialStore.Remove(service, username);
        }
        catch
        {
            // Keychain may be unavailable (e.g. Linux without libsecret)
        }

        HasSavedCredentials = servers.Count > 0;
    }

    [RelayCommand]
    private void ClearSavedCredentials()
    {
        var settings = _serverSettings.Load();
        foreach (var entry in settings.Servers)
        {
            var service = CredentialServiceKey(entry.Host, entry.Port);
            try
            {
                _credentialStore.Remove(service, entry.Username);
            }
            catch
            {
                // Keychain may be unavailable
            }
        }
        _serverSettings.Save(ServerSettings.Default);
        HasSavedCredentials = false;
        RememberMe = false;
    }

    [RelayCommand]
    private async Task ConnectAsync()
    {
        IsConnecting = true;

        var host = HostText?.Trim() ?? "localhost";
        if (!int.TryParse(PortText, out var port))
            port = 8443;
        var username = Username?.Trim() ?? "";
        var password = Password ?? "";

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            await _host.ShowErrorAsync("Please enter username and password.");
            IsConnecting = false;
            return;
        }

        try
        {
            var allowDevCert = ShouldAllowDevCert(host);
            if (allowDevCert)
            {
                var confirmed = await _host.ShowUntrustedCertWarningAsync(host, port);
                if (!confirmed)
                {
                    IsConnecting = false;
                    return;
                }
            }

            var pinnedHash = ServerTrustStore.GetPinnedHash(host, port);
            var parameters = new ConnectParams(host, port, username, password, allowDevCert, pinnedHash, AcceptUnverifiedCert: false);

            var outcome = await _loginService.ConnectAsync(parameters);

            if (outcome is ConnectFailed failed)
            {
                if (failed.IsCertificateError)
                {
                    var confirmed = await _host.ShowUnverifiedCertRetryDialogAsync(host, port);
                    if (confirmed)
                    {
                        var retryParams = new ConnectParams(host, port, username, password, AllowDevCert: false, null, AcceptUnverifiedCert: true);
                        var retryOutcome = await _loginService.ConnectAsync(retryParams);

                        if (retryOutcome is ConnectFailed retryFailed)
                        {
                            await _host.ShowErrorAsync(retryFailed.Error);
                            IsConnecting = false;
                            return;
                        }

                        var retrySuccess = (ConnectSuccess)retryOutcome;
                        if (RememberMe)
                            SaveCredentials(host, port, username, password);
                        _host.ShowChannelView(retrySuccess.Connection, retrySuccess.Auth, retrySuccess.ChannelJoined, retrySuccess.ServerState, host);
                    }
                }
                else
                {
                    await _host.ShowErrorAsync(failed.Error);
                }
                IsConnecting = false;
                return;
            }

            var success = (ConnectSuccess)outcome;
            if (RememberMe)
                SaveCredentials(host, port, username, password);
            _host.ShowChannelView(success.Connection, success.Auth, success.ChannelJoined, success.ServerState, host);
        }
        catch (Exception ex)
        {
            await _host.ShowErrorAsync(ex.Message);
        }
        finally
        {
            IsConnecting = false;
        }
    }

    private static bool ShouldAllowDevCert(string host)
    {
        if (host != "localhost" && host != "127.0.0.1")
            return false;
#if DEBUG
        return true;
#else
        return Environment.GetEnvironmentVariable("WHISPR_ALLOW_DEV_CERT") == "1";
#endif
    }
}

using System.Collections.ObjectModel;
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

    /// <summary>Saved servers for one-click connect. Populated from ServerSettings.Servers.</summary>
    public ObservableCollection<SavedServerItem> SavedServers { get; } = new();

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
        RefreshSavedServers();
    }

    private void RefreshSavedServers()
    {
        SavedServers.Clear();
        foreach (var entry in _serverSettings.Load().Servers)
            SavedServers.Add(new SavedServerItem(entry));
    }

    private static string CredentialServiceKey(string host, int port) => $"whisper://{host}:{port}";

    private void SaveCredentials(string host, int port, string username, string password)
    {
        var settings = _serverSettings.Load();
        var servers = settings.Servers.Where(s => !(s.Host == host && s.Port == port)).ToList();
        var service = CredentialServiceKey(host, port);
        var rememberPassword = RememberMe;
        if (RememberMe)
        {
            try
            {
                _credentialStore.Store(service, username, password);
            }
            catch
            {
                // Keychain unavailable (e.g. Linux without libsecret or GCM_CREDENTIAL_STORE not set)
                rememberPassword = false;
            }
        }
        else
        {
            try
            {
                _credentialStore.Remove(service, username);
            }
            catch
            {
                // Keychain may be unavailable
            }
        }
        servers.Add(new ServerEntry(host, port, username, rememberPassword));
        _serverSettings.Save(new ServerSettings(host, port, RememberMe, servers));

        HasSavedCredentials = servers.Count > 0;
        RefreshSavedServers();
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
        ServerTrustStore.ClearAcceptedUnverified();
        HasSavedCredentials = false;
        RememberMe = false;
        SavedServers.Clear();
    }

    [RelayCommand]
    private async Task ConnectToSavedAsync(SavedServerItem? item)
    {
        if (item is null || IsConnecting) return;

        var entry = item.Entry;
        string? password = null;
        try
        {
            password = _credentialStore.Retrieve(CredentialServiceKey(entry.Host, entry.Port), entry.Username);
        }
        catch
        {
            // Keychain unavailable
        }

        if (string.IsNullOrEmpty(password))
        {
            HostText = entry.Host;
            PortText = entry.Port.ToString();
            Username = entry.Username;
            Password = "";
            await _host.ShowErrorAsync("Saved password couldn't be read for this server (e.g. keychain was unavailable when it was saved). Enter your password below and sign in to save it again.");
            return;
        }

        await TryConnectAndNavigateAsync(entry.Host, entry.Port, entry.Username, password, saveIfRememberMe: false);
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

        await TryConnectAndNavigateAsync(host, port, username, password, saveIfRememberMe: true);
    }

    /// <summary>
    /// Runs cert checks, connects, handles cert retry, and on success navigates to channel view. Optionally saves credentials when saveIfRememberMe and RememberMe are true.
    /// </summary>
    private async Task TryConnectAndNavigateAsync(string host, int port, string username, string password, bool saveIfRememberMe)
    {
        IsConnecting = true;
        try
        {
            var allowDevCert = ShouldAllowDevCert(host);
            var acceptedUnverified = ServerTrustStore.IsAcceptedUnverified(host, port);
            var skippedDevWarning = ServerTrustStore.IsSkippedDevCertWarning(host, port);
            if (allowDevCert && !acceptedUnverified && !skippedDevWarning)
            {
                var (confirmed, saveDecision) = await _host.ShowUntrustedCertWarningAsync(host, port);
                if (!confirmed)
                {
                    IsConnecting = false;
                    return;
                }
                if (saveDecision)
                    ServerTrustStore.AddSkippedDevCertWarning(host, port);
            }

            var pinnedHash = acceptedUnverified ? null : ServerTrustStore.GetPinnedHash(host, port);
            var parameters = new ConnectParams(host, port, username, password,
                allowDevCert, pinnedHash, AcceptUnverifiedCert: acceptedUnverified);

            var outcome = await _loginService.ConnectAsync(parameters);

            if (outcome is ConnectFailed failed)
            {
                if (failed.IsCertificateError)
                {
                    var (confirmed, saveDecision) = await _host.ShowUnverifiedCertRetryDialogAsync(host, port);
                    if (confirmed)
                    {
                        if (saveDecision)
                            ServerTrustStore.AddAcceptedUnverified(host, port);
                        var retryParams = new ConnectParams(host, port, username, password, AllowDevCert: false, null, AcceptUnverifiedCert: true);
                        var retryOutcome = await _loginService.ConnectAsync(retryParams);

                        if (retryOutcome is ConnectFailed retryFailed)
                        {
                            await _host.ShowErrorAsync(retryFailed.Error);
                            IsConnecting = false;
                            return;
                        }

                        var retrySuccess = (ConnectSuccess)retryOutcome;
                        if (saveIfRememberMe && RememberMe)
                            SaveCredentials(host, port, username, password);
                        UpdateLastServer(host, port);
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
            if (saveIfRememberMe && RememberMe)
                SaveCredentials(host, port, username, password);
            UpdateLastServer(host, port);
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

    private void UpdateLastServer(string host, int port)
    {
        var settings = _serverSettings.Load();
        _serverSettings.Save(new ServerSettings(host, port, settings.RememberMe, settings.Servers));
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

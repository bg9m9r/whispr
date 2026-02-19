using Whispr.Client.Models;
using Whispr.Client.Services;
using Whispr.Client.Tests.Fakes;
using Whispr.Client.ViewModels;
using Whispr.Core.Protocol;
using Xunit;

namespace Whispr.Client.Tests.ViewModels;

public sealed class LoginViewModelTests
{
    private static LoginViewModel CreateVm(
        FakeLoginViewHost host,
        FakeLoginService loginService,
        FakeServerSettingsStore? serverSettings = null,
        FakeCredentialStore? credentialStore = null)
    {
        return new LoginViewModel(
            host,
            loginService,
            serverSettings ?? new FakeServerSettingsStore(),
            credentialStore ?? new FakeCredentialStore());
    }

    [Fact]
    public async Task Connect_EmptyCredentials_ShowsError()
    {
        var host = new FakeLoginViewHost();
        var loginService = new FakeLoginService();
        var vm = CreateVm(host, loginService);

        vm.HostText = "example.com";
        vm.Username = "";
        vm.Password = "";

        await vm.ConnectCommand.ExecuteAsync(null!);

        Assert.Equal(1, host.ShowErrorCallCount);
        Assert.Equal("Please enter username and password.", host.LastErrorMessage);
        Assert.Equal(0, host.ShowChannelViewCallCount);
    }

    [Fact]
    public async Task Connect_CertWarningCancelled_NoShowChannelView()
    {
        var host = new FakeLoginViewHost { CertWarningReturnsTrue = false };
        var loginService = new FakeLoginService();
        loginService.SetSuccess(
            new ChannelJoinedResult(Guid.NewGuid(), "General", [], [], []),
            new ServerStatePayload { Channels = [], CanCreateChannel = true });
        var vm = CreateVm(host, loginService);

        vm.HostText = "localhost";
        vm.Username = "alice";
        vm.Password = "secret";

        await vm.ConnectCommand.ExecuteAsync(null!);

        Assert.Equal(1, host.ShowUntrustedCertWarningCallCount);
        Assert.Equal(0, host.ShowChannelViewCallCount);
    }

    [Fact]
    public async Task Connect_CertWarningAccepted_ShowsChannelView()
    {
        var host = new FakeLoginViewHost { CertWarningReturnsTrue = true };
        var roomJoined = new ChannelJoinedResult(Guid.NewGuid(), "General", [], [], []);
        var serverState = new ServerStatePayload { Channels = [], CanCreateChannel = true };
        var loginService = new FakeLoginService();
        loginService.SetSuccess(roomJoined, serverState);
        var vm = CreateVm(host, loginService);

        vm.HostText = "localhost";
        vm.Username = "alice";
        vm.Password = "secret";

        await vm.ConnectCommand.ExecuteAsync(null!);

        Assert.Equal(1, host.ShowUntrustedCertWarningCallCount);
        Assert.Equal(1, host.ShowChannelViewCallCount);
    }

    [Fact]
    public async Task Connect_ServiceReturnsFailure_ShowsError()
    {
        var host = new FakeLoginViewHost();
        var loginService = new FakeLoginService();
        loginService.SetFailure("Invalid credentials");
        var vm = CreateVm(host, loginService);

        vm.HostText = "example.com";
        vm.Username = "alice";
        vm.Password = "wrong";

        await vm.ConnectCommand.ExecuteAsync(null!);

        Assert.Equal(1, host.ShowErrorCallCount);
        Assert.Equal("Invalid credentials", host.LastErrorMessage);
        Assert.Equal(0, host.ShowChannelViewCallCount);
    }

    [Fact]
    public async Task Connect_ServiceReturnsSuccess_ShowsChannelView()
    {
        var host = new FakeLoginViewHost();
        var roomJoined = new ChannelJoinedResult(Guid.NewGuid(), "General", [], [], []);
        var serverState = new ServerStatePayload { Channels = [], CanCreateChannel = true };
        var loginService = new FakeLoginService();
        loginService.SetSuccess(roomJoined, serverState);
        var vm = CreateVm(host, loginService);

        vm.HostText = "example.com";
        vm.Username = "alice";
        vm.Password = "secret";

        await vm.ConnectCommand.ExecuteAsync(null!);

        Assert.Equal(0, host.ShowErrorCallCount);
        Assert.Equal(1, host.ShowChannelViewCallCount);
    }

    [Fact]
    public async Task Connect_CertErrorRetryAccepted_ShowsChannelView()
    {
        var host = new FakeLoginViewHost { CertRetryReturnsTrue = true };
        var roomJoined = new ChannelJoinedResult(Guid.NewGuid(), "General", [], [], []);
        var serverState = new ServerStatePayload { Channels = [], CanCreateChannel = true };
        var loginService = new FakeLoginService();
        loginService.SetFailure("Certificate validation failed", isCertificateError: true);
        var conn = new ConnectionService();
        loginService.AddOutcome(new ConnectSuccess(conn, new AuthService(conn), roomJoined, serverState));
        var vm = CreateVm(host, loginService);

        vm.HostText = "example.com";
        vm.Username = "alice";
        vm.Password = "secret";

        await vm.ConnectCommand.ExecuteAsync(null!);

        Assert.Equal(1, host.ShowUnverifiedCertRetryCallCount);
        Assert.Equal(1, host.ShowChannelViewCallCount);
    }

    [Fact]
    public async Task Connect_CertErrorRetryCancelled_ShowsErrorOnly()
    {
        var host = new FakeLoginViewHost { CertRetryReturnsTrue = false };
        var loginService = new FakeLoginService();
        loginService.SetFailure("Certificate validation failed", isCertificateError: true);
        var vm = CreateVm(host, loginService);

        vm.HostText = "example.com";
        vm.Username = "alice";
        vm.Password = "secret";

        await vm.ConnectCommand.ExecuteAsync(null!);

        Assert.Equal(1, host.ShowUnverifiedCertRetryCallCount);
        Assert.Equal(0, host.ShowChannelViewCallCount);
    }

    [Fact]
    public void LoadSavedCredentials_PrefillsHostPortUsernamePassword()
    {
        var serverSettings = new FakeServerSettingsStore();
        serverSettings.Set(new ServerSettings("myserver.com", 9000, true,
            [new ServerEntry("myserver.com", 9000, "bob", true)]));
        var credentialStore = new FakeCredentialStore();
        credentialStore.Store("whisper://myserver.com:9000", "bob", "mypassword");

        var vm = CreateVm(new FakeLoginViewHost(), new FakeLoginService(), serverSettings, credentialStore);

        Assert.Equal("myserver.com", vm.HostText);
        Assert.Equal("9000", vm.PortText);
        Assert.Equal("bob", vm.Username);
        Assert.Equal("mypassword", vm.Password);
        Assert.True(vm.RememberMe);
        Assert.True(vm.HasSavedCredentials);
    }

    [Fact]
    public void LoadSavedCredentials_RememberPasswordFalse_DoesNotLoadPassword()
    {
        var serverSettings = new FakeServerSettingsStore();
        serverSettings.Set(new ServerSettings("example.com", 8443, false,
            [new ServerEntry("example.com", 8443, "alice", false)]));
        var credentialStore = new FakeCredentialStore();
        credentialStore.Store("whisper://example.com:8443", "alice", "stored-but-should-not-load");

        var vm = CreateVm(new FakeLoginViewHost(), new FakeLoginService(), serverSettings, credentialStore);

        Assert.Equal("example.com", vm.HostText);
        Assert.Equal("alice", vm.Username);
        Assert.Equal("", vm.Password);
        Assert.False(vm.RememberMe);
    }

    [Fact]
    public async Task Connect_RememberMeTrue_SavesCredentials()
    {
        var host = new FakeLoginViewHost();
        var loginService = new FakeLoginService();
        loginService.SetSuccess(
            new ChannelJoinedResult(Guid.NewGuid(), "General", [], [], []),
            new ServerStatePayload { Channels = [], CanCreateChannel = true });
        var serverSettings = new FakeServerSettingsStore();
        var credentialStore = new FakeCredentialStore();

        var vm = CreateVm(host, loginService, serverSettings, credentialStore);
        vm.HostText = "srv.example.com";
        vm.PortText = "9443";
        vm.Username = "carol";
        vm.Password = "secret123";
        vm.RememberMe = true;

        await vm.ConnectCommand.ExecuteAsync(null!);

        var saved = serverSettings.Load();
        Assert.Equal("srv.example.com", saved.LastHost);
        Assert.Equal(9443, saved.LastPort);
        Assert.True(saved.RememberMe);
        Assert.Single(saved.Servers);
        Assert.Equal("carol", saved.Servers[0].Username);
        Assert.True(saved.Servers[0].RememberPassword);
        Assert.Equal("secret123", credentialStore.Retrieve("whisper://srv.example.com:9443", "carol"));
        Assert.True(vm.HasSavedCredentials);
    }

    [Fact]
    public async Task Connect_RememberMeFalse_DoesNotSaveCredentials()
    {
        var host = new FakeLoginViewHost();
        var loginService = new FakeLoginService();
        loginService.SetSuccess(
            new ChannelJoinedResult(Guid.NewGuid(), "General", [], [], []),
            new ServerStatePayload { Channels = [], CanCreateChannel = true });
        var serverSettings = new FakeServerSettingsStore();
        var credentialStore = new FakeCredentialStore();

        var vm = CreateVm(host, loginService, serverSettings, credentialStore);
        vm.HostText = "example.com";
        vm.Username = "dave";
        vm.Password = "nopass";
        vm.RememberMe = false;

        await vm.ConnectCommand.ExecuteAsync(null!);

        var saved = serverSettings.Load();
        Assert.Empty(saved.Servers);
        Assert.Null(credentialStore.Retrieve("whisper://example.com:8443", "dave"));
    }

    [Fact]
    public void ClearSavedCredentials_RemovesAllAndResetsState()
    {
        var serverSettings = new FakeServerSettingsStore();
        serverSettings.Set(new ServerSettings("old.com", 8443, true,
            [new ServerEntry("old.com", 8443, "user", true)]));
        var credentialStore = new FakeCredentialStore();
        credentialStore.Store("whisper://old.com:8443", "user", "oldpass");

        var vm = CreateVm(new FakeLoginViewHost(), new FakeLoginService(), serverSettings, credentialStore);
        Assert.True(vm.HasSavedCredentials);

        vm.ClearSavedCredentialsCommand.Execute(null!);

        var saved = serverSettings.Load();
        Assert.Null(saved.LastHost);
        Assert.Equal(8443, saved.LastPort);
        Assert.Empty(saved.Servers);
        Assert.Null(credentialStore.Retrieve("whisper://old.com:8443", "user"));
        Assert.False(vm.HasSavedCredentials);
        Assert.False(vm.RememberMe);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Whispr.Server;
using Whispr.Server.Data;
using Whispr.Server.Handlers;
using Whispr.Server.Repositories;
using Whispr.Server.Server;
using Whispr.Server.Services;

var config = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: true)
    .AddJsonFile("appsettings.Development.json", optional: true)
    .Build();

var options = config.Get<ServerOptions>() ?? new ServerOptions();

// Certificate password from env var takes precedence (avoids storing in config file)
var certPassword = Environment.GetEnvironmentVariable("WHISPR_CERT_PASSWORD");
if (certPassword is not null)
    options = new ServerOptions
    {
        ControlPort = options.ControlPort,
        AudioPort = options.AudioPort,
        CertificatePath = options.CertificatePath,
        CertificatePassword = certPassword,
        DatabasePath = options.DatabasePath,
        SeedTestUsers = options.SeedTestUsers,
        TokenLifetimeHours = options.TokenLifetimeHours
    };

// Ensure database schema exists and seed defaults
DbInitializer.Initialize(options.DatabasePath);

var services = new ServiceCollection();
services.AddSingleton(options);

// Repositories
if (string.IsNullOrWhiteSpace(options.DatabasePath))
{
    services.AddSingleton<IUserRepository, InMemoryUserRepository>();
    services.AddSingleton<IPermissionRepository, InMemoryPermissionRepository>();
    services.AddSingleton<IChannelRepository, InMemoryChannelRepository>();
    services.AddSingleton<IMessageRepository, InMemoryMessageRepository>();
}
else
{
    var path = Path.GetFullPath(options.DatabasePath);
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    var connectionString = $"Data Source={path}";

    services.AddDbContextFactory<WhisprDbContext>(opts => opts.UseSqlite(connectionString));
    services.AddSingleton<IUserRepository, EfUserStore>();
    services.AddSingleton<IPermissionRepository>(sp => (IPermissionRepository)sp.GetRequiredService<IUserRepository>());
    services.AddSingleton<IChannelRepository, EfChannelRepository>();
    services.AddSingleton<IMessageRepository, EfMessageRepository>();
}

// Services
services.AddSingleton<IAuthService>(sp => new AuthService(
    sp.GetRequiredService<IUserRepository>(),
    sp.GetRequiredService<IPermissionRepository>(),
    options));
services.AddSingleton<IChannelService, ChannelManager>();
services.AddSingleton<IMessageService, MessageService>();
services.AddSingleton<UdpEndpointRegistry>();
services.AddSingleton<ControlMessageRouter>();
services.AddSingleton<ControlServer>();
services.AddSingleton<AudioRelayServer>();

await using var provider = services.BuildServiceProvider();

// Admin CLI: add-user <username> <password> [--admin]
if (args is ["add-user", ..] addUserArgs)
{
    var adminFlag = addUserArgs.Length >= 4 && addUserArgs[3] == "--admin";
    var addUsername = addUserArgs.Length >= 2 ? addUserArgs[1] : "";
    var addPassword = addUserArgs.Length >= 3 ? addUserArgs[2] : "";
    if (string.IsNullOrWhiteSpace(options.DatabasePath) || string.IsNullOrWhiteSpace(addUsername) || string.IsNullOrWhiteSpace(addPassword))
    {
        Console.Error.WriteLine("Usage: add-user <username> <password> [--admin]");
        return 1;
    }
    var auth = provider.GetRequiredService<IAuthService>();
    if (auth.AddUser(addUsername, addPassword, adminFlag ? Whispr.Core.Models.UserRole.Admin : Whispr.Core.Models.UserRole.User))
    {
        Console.WriteLine($"User '{addUsername}' added{(adminFlag ? " as admin" : "")}.");
        return 0;
    }
    Console.Error.WriteLine($"User '{addUsername}' already exists.");
    return 1;
}

var controlServer = provider.GetRequiredService<ControlServer>();
var audioRelay = provider.GetRequiredService<AudioRelayServer>();

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

var controlTask = controlServer.StartAsync(cts.Token);
var audioTask = audioRelay.StartAsync(cts.Token);

ServerLog.Info("Whispr server running. Press Ctrl+C to stop.");

try
{
    await Task.WhenAll(controlTask, audioTask);
}
catch (OperationCanceledException)
{
}

controlServer.Stop();
audioRelay.Stop();
ServerLog.Info("Server stopped.");
return 0;

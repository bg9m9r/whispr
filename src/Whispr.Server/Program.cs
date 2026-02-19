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

// Env overrides (avoid storing secrets in config)
var certPassword = Environment.GetEnvironmentVariable("WHISPR_CERT_PASSWORD");
var messageEncryptionKey = Environment.GetEnvironmentVariable("WHISPR_MESSAGE_ENCRYPTION_KEY")
    ?? options.MessageEncryptionKeyBase64;
if (certPassword is not null || messageEncryptionKey is not null)
{
    options = new ServerOptions
    {
        ControlPort = options.ControlPort,
        AudioPort = options.AudioPort,
        CertificatePath = options.CertificatePath,
        CertificatePassword = certPassword ?? options.CertificatePassword,
        DatabasePath = options.DatabasePath,
        SeedTestUsers = options.SeedTestUsers,
        TokenLifetimeHours = options.TokenLifetimeHours,
        MessageEncryptionKeyBase64 = messageEncryptionKey ?? options.MessageEncryptionKeyBase64
    };
}

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
    IMessageEncryption messageEncryption;
    if (!string.IsNullOrWhiteSpace(options.MessageEncryptionKeyBase64))
    {
        byte[] keyBytes;
        try
        {
            keyBytes = Convert.FromBase64String(options.MessageEncryptionKeyBase64.Trim());
        }
        catch (FormatException)
        {
            Console.Error.WriteLine("WHISPR_MESSAGE_ENCRYPTION_KEY must be valid base64.");
            return 1;
        }
        if (keyBytes.Length != 32)
        {
            Console.Error.WriteLine("WHISPR_MESSAGE_ENCRYPTION_KEY must decode to 32 bytes (AES-256).");
            return 1;
        }
        messageEncryption = new AesMessageEncryption(keyBytes);
    }
    else
    {
        var isAddUser = args is ["add-user", ..];
        var devSkipEncryption = string.Equals(Environment.GetEnvironmentVariable("WHISPR_DEV_SKIP_MESSAGE_ENCRYPTION"), "1", StringComparison.Ordinal)
            || string.Equals(Environment.GetEnvironmentVariable("WHISPR_DEV_SKIP_MESSAGE_ENCRYPTION"), "true", StringComparison.OrdinalIgnoreCase);
        if (isAddUser)
            messageEncryption = new ThrowingMessageEncryptionStub();
        else if (devSkipEncryption)
        {
            Console.WriteLine("WARNING: WHISPR_DEV_SKIP_MESSAGE_ENCRYPTION is set. Messages are not encrypted at rest. Use only for local testing.");
            messageEncryption = new NoOpMessageEncryption();
        }
        else
        {
            Console.Error.WriteLine("When using a database, WHISPR_MESSAGE_ENCRYPTION_KEY (32-byte key, base64) is required for message encryption at rest.");
            Console.Error.WriteLine("For local testing only, set WHISPR_DEV_SKIP_MESSAGE_ENCRYPTION=1 to skip encryption (not for production).");
            return 1;
        }
    }
    services.AddSingleton<IMessageEncryption>(messageEncryption);

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

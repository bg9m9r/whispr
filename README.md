# Whispr

Voice chat application with server and client components.

## Requirements

- **.NET 10 SDK** — [Install .NET](https://dotnet.microsoft.com/download)

## Building

```bash
cd src
dotnet build Whispr.sln
```

## Running

**Server:**
```bash
cd src
dotnet run --project Whispr.Server
```

**Client:**
```bash
cd src
dotnet run --project Whispr.Client
```

---

## Installing Whispr

Pre-built client and server binaries are published on [GitHub Releases](/releases). Download the archive for your platform and architecture.

### Client

| Platform    | Download                    | Run |
|-------------|-----------------------------|-----|
| Windows x64 | `whispr-client-win-x64.zip` | Extract, then run `Whispr.Client.exe` |
| Linux x64  | `whispr-client-linux-x64.tar.gz` | Extract, `chmod +x Whispr.Client`, then `./Whispr.Client` |
| macOS Intel | `whispr-client-osx-x64.tar.gz` | Extract, then `./Whispr.Client` |
| macOS Apple Silicon | `whispr-client-osx-arm64.tar.gz` | Extract, then `./Whispr.Client` |

Ensure [system dependencies](#client-whisprclient) are installed on Linux (OpenAL, X11, etc.).

### Server

| Platform    | Download                    | Run |
|-------------|-----------------------------|-----|
| Windows x64 | `whispr-server-win-x64.zip` | Extract, add `cert.pfx` and optionally edit `appsettings.json`, then run `Whispr.Server.exe` |
| Linux x64  | `whispr-server-linux-x64.tar.gz` | Extract, add `cert.pfx`, then `./Whispr.Server` |

For production server setup (Docker, systemd, certificates), see [Server setup](docs/SERVER_SETUP.md).

---

## Dependencies

### Server (Whispr.Server)

| Package | Version | Purpose |
|---------|---------|---------|
| Microsoft.Extensions.Configuration | 9.0 | Configuration system |
| Microsoft.Extensions.Configuration.Json | 9.0 | JSON configuration |
| Microsoft.Extensions.Configuration.Binder | 9.0 | Configuration binding |
| Microsoft.Extensions.DependencyInjection | 9.0 | DI container |
| Microsoft.EntityFrameworkCore.Sqlite | 9.0 | SQLite database |
| Microsoft.EntityFrameworkCore.Design | 9.0 | EF Core tooling (design-time) |

**System dependencies:** None. SQLite is embedded; no separate database installation required.

---

### Client (Whispr.Client)

| Package | Version | Purpose |
|---------|---------|---------|
| Avalonia | 11.2 | Cross-platform UI framework |
| Avalonia.Desktop | 11.2 | Desktop platform support |
| Avalonia.Themes.Fluent | 11.2 | Fluent design theme |
| CommunityToolkit.Mvvm | 8.4 | MVVM helpers |
| RNNoise.NET | 1.0 | Noise suppression |
| Silk.NET.OpenAL | 2.23 | Audio capture/playback |
| Silk.NET.OpenAL.Extensions.Enumeration | 2.23 | Audio device enumeration |
| Silk.NET.OpenAL.Extensions.EXT | 2.23 | OpenAL extensions |
| Concentus | 2.2 | Opus codec |
| Devlooped.CredentialManager | 2.6 | Secure credential storage (keychain) |

**System dependencies (Linux):**

| Purpose | Debian/Ubuntu | Arch | Fedora |
|---------|---------------|------|--------|
| GUI (Avalonia) | `libx11-6 libice6 libsm6 libfontconfig1 libgdiplus` | `libx11 libice libsm fontconfig` | `libX11 libICE libSM fontconfig libgdiplus` |
| Audio (OpenAL) | `libopenal1` | `openal` | `openal-soft` |
| Credential storage ("Remember me") | `libsecret-1-0` | `libsecret` | `libsecret` |

**Install on Debian/Ubuntu:**
```bash
sudo apt install libx11-6 libice6 libsm6 libfontconfig1 libgdiplus libopenal1 libsecret-1-0
```

**Install on Arch:**
```bash
sudo pacman -S libx11 libice libsm fontconfig openal libsecret
```

**Install on Fedora:**
```bash
sudo dnf install libX11 libICE libSM fontconfig libgdiplus openal-soft libsecret
```

> **Note:** On Linux, the "Remember me" feature stores passwords via the Secret Service API (libsecret), which uses GNOME Keyring, KWallet, or similar. Without `libsecret`, credential storage will fail gracefully; the app still works, but saved passwords will not persist.

**System dependencies (Windows):**

| Requirement | Details |
|-------------|---------|
| OS | Windows 11, 10, or 8.1 (Windows 7 has limited support) |
| GUI | Built-in (Avalonia uses native Windows APIs) |
| Audio | Built-in (OpenAL Soft bundled or system audio) |
| Credential storage | Windows Credential Manager (built-in) |

No additional software installation required.

**System dependencies (macOS):**

| Requirement | Details |
|-------------|---------|
| OS | macOS 10.14 (Mojave) through 15 (Sequoia); 10.13 (High Sierra) supported with limited GPU acceleration |
| Architecture | Intel (x64) or Apple Silicon (arm64) |
| GUI | Built-in (Avalonia uses Skia + Metal) |
| Audio | Built-in (Core Audio) |
| Credential storage | macOS Keychain (built-in) |

No additional software installation required. For signing and notarizing distributable builds, Xcode is required.

---

### Core (Whispr.Core)

Shared protocol and types. No external package dependencies.

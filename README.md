# Whispr

Voice chat application with server and client components.

---

## Install the client

Download the client for your platform from [GitHub Releases](/releases).

| Platform    | Download                    | Run |
|-------------|-----------------------------|-----|
| Windows x64 | `whispr-client-win-x64.zip` | Extract, then run `Whispr.Client.exe` |
| Linux x64  | `whispr-client-linux-x64.tar.gz` | Extract, `chmod +x Whispr.Client`, then `./Whispr.Client` |
| macOS Intel | `whispr-client-osx-x64.tar.gz` | Extract, then `./Whispr.Client` |
| macOS Apple Silicon | `whispr-client-osx-arm64.tar.gz` | Extract, then `./Whispr.Client` |

**Linux:** Install system dependencies first (OpenAL, X11, etc.):

```bash
# Debian/Ubuntu
sudo apt install libx11-6 libice6 libsm6 libfontconfig1 libgdiplus libopenal1 libsecret-1-0
# Arch
sudo pacman -S libx11 libice libsm fontconfig openal libsecret
# Fedora
sudo dnf install libX11 libICE libSM fontconfig libgdiplus openal-soft libsecret
```

**Windows:** Windows 11, 10, or 8.1. No extra installs.

**macOS:** macOS 10.14–15, Intel or Apple Silicon. No extra installs.

---

## Server (self-hosting)

Download the server from [GitHub Releases](/releases). You need a TLS certificate (`cert.pfx`) to run it.

| Platform    | Download                    | Run |
|-------------|-----------------------------|-----|
| Windows x64 | `whispr-server-win-x64.zip` | Extract, add `cert.pfx`, then run `Whispr.Server.exe` |
| Linux x64  | `whispr-server-linux-x64.tar.gz` | Extract, add `cert.pfx`, then `./Whispr.Server` |

For production setup (Docker, systemd, Let's Encrypt), see **[Server setup](docs/SERVER_SETUP.md)**.

---

## Documentation

- [Contributing](docs/CONTRIBUTING.md) — build from source, run tests, open a PR
- [Server setup](docs/SERVER_SETUP.md) — Docker, systemd, certificates
- [Certificate guide](docs/CERTIFICATE_GUIDE.md) — custom CA, self-signed, pinning

# Installing Whispr on Bazzite Linux

Bazzite is a Fedora-based immutable OS. Use these steps to install dependencies for the Whispr client (running the pre-built release or building from source).

## Option A: Run the pre-built client

### 1. Install runtime dependencies

Bazzite uses `rpm-ostree` for package layering. Install the required libraries:

```bash
rpm-ostree install libX11 libICE libSM fontconfig libgdiplus openal-soft libsecret
```

**Note:** Layering requires a reboot. After the command completes, reboot your system for the packages to take effect.

### 2. Optional: Noise suppression (RNNoise)

For voice-activated noise suppression, install rnnoise:

```bash
rpm-ostree install rnnoise
```

Then reboot.

### 3. Download and run the client

1. Download `whispr-client-linux-x64.tar.gz` from [GitHub Releases](https://github.com/YOUR_ORG/whispr/releases).
2. Extract: `tar xzf whispr-client-linux-x64.tar.gz`
3. Make executable: `chmod +x Whispr.Client`
4. Run: `./Whispr.Client`

---

## Option B: Build from source

### 1. Install runtime dependencies (same as Option A)

```bash
rpm-ostree install libX11 libICE libSM fontconfig libgdiplus openal-soft libsecret rnnoise
```

Reboot after installation.

### 2. Install .NET 10 SDK

Use the official Microsoft repository or the Fedora .NET package:

```bash
# Microsoft repo (recommended for latest .NET 10)
sudo dnf install -y dotnet-sdk-10.0

# Or, if not yet in Fedora repos, add Microsoft's repo:
# https://learn.microsoft.com/en-us/dotnet/core/install/linux-fedora
```

### 3. Clone and build

```bash
git clone https://github.com/YOUR_ORG/whispr.git
cd whispr/src
dotnet build Whispr.sln
dotnet run --project Whispr.Client
```

---

## Alternative: Use Toolbox (no reboot)

If you prefer not to layer packages (avoids reboot and keeps the base image clean), use **Toolbox** or **Distrobox** to get a mutable Fedora container:

```bash
# Create a Fedora toolbox
toolbox create -c whispr-build

# Enter the container
toolbox enter -c whispr-build

# Inside the container, use dnf normally (no rpm-ostree)
sudo dnf install libX11 libICE libSM fontconfig libgdiplus openal-soft libsecret rnnoise dotnet-sdk-10.0

# Build and run
cd /path/to/whispr/src
dotnet run --project Whispr.Client
```

The client runs inside the container but can use your host's display and audio (X11/Wayland and PipeWire are typically shared).

---

## Audio (PipeWire)

Bazzite uses PipeWire by default. OpenAL Soft works with PipeWire; no extra configuration is needed. If the mic does not appear, ensure PipeWire is running:

```bash
systemctl --user status pipewire
```

---

## Summary: Package list

| Package      | Purpose                          |
|-------------|-----------------------------------|
| libX11      | X11 display                      |
| libICE      | X11 inter-client exchange        |
| libSM       | X11 session management           |
| fontconfig  | Font configuration               |
| libgdiplus  | GDI+ compatibility (Avalonia)     |
| openal-soft | Audio capture and playback       |
| libsecret   | Credential storage               |
| rnnoise     | Optional noise suppression       |

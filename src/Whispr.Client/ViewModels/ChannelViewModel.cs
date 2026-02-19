using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Whispr.Client.Models;
using Whispr.Client.Services;
using Whispr.Core.Models;
using Whispr.Core.Protocol;

namespace Whispr.Client.ViewModels;

public sealed partial class ChannelViewModel : ObservableObject, IDisposable
{
    private const int AudioPort = 8444;
    private const int UiUpdateIntervalMs = 50;
    private const long SpeakingTimeoutMs = 400;

    private readonly IChannelService _channelService;
    private readonly IAuthService _auth;
    private readonly IChannelViewHost _host;
    private readonly string _serverHost;
    private readonly Guid _myUserId;

    private ChannelJoinedResult _channelResult;
    private ServerTreeNode _rootNode = null!;
    private AudioService? _audioService;
    private System.Timers.Timer? _uiTimer;
    private readonly ConcurrentDictionary<uint, long> _lastSpeakingByClientId = new();
    private bool _disposed;

    [ObservableProperty]
    private string _currentChannelName = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCreateChannelEnabled))]
    private bool _canCreateChannel;

    [ObservableProperty]
    private bool _isMuted;

    partial void OnIsMutedChanged(bool value) => _audioService?.SetMuteSend(value);

    [ObservableProperty]
    private bool _isReceiveMuted;

    partial void OnIsReceiveMutedChanged(bool value) => _audioService?.SetMuteReceive(value);

    [ObservableProperty]
    private string _micStatusText = "";

    [ObservableProperty]
    private bool _isMicStatusVisible;

    [ObservableProperty]
    private bool _isTalkButtonVisible = true;

    [ObservableProperty]
    private bool _isDisconnectEnabled = true;

    [ObservableProperty]
    private string _createChannelName = "";

    [ObservableProperty]
    private string _pingDisplayText = "—";

    // Context menu state (set by view on right-click)
    private ServerTreeNode? _contextMenuTargetNode;

    public ServerTreeNode? ContextMenuTargetNode
    {
        get => _contextMenuTargetNode;
        set
        {
            if (_contextMenuTargetNode == value) return;
            _contextMenuTargetNode = value;
            OnPropertyChanged();
            UpdatePermissionTargetItems();
            OnPropertyChanged(nameof(IsCreateChannelVisible));
            OnPropertyChanged(nameof(IsCreateChannelEnabled));
            OnPropertyChanged(nameof(IsEditPermissionsVisible));
        }
    }

    public bool IsCreateChannelVisible => _contextMenuTargetNode == _rootNode;
    public bool IsCreateChannelEnabled => IsCreateChannelVisible && CanCreateChannel;
    /// <summary>Visible when right-clicking on a user (edit that user's permissions) or a channel (edit channel permissions).</summary>
    public bool IsEditPermissionsVisible => _contextMenuTargetNode is { Kind: NodeKind.User } || _contextMenuTargetNode is { Kind: NodeKind.Channel };

    public ObservableCollection<ServerTreeNode> ServerTreeRootItems { get; } = new();
    public ObservableCollection<PermissionTargetItem> PermissionTargetItems { get; } = new();

    public ChannelViewModel(IChannelService channelService, IAuthService auth, IChannelViewHost host,
        ChannelJoinedResult channelResult, ServerStatePayload serverState, string serverHost)
    {
        _channelService = channelService;
        _auth = auth;
        _host = host;
        _serverHost = serverHost;
        _myUserId = auth.User?.Id ?? Guid.Empty;
        _channelResult = channelResult;

        _channelService.ServerStateReceived += OnServerStateReceived;
        _channelService.RoomJoinedReceived += OnRoomJoinedReceived;
        _channelService.RoomLeftReceived += OnRoomLeftReceived;
        _channelService.PingLatencyUpdated += OnPingLatencyUpdated;

        BuildTree();
        CurrentChannelName = channelResult.ChannelName;
        CanCreateChannel = serverState.CanCreateChannel;
        _ = EnterRoomAsync();
    }

    private void UpdatePermissionTargetItems()
    {
        PermissionTargetItems.Clear();
        var node = _contextMenuTargetNode;
        if (node is null) return;

        if (node.Kind == NodeKind.User && node.UserId.HasValue)
        {
            var username = _channelService.GetUsernameForUserId(node.UserId.Value) ?? node.DisplayName;
            PermissionTargetItems.Add(new PermissionTargetItem(node.UserId.Value, username));
        }
        else if (node.Kind == NodeKind.Channel && node.ChannelId.HasValue)
        {
            var channel = _channelService.ServerState.Channels.FirstOrDefault(c => c.Id == node.ChannelId.Value);
            foreach (var m in channel?.Members ?? [])
                PermissionTargetItems.Add(new PermissionTargetItem(m.UserId, m.Username));
        }
    }

    private void OnServerStateReceived(ServerStatePayload state)
    {
        RefreshTree();
        CanCreateChannel = state.CanCreateChannel;
    }

    private void OnRoomJoinedReceived(ChannelJoinedResult result)
    {
        _channelResult = result;
        StopAudio();
        CurrentChannelName = result.ChannelName;
        _ = EnterRoomAsync();
        _ = _channelService.RequestServerStateAsync();
    }

    private void OnRoomLeftReceived()
    {
        StopAudio();
        _channelService.Stop();
        _host.ShowLogin();
    }

    private void OnPingLatencyUpdated(int? ms)
    {
        PingDisplayText = ms switch
        {
            null => "—",
            -1 => "timeout",
            _ => $"{ms} ms"
        };
    }

    private void BuildTree()
    {
        _rootNode = new ServerTreeNode
        {
            DisplayName = "Server",
            Kind = NodeKind.Server
        };

        foreach (var ch in _channelService.ServerState.Channels)
        {
            var channelNode = new ServerTreeNode
            {
                DisplayName = ch.Name,
                Kind = NodeKind.Channel,
                ChannelId = ch.Id,
                IsCurrentChannel = ch.Id == _channelResult.ChannelId
            };

            foreach (var m in ch.Members)
            {
                channelNode.Children.Add(new ServerTreeNode
                {
                    DisplayName = m.Username,
                    Kind = NodeKind.User,
                    UserId = m.UserId,
                    ClientId = m.ClientId,
                    IsMe = m.UserId == _myUserId,
                    IsAdmin = m.IsAdmin
                });
            }

            _rootNode.Children.Add(channelNode);
        }

        ServerTreeRootItems.Clear();
        ServerTreeRootItems.Add(_rootNode);
    }

    private void RefreshTree()
    {
        foreach (var ch in _channelService.ServerState.Channels)
        {
            var channelNode = _rootNode.Children.FirstOrDefault(n => n.ChannelId == ch.Id);
            if (channelNode is null) continue;

            channelNode.IsCurrentChannel = ch.Id == _channelResult.ChannelId;

            var existingUserIds = new HashSet<Guid>(channelNode.Children.Select(c => c.UserId!.Value));
            var currentUserIds = new HashSet<Guid>(ch.MemberIds);

            foreach (var uid in currentUserIds.Where(uid => !existingUserIds.Contains(uid)))
            {
                var m = ch.Members.FirstOrDefault(x => x.UserId == uid);
                channelNode.Children.Add(new ServerTreeNode
                {
                    DisplayName = m?.Username ?? uid.ToString(),
                    Kind = NodeKind.User,
                    UserId = uid,
                    ClientId = m?.ClientId ?? 0,
                    IsMe = uid == _myUserId,
                    IsAdmin = m?.IsAdmin ?? false
                });
            }

            for (var i = channelNode.Children.Count - 1; i >= 0; i--)
            {
                var child = channelNode.Children[i];
                if (child.Kind == NodeKind.User && child.UserId.HasValue && !currentUserIds.Contains(child.UserId.Value))
                    channelNode.Children.RemoveAt(i);
            }

            foreach (var child in channelNode.Children.Where(c => c.Kind == NodeKind.User))
            {
                var m = ch.Members.FirstOrDefault(x => x.UserId == child.UserId);
                if (m is not null)
                {
                    child.ClientId = m.ClientId;
                    child.IsAdmin = m.IsAdmin;
                }
                child.IsSpeaking = _channelService.UserIdToClientId.TryGetValue(child.UserId!.Value, out var cid) &&
                    _lastSpeakingByClientId.TryGetValue(cid, out var ticks) &&
                    (Environment.TickCount64 - ticks) < SpeakingTimeoutMs;
            }
        }

        OnPropertyChanged(nameof(ServerTreeRootItems));
        TreeRefreshed?.Invoke();
    }

    private void UpdateSpeakingIndicators()
    {
        var now = Environment.TickCount64;
        foreach (var ch in _channelService.ServerState.Channels)
        {
            var channelNode = _rootNode.Children.FirstOrDefault(n => n.ChannelId == ch.Id);
            if (channelNode is null) continue;
            foreach (var child in channelNode.Children.Where(c => c.Kind == NodeKind.User && c.UserId.HasValue))
            {
                child.IsSpeaking = _channelService.UserIdToClientId.TryGetValue(child.UserId!.Value, out var cid) &&
                    _lastSpeakingByClientId.TryGetValue(cid, out var ticks) &&
                    (now - ticks) < SpeakingTimeoutMs;
            }
        }
    }

    private async Task EnterRoomAsync()
    {
        IsDisconnectEnabled = false;

        try
        {
            var clientId = (uint)Random.Shared.Next(1, int.MaxValue);
            await _auth.RegisterUdpAsync(clientId);

            _audioService = new AudioService();
            _audioService.OnFrameReceived += cid => _lastSpeakingByClientId[cid] = Environment.TickCount64;
            _audioService.OnCaptureFailed += msg =>
            {
                MicStatusText = msg;
                IsMicStatusVisible = true;
            };

            var (audioBackend, captureDevice, playbackDevice, voiceActivated, micCutoffDelayMs, noiseSuppression, noiseGateOpen, noiseGateClose, noiseGateHoldMs) = AudioSettings.Load();
            var pushToTalk = !voiceActivated;
            ClientLog.Info($"Starting audio (clientId={clientId}, pushToTalk={pushToTalk}, voiceActivated={voiceActivated}, cutoffDelay={micCutoffDelayMs}ms)");

            var resolvedCapture = ResolveCaptureDevice(audioBackend, captureDevice);
            var resolvedPlayback = ResolvePlaybackDevice(audioBackend, playbackDevice);

            _audioService.Start(_serverHost, AudioPort, clientId, _channelResult.AudioKey,
                captureDeviceName: resolvedCapture, playbackDeviceName: resolvedPlayback, pushToTalk: pushToTalk, voiceActivated: voiceActivated, micCutoffDelayMs: micCutoffDelayMs,
                noiseSuppression: noiseSuppression, noiseGateOpen: noiseGateOpen, noiseGateClose: noiseGateClose, noiseGateHoldMs: noiseGateHoldMs);

            IsTalkButtonVisible = !voiceActivated;
            _audioService.SetMuteSend(IsMuted);
            _audioService.SetMuteReceive(IsReceiveMuted);

            _uiTimer = new System.Timers.Timer(UiUpdateIntervalMs);
            _uiTimer.Elapsed += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(UpdateSpeakingIndicators);
            _uiTimer.Start();
        }
        catch (Exception ex)
        {
            ClientLog.Info($"Failed to start audio: {ex.Message}");
        }
        finally
        {
            IsDisconnectEnabled = true;
        }
    }

    private void StopAudio()
    {
        _audioService?.Stop();
        _audioService?.Dispose();
        _audioService = null;
        _uiTimer?.Stop();
        _uiTimer?.Dispose();
        _uiTimer = null;
    }

    [RelayCommand]
    private async Task SwitchChannel(Guid channelId)
    {
        if (channelId == _channelResult.ChannelId) return;

        CanCreateChannel = false;
        try
        {
            var result = await _channelService.SwitchToChannelAsync(channelId);
            if (result is not null)
            {
                _channelResult = result;
                StopAudio();
                CurrentChannelName = result.ChannelName;
                _ = EnterRoomAsync();
            }
        }
        catch (Exception ex)
        {
            ClientLog.Info($"Switch channel failed: {ex.Message}");
            await _channelService.RequestServerStateAsync();
        }
        finally
        {
            CanCreateChannel = _channelService.ServerState.CanCreateChannel;
        }
    }

    [RelayCommand]
    private async Task ShowPermissions(PermissionTargetItem item)
    {
        await _host.ShowPermissionsWindowAsync(item.UserId, item.Username);
    }

    [RelayCommand]
    private async Task EditPermissions()
    {
        var node = _contextMenuTargetNode;
        if (node is null) return;

        if (node.Kind == NodeKind.User && node.UserId.HasValue)
        {
            var username = _channelService.GetUsernameForUserId(node.UserId.Value) ?? node.DisplayName;
            await _host.ShowPermissionsWindowAsync(node.UserId.Value, username);
        }
        else if (node.Kind == NodeKind.Channel && node.ChannelId.HasValue)
        {
            var channel = _channelService.ServerState.Channels.FirstOrDefault(c => c.Id == node.ChannelId.Value);
            var channelName = channel?.Name ?? node.DisplayName;
            await _host.ShowChannelPermissionsWindowAsync(node.ChannelId.Value, channelName);
        }
    }

    [RelayCommand]
    private async Task ShowChannelPermissions()
    {
        var node = _contextMenuTargetNode;
        if (node is null || !node.ChannelId.HasValue) return;
        var channel = _channelService.ServerState.Channels.FirstOrDefault(c => c.Id == node.ChannelId.Value);
        var channelName = channel?.Name ?? node.DisplayName;
        await _host.ShowChannelPermissionsWindowAsync(node.ChannelId.Value, channelName);
    }

    [RelayCommand]
    private async Task CreateChannel()
    {
        var name = CreateChannelName.Trim();
        if (string.IsNullOrEmpty(name)) return;

        CreateChannelName = "";
        CanCreateChannel = false;

        try
        {
            var result = await _channelService.CreateChannelAsync(name);
            if (result is not null)
            {
                _channelResult = result;
                StopAudio();
                CurrentChannelName = result.ChannelName;
                _ = EnterRoomAsync();
            }
        }
        catch (Exception ex)
        {
            ClientLog.Info($"Create channel failed: {ex.Message}");
            await _channelService.RequestServerStateAsync();
        }
        finally
        {
            CanCreateChannel = _channelService.ServerState.CanCreateChannel;
        }
    }

    [RelayCommand]
    private async Task Disconnect()
    {
        IsDisconnectEnabled = false;
        try
        {
            StopAudio();
            await _channelService.LeaveRoomAsync();
        }
        catch (Exception ex)
        {
            ClientLog.Info($"Disconnect failed: {ex.Message}");
        }
        _channelService.Stop();
        _host.ShowLogin();
    }

    [RelayCommand]
    private void ShowSettings()
    {
        _host.ShowSettings();
    }

    public void SetTransmitting(bool transmitting)
    {
        _audioService?.SetTransmitting(transmitting);
    }

    public void RestartAudio()
    {
        StopAudio();
        _ = EnterRoomAsync();
    }

    public void MuteAudioForMicTest()
    {
        _audioService?.SetMutedForMicTest(true);
    }

    public void UnmuteAudioForMicTest()
    {
        _audioService?.SetMutedForMicTest(false);
    }

    public ServerTreeNode? RootNode => _rootNode;

    public bool IsAdmin => _auth.IsAdmin;

    /// <summary>
    /// Raised when the tree is refreshed (e.g. after ServerState). View can use to expand nodes.
    /// </summary>
    public event Action? TreeRefreshed;

    private static string? ResolvePlaybackDevice(string? backend, string? device)
    {
        if (!string.IsNullOrEmpty(device)) return device;
        return backend;
    }

    private static string? ResolveCaptureDevice(string? backend, string? device)
    {
        if (!string.IsNullOrEmpty(device)) return device;
        return null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _channelService.ServerStateReceived -= OnServerStateReceived;
        _channelService.RoomJoinedReceived -= OnRoomJoinedReceived;
        _channelService.RoomLeftReceived -= OnRoomLeftReceived;
        _channelService.PingLatencyUpdated -= OnPingLatencyUpdated;
        StopAudio();
        _channelService.Stop();
        _disposed = true;
    }
}

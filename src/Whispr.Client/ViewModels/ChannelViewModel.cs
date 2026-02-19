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
    private Guid? _selectedChannelId;
    private string _selectedChannelName = "";
    private string _selectedChannelType = "voice";
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

    /// <summary>"voice" or "text". Used when creating a channel.</summary>
    [ObservableProperty]
    private string _createChannelType = "voice";

    /// <summary>Choices for create channel type dropdown.</summary>
    public IReadOnlyList<string> CreateChannelTypeChoices { get; } = ["voice", "text"];

    [ObservableProperty]
    private string _pingDisplayText = "—";

    /// <summary>Chat messages for the current text channel. Only populated when IsTextChannel.</summary>
    public ObservableCollection<ChatMessagePayload> Messages { get; } = new();

    /// <summary>Messages with display hints (e.g. show sender header only when sender changes). Bound by the view.</summary>
    public ObservableCollection<MessageDisplayItem> MessageDisplayItems { get; } = new();

    [ObservableProperty]
    private string _messageInputText = "";

    /// <summary>True when a request for older messages is in flight (scroll-up paging).</summary>
    [ObservableProperty]
    private bool _isLoadingOlderMessages;

    /// <summary>True when the next MessageHistoryReceived should prepend (older messages) instead of replace.</summary>
    private bool _pendingHistoryIsOlder;

    /// <summary>Selected channel for the main panel (voice or text).</summary>
    public Guid? SelectedChannelId { get => _selectedChannelId; private set => SetProperty(ref _selectedChannelId, value); }
    /// <summary>Name of the selected channel.</summary>
    public string SelectedChannelName { get => _selectedChannelName; private set => SetProperty(ref _selectedChannelName, value ?? ""); }
    /// <summary>"voice" or "text".</summary>
    public string SelectedChannelType { get => _selectedChannelType; private set => SetProperty(ref _selectedChannelType, value ?? "voice"); }

    /// <summary>True when the selected channel is voice (audio).</summary>
    public bool IsVoiceChannel => string.Equals(_selectedChannelType, "voice", StringComparison.OrdinalIgnoreCase);

    /// <summary>True when the selected channel is text (messages only).</summary>
    public bool IsTextChannel => string.Equals(_selectedChannelType, "text", StringComparison.OrdinalIgnoreCase);

    /// <summary>True when a text channel is selected for viewing (right panel shows messages). When false, show "No text channel" placeholder.</summary>
    public bool HasTextChannelSelected => _selectedChannelId.HasValue;

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
        _channelService.MessageReceived += OnMessageReceived;
        _channelService.MessageHistoryReceived += OnMessageHistoryReceived;

        BuildTree();
        CanCreateChannel = serverState.CanCreateChannel;

        var isVoice = string.Equals(channelResult.ChannelType, "voice", StringComparison.OrdinalIgnoreCase);
        if (isVoice)
        {
            _ = EnterRoomAsync();
            var firstText = serverState.Channels?.FirstOrDefault(c => string.Equals(c.Type, "text", StringComparison.OrdinalIgnoreCase));
            if (firstText is not null)
            {
                SelectedChannelId = firstText.Id;
                SelectedChannelName = firstText.Name ?? "Text";
                SelectedChannelType = "text";
                CurrentChannelName = SelectedChannelName;
                _ = LoadTextChannelMessagesAsync();
            }
            else
            {
                SelectedChannelId = null;
                SelectedChannelName = "No text channel";
                SelectedChannelType = "text";
                CurrentChannelName = SelectedChannelName;
            }
            OnPropertyChanged(nameof(IsVoiceChannel));
            OnPropertyChanged(nameof(IsTextChannel));
            OnPropertyChanged(nameof(HasTextChannelSelected));
            ((IRelayCommand)SendMessageCommand).NotifyCanExecuteChanged();
        }
        else
        {
            SelectedChannelId = channelResult.ChannelId;
            SelectedChannelName = channelResult.ChannelName;
            SelectedChannelType = channelResult.ChannelType ?? "voice";
            CurrentChannelName = SelectedChannelName;
            _ = LoadTextChannelMessagesAsync();
        }
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
        BuildTree();
        CanCreateChannel = state.CanCreateChannel;
        TreeRefreshed?.Invoke();
    }

    private void OnRoomJoinedReceived(ChannelJoinedResult result)
    {
        var isVoice = string.Equals(result.ChannelType, "voice", StringComparison.OrdinalIgnoreCase);
        var isText = string.Equals(result.ChannelType, "text", StringComparison.OrdinalIgnoreCase);

        if (isVoice)
        {
            _channelResult = result;
            StopAudio();
            _ = EnterRoomAsync();
        }
        else if (isText)
        {
            SelectedChannelId = result.ChannelId;
            SelectedChannelName = result.ChannelName;
            SelectedChannelType = "text";
            CurrentChannelName = result.ChannelName;
            _ = LoadTextChannelMessagesAsync();
            OnPropertyChanged(nameof(IsVoiceChannel));
            OnPropertyChanged(nameof(IsTextChannel));
            OnPropertyChanged(nameof(HasTextChannelSelected));
            ((IRelayCommand)SendMessageCommand).NotifyCanExecuteChanged();
        }

        RefreshTree();
        _ = _channelService.RequestServerStateAsync();
    }

    private void OnMessageReceived(ChatMessagePayload payload)
    {
        if (!_selectedChannelId.HasValue || payload.ChannelId != _selectedChannelId.Value) return;
        Messages.Add(payload);
        RefreshMessageDisplayItems();
    }

    private void OnMessageHistoryReceived(MessageHistoryPayload payload)
    {
        if (!_selectedChannelId.HasValue || payload.ChannelId != _selectedChannelId.Value) return;
        IsLoadingOlderMessages = false;
        if (_pendingHistoryIsOlder)
        {
            _pendingHistoryIsOlder = false;
            foreach (var m in payload.Messages.Reverse())
                Messages.Insert(0, m);
        }
        else
        {
            Messages.Clear();
            foreach (var m in payload.Messages)
                Messages.Add(m);
        }
        RefreshMessageDisplayItems();
    }

    private void RefreshMessageDisplayItems()
    {
        MessageDisplayItems.Clear();
        Guid? prevSenderId = null;
        foreach (var m in Messages)
        {
            var showHeader = prevSenderId is null || prevSenderId.Value != m.SenderId;
            MessageDisplayItems.Add(new MessageDisplayItem(m, showHeader));
            prevSenderId = m.SenderId;
        }
    }

    private async Task LoadTextChannelMessagesAsync()
    {
        if (!_selectedChannelId.HasValue) return;
        Messages.Clear();
        MessageDisplayItems.Clear();
        _pendingHistoryIsOlder = false;
        try
        {
            await _channelService.RequestMessageHistoryAsync(_selectedChannelId.Value, since: null, before: null, limit: 100);
        }
        catch (Exception ex)
        {
            ClientLog.Info($"Load message history failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Requests older messages (for scroll-up). Call when user scrolls near the top.
    /// </summary>
    public async Task LoadOlderMessagesAsync()
    {
        if (!IsTextChannel || !_selectedChannelId.HasValue || Messages.Count == 0 || IsLoadingOlderMessages) return;
        var oldest = Messages[0].CreatedAt;
        IsLoadingOlderMessages = true;
        _pendingHistoryIsOlder = true;
        try
        {
            await _channelService.RequestMessageHistoryAsync(_selectedChannelId.Value, before: oldest, limit: 50);
        }
        catch (Exception ex)
        {
            ClientLog.Info($"Load older messages failed: {ex.Message}");
            IsLoadingOlderMessages = false;
            _pendingHistoryIsOlder = false;
        }
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
                IsCurrentChannel = ch.Id == _selectedChannelId,
                ChannelType = ch.Type ?? "voice"
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

            channelNode.IsCurrentChannel = _selectedChannelId == ch.Id;

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
        if (_channelResult.AudioKey is null)
            return;

        IsDisconnectEnabled = false;

        try
        {
            var clientId = await _channelService.RegisterUdpAsync();

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

    /// <summary>
    /// Selects a channel for the main panel. Voice: joins the channel (leave current, join new). Text: only switches view, no join.
    /// </summary>
    public async Task SelectChannelAsync(Guid channelId, string channelName, string channelType)
    {
        var isVoice = string.Equals(channelType, "voice", StringComparison.OrdinalIgnoreCase);
        var isText = string.Equals(channelType, "text", StringComparison.OrdinalIgnoreCase);

        if (isText)
        {
            if (channelId == _selectedChannelId) return;
            SelectedChannelId = channelId;
            SelectedChannelName = channelName;
            SelectedChannelType = "text";
            CurrentChannelName = channelName;
            RefreshTree();
            OnPropertyChanged(nameof(IsVoiceChannel));
            OnPropertyChanged(nameof(IsTextChannel));
            OnPropertyChanged(nameof(HasTextChannelSelected));
            ((IRelayCommand)SendMessageCommand).NotifyCanExecuteChanged();
            _ = LoadTextChannelMessagesAsync();
            return;
        }

        if (isVoice)
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
                    RefreshTree();
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
            var result = await _channelService.CreateChannelAsync(name, CreateChannelType);
            if (result is not null)
            {
                var isVoice = string.Equals(result.ChannelType, "voice", StringComparison.OrdinalIgnoreCase);
                var isText = string.Equals(result.ChannelType, "text", StringComparison.OrdinalIgnoreCase);
                RefreshTree();
                if (isVoice)
                {
                    _channelResult = result;
                    StopAudio();
                    _ = EnterRoomAsync();
                }
                else if (isText)
                {
                    SelectedChannelId = result.ChannelId;
                    SelectedChannelName = result.ChannelName;
                    SelectedChannelType = "text";
                    CurrentChannelName = result.ChannelName;
                    OnPropertyChanged(nameof(IsVoiceChannel));
                    OnPropertyChanged(nameof(IsTextChannel));
                    OnPropertyChanged(nameof(HasTextChannelSelected));
                    ((IRelayCommand)SendMessageCommand).NotifyCanExecuteChanged();
                    _ = LoadTextChannelMessagesAsync();
                }
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

    [RelayCommand(CanExecute = nameof(CanSendMessage))]
    private async Task SendMessage()
    {
        var content = MessageInputText.Trim();
        if (string.IsNullOrEmpty(content) || !IsTextChannel || !_selectedChannelId.HasValue) return;

        var channelId = _selectedChannelId.Value;
        MessageInputText = "";
        try
        {
            await _channelService.SendMessageAsync(channelId, content);
        }
        catch (Exception ex)
        {
            ClientLog.Info($"Send message failed: {ex.Message}");
        }
    }

    private bool CanSendMessage() => HasTextChannelSelected;

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
        _channelService.MessageReceived -= OnMessageReceived;
        _channelService.MessageHistoryReceived -= OnMessageHistoryReceived;
        StopAudio();
        _channelService.Stop();
        _disposed = true;
    }
}

using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Whispr.Client.Models;
using Whispr.Client.Services;
using Whispr.Client.ViewModels;
using Whispr.Core.Protocol;

namespace Whispr.Client.Views;

public partial class ChannelView : UserControl, IDisposable, IChannelViewHost
{
    private readonly MainWindow _window;
    private readonly ChannelService _channelService;
    private readonly ChannelViewModel _viewModel;
    private bool _pttMouseCaptureActive;

    public ChannelView(MainWindow window, ConnectionService connection, AuthService auth,
        ChannelJoinedResult channelResult, ServerStatePayload serverState, string serverHost)
    {
        _window = window;
        var myUserId = auth.User?.Id ?? Guid.Empty;
        _channelService = new ChannelService(connection, auth, myUserId, a => Avalonia.Threading.Dispatcher.UIThread.Post(a));
        _channelService.Start(channelResult, serverState);
        _viewModel = new ChannelViewModel(_channelService, auth, this, channelResult, serverState, serverHost);
        _viewModel.TreeRefreshed += ExpandAllNodesWhenReady;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        DataContext = _viewModel;
        InitializeComponent();

        TalkButton.AddHandler(InputElement.PointerPressedEvent, OnTalkButtonPressed, handledEventsToo: true);
        TalkButton.AddHandler(InputElement.PointerReleasedEvent, OnTalkButtonReleased, handledEventsToo: true);
        TalkButton.AddHandler(InputElement.PointerCaptureLostEvent, OnTalkButtonCaptureLost, handledEventsToo: true);
        MessageInputBox.AddHandler(InputElement.KeyDownEvent, OnMessageInputKeyDown, handledEventsToo: false);
        // Tunnel runs before the TextBox processes the key; we handle Space when empty so the TextBox never inserts a tab-like character.
        MessageInputBox.AddHandler(InputElement.KeyDownEvent, OnMessageInputKeyDownTunnel, RoutingStrategies.Tunnel);
        MessageScroll.ScrollChanged += OnMessageScrollChanged;
        _viewModel.MessageDisplayItems.CollectionChanged += OnMessageDisplayItemsChanged;
        AttachedToVisualTree += OnAttachedToVisualTree;
    }

    private void OnAttachedToVisualTree(object? sender, EventArgs e)
    {
        if (_viewModel.IsTalkButtonVisible)
            AttachPttKeyHandlers();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ChannelViewModel.IsTalkButtonVisible)) return;
        if (_viewModel.IsTalkButtonVisible)
            AttachPttKeyHandlers();
        else
            RemovePttKeyHandlers();
    }

    private void AttachPttKeyHandlers()
    {
        RemovePttKeyHandlers();
        _window.AddHandler(InputElement.KeyDownEvent, OnPttKeyDown, handledEventsToo: true);
        _window.AddHandler(InputElement.KeyUpEvent, OnPttKeyUp, handledEventsToo: true);
        _window.AddHandler(InputElement.PointerPressedEvent, OnPttPointerPressed, handledEventsToo: true);
        _window.AddHandler(InputElement.PointerReleasedEvent, OnPttPointerReleased, handledEventsToo: true);
        _window.AddHandler(InputElement.PointerCaptureLostEvent, OnPttPointerCaptureLost, handledEventsToo: true);
    }

    private void RemovePttKeyHandlers()
    {
        _window.RemoveHandler(InputElement.KeyDownEvent, OnPttKeyDown);
        _window.RemoveHandler(InputElement.KeyUpEvent, OnPttKeyUp);
        _window.RemoveHandler(InputElement.PointerPressedEvent, OnPttPointerPressed);
        _window.RemoveHandler(InputElement.PointerReleasedEvent, OnPttPointerReleased);
        _window.RemoveHandler(InputElement.PointerCaptureLostEvent, OnPttPointerCaptureLost);
    }

    private static (Key? Key, string? MouseButton) ParsePttKeyOrButton(string? value)
    {
        if (string.IsNullOrEmpty(value)) return (null, null);
        if (value.StartsWith("Key:", StringComparison.OrdinalIgnoreCase))
        {
            var keyPart = value.Length > 4 ? value[4..] : "";
            return (Enum.TryParse<Key>(keyPart, ignoreCase: true, out var k) ? k : null, null);
        }
        if (value.StartsWith("Mouse:", StringComparison.OrdinalIgnoreCase))
        {
            var btn = value.Length > 6 ? value[6..] : "";
            return (null, btn.Length > 0 ? btn : null);
        }
        return (null, null);
    }

    private void OnPttKeyDown(object? sender, KeyEventArgs e)
    {
        var (pttKey, _) = ParsePttKeyOrButton(_viewModel.PttKeyOrButton);
        if (pttKey is null || e.Key != pttKey) return;
        _viewModel.SetTransmitting(true);
        e.Handled = true;
    }

    private void OnPttKeyUp(object? sender, KeyEventArgs e)
    {
        var (pttKey, _) = ParsePttKeyOrButton(_viewModel.PttKeyOrButton);
        if (pttKey is null || e.Key != pttKey) return;
        _viewModel.SetTransmitting(false);
        e.Handled = true;
    }

    private void OnPttPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var (_, pttMouse) = ParsePttKeyOrButton(_viewModel.PttKeyOrButton);
        if (string.IsNullOrEmpty(pttMouse)) return;
        var props = e.GetCurrentPoint(this).Properties;
        bool match = pttMouse.Equals("Left", StringComparison.OrdinalIgnoreCase) && props.IsLeftButtonPressed
            || pttMouse.Equals("Right", StringComparison.OrdinalIgnoreCase) && props.IsRightButtonPressed
            || pttMouse.Equals("Middle", StringComparison.OrdinalIgnoreCase) && props.IsMiddleButtonPressed
            || pttMouse.Equals("XButton1", StringComparison.OrdinalIgnoreCase) && props.IsXButton1Pressed
            || pttMouse.Equals("XButton2", StringComparison.OrdinalIgnoreCase) && props.IsXButton2Pressed;
        if (!match) return;
        _pttMouseCaptureActive = true;
        e.Pointer.Capture(_window);
        _viewModel.SetTransmitting(true);
        e.Handled = true;
    }

    private void OnPttPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_pttMouseCaptureActive) return;
        _pttMouseCaptureActive = false;
        e.Pointer.Capture(null);
        _viewModel.SetTransmitting(false);
        e.Handled = true;
    }

    private void OnPttPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (!_pttMouseCaptureActive) return;
        _pttMouseCaptureActive = false;
        _viewModel.SetTransmitting(false);
    }

    private void OnMessageDisplayItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems?.Count > 0 || e.Action == NotifyCollectionChangedAction.Reset)
            Avalonia.Threading.Dispatcher.UIThread.Post(ScrollMessageToBottomIfRequested, Avalonia.Threading.DispatcherPriority.Loaded);
    }

    private void ScrollMessageToBottomIfRequested()
    {
        if (!_viewModel.RequestScrollToBottom) return;
        _viewModel.RequestScrollToBottom = false;
        MessageScroll.ScrollToEnd();
    }

    public void RestartAudioWithNewSettings() => _viewModel.RestartAudio();
    public void MuteAudioForMicTest() => _viewModel.MuteAudioForMicTest();
    public void UnmuteAudioForMicTest() => _viewModel.UnmuteAudioForMicTest();

    void IChannelViewHost.ShowSettings() => _window.ShowSettings();
    void IChannelViewHost.ShowLogin() => _window.ShowLogin();

    async Task IChannelViewHost.ShowPermissionsWindowAsync(Guid userId, string username)
    {
        var owner = this.FindAncestorOfType<Window>();
        var dialog = new PermissionsWindow(owner, userId, username, _channelService);
        await dialog.ShowDialog(owner ?? throw new InvalidOperationException("No owner window"));
    }

    async Task IChannelViewHost.ShowChannelPermissionsWindowAsync(Guid channelId, string channelName)
    {
        var owner = this.FindAncestorOfType<Window>();
        var dialog = new ChannelPermissionsWindow(owner, channelId, channelName, _channelService);
        await dialog.ShowDialog(owner ?? throw new InvalidOperationException("No owner window"));
    }

    void IChannelViewHost.RestartAudioWithNewSettings() => _viewModel.RestartAudio();

    private void OnTalkButtonPressed(object? sender, PointerPressedEventArgs e)
    {
        e.Pointer.Capture(TalkButton);
        _viewModel.SetTransmitting(true);
    }

    private void OnTalkButtonReleased(object? sender, PointerReleasedEventArgs e)
    {
        e.Pointer.Capture(null);
        _viewModel.SetTransmitting(false);
    }

    private void OnTalkButtonCaptureLost(object? sender, PointerCaptureLostEventArgs e) => _viewModel.SetTransmitting(false);

    private void OnResizeWest(object? sender, PointerPressedEventArgs e) =>
        this.FindAncestorOfType<Window>()?.BeginResizeDrag(WindowEdge.West, e);

    private void OnResizeEast(object? sender, PointerPressedEventArgs e) =>
        this.FindAncestorOfType<Window>()?.BeginResizeDrag(WindowEdge.East, e);

    private void OnResizeSouth(object? sender, PointerPressedEventArgs e) =>
        this.FindAncestorOfType<Window>()?.BeginResizeDrag(WindowEdge.South, e);

    private void OnChannelSelected(object? sender, SelectionChangedEventArgs e)
    {
        var added = e.AddedItems?.Count > 0 ? e.AddedItems[0] : null;
        if (added is not ServerTreeNode node || node.Kind != NodeKind.Channel || !node.ChannelId.HasValue)
            return;
        _ = _viewModel.SelectChannelAsync(node.ChannelId.Value, node.DisplayName, node.ChannelType ?? "voice");
    }

    private void OnContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (!e.TryGetPosition(ChannelTree, out var point))
            return;

        var hit = ChannelTree.InputHitTest(point);
        var node = hit is not null ? ViewHelpers.FindNodeAtVisual(hit) : null;
        if (node is null && ChannelTree.SelectedItem is ServerTreeNode selected)
            node = selected;

        if (node is null)
        {
            e.Handled = true;
            return;
        }

        // Show menu for any valid node; menu items control their own visibility
        _viewModel.ContextMenuTargetNode = node;
    }

    private void OnMessageContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (sender is not Control control || control.DataContext is not MessageDisplayItem item)
            return;
        _viewModel.ContextMessageItem = item;
    }

    private void OnCreateChannelContextClick(object? sender, RoutedEventArgs e)
    {
        FlyoutBase.ShowAttachedFlyout(ChannelTreeHost);
    }

    private void OnCreateChannelFlyoutClick(object? sender, RoutedEventArgs e)
    {
        _viewModel.CreateChannelCommand.Execute(null);
        FlyoutBase.GetAttachedFlyout(ChannelTreeHost)?.Hide();
    }

    private void OnChannelTreeLoaded(object? sender, RoutedEventArgs e)
    {
        ExpandAllNodesWhenReady();
    }

    private void OnMessageScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (sender is not ScrollViewer scroll || !_viewModel.IsTextChannel || _viewModel.IsLoadingOlderMessages)
            return;
        // Load older messages when user scrolls near the top (within 80px)
        if (scroll.Offset.Y < 80)
            _ = _viewModel.LoadOlderMessagesAsync();
    }

    private void OnMessageInputKeyDownTunnel(object? sender, KeyEventArgs e)
    {
        // Space is sometimes turned into a tab or other character by the control. Always insert a normal space (U+0020)
        // ourselves so copy/paste and display are consistent.
        // When the character before the caret is an emoji, the layout engine draws the following space with the emoji font (wide).
        // Inserting a zero-width space (U+200B) before the space forces a new run so the space uses the primary font.
        if (e.Key != Key.Space) return;
        e.Handled = true;
        var text = MessageInputBox.Text ?? "";
        var caret = Math.Clamp(MessageInputBox.CaretIndex, 0, text.Length);
        var insert = IsEmojiCodePoint(GetLastCodePoint(text, caret)) ? "\u200B\u0020" : "\u0020";
        MessageInputBox.Text = text.Insert(caret, insert);
        MessageInputBox.CaretIndex = caret + insert.Length;
    }

    private static int GetLastCodePoint(string text, int caret)
    {
        if (string.IsNullOrEmpty(text) || caret <= 0 || caret > text.Length) return -1;
        var i = caret - 1;
        if (char.IsLowSurrogate(text[i]) && i > 0 && char.IsHighSurrogate(text[i - 1]))
            return char.ConvertToUtf32(text[i - 1], text[i]);
        return text[i];
    }

    private static bool IsEmojiCodePoint(int codePoint)
    {
        if (codePoint < 0) return false;
        return codePoint is >= 0x2600 and <= 0x26FF or >= 0x2700 and <= 0x27BF
            or >= 0x1F000 and <= 0x1F02F or >= 0x1F300 and <= 0x1F9FF
            or >= 0x1FA00 and <= 0x1FA6F;
    }

    private void OnMessageInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;
        // Shift+Enter = new line; Enter alone = send
        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            return;
        e.Handled = true;
        if (_viewModel.SendMessageCommand.CanExecute(null))
            _viewModel.SendMessageCommand.Execute(null);
    }

    public void OnEmojiPickerButtonClick(object? sender, RoutedEventArgs e)
    {
        FlyoutBase.ShowAttachedFlyout(EmojiPickerButton);
    }

    public void OnEmojiChosen(object? sender, RoutedEventArgs e)
    {
        var emoji = (sender as Button)?.Content as string;
        if (string.IsNullOrEmpty(emoji)) return;
        var text = MessageInputBox.Text ?? "";
        var caret = Math.Clamp(MessageInputBox.CaretIndex, 0, text.Length);
        MessageInputBox.Text = text.Insert(caret, emoji);
        MessageInputBox.CaretIndex = caret + emoji.Length;
        FlyoutBase.GetAttachedFlyout(EmojiPickerButton)?.Hide();
    }

    private void OnMessageLinkClick(object? sender, RoutedEventArgs e)
    {
        if (e.Source is not Button button || button.DataContext is not MessageContentSegment segment)
            return;
        if (!segment.IsLink && !segment.IsGifEmbed && !segment.IsYouTubeEmbed)
            return;
        var url = segment.Content;
        if (string.IsNullOrWhiteSpace(url) || (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
            return;
        try
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ClientLog.Info($"Failed to open link: {ex.Message}");
        }
    }

    private void ExpandAllNodesWhenReady()
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            var root = _viewModel.RootNode;
            if (root is null) return;
            var rootContainer = ChannelTree.TreeContainerFromItem(root);
            if (rootContainer is TreeViewItem rootItem)
                ChannelTree.ExpandSubTree(rootItem);
            var selectedId = _viewModel.SelectedChannelId;
            if (selectedId.HasValue && root?.Children is { } children)
            {
                var channelNode = children.FirstOrDefault(n => n.ChannelId == selectedId.Value);
                if (channelNode is not null)
                    ChannelTree.SelectedItem = channelNode;
            }
        }, Avalonia.Threading.DispatcherPriority.Loaded);
    }

    public void Dispose()
    {
        AttachedToVisualTree -= OnAttachedToVisualTree;
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        RemovePttKeyHandlers();
        MessageScroll.ScrollChanged -= OnMessageScrollChanged;
        _viewModel.MessageDisplayItems.CollectionChanged -= OnMessageDisplayItemsChanged;
        _viewModel.TreeRefreshed -= ExpandAllNodesWhenReady;
        _viewModel.Dispose();
    }
}

using System.Collections.Specialized;
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

    public ChannelView(MainWindow window, ConnectionService connection, AuthService auth,
        ChannelJoinedResult channelResult, ServerStatePayload serverState, string serverHost)
    {
        _window = window;
        var myUserId = auth.User?.Id ?? Guid.Empty;
        _channelService = new ChannelService(connection, auth, myUserId, a => Avalonia.Threading.Dispatcher.UIThread.Post(a));
        _channelService.Start(channelResult, serverState);
        _viewModel = new ChannelViewModel(_channelService, auth, this, channelResult, serverState, serverHost);
        _viewModel.TreeRefreshed += ExpandAllNodesWhenReady;
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
        MessageScroll.ScrollChanged -= OnMessageScrollChanged;
        _viewModel.MessageDisplayItems.CollectionChanged -= OnMessageDisplayItemsChanged;
        _viewModel.TreeRefreshed -= ExpandAllNodesWhenReady;
        _viewModel.Dispose();
    }
}

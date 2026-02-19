using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Whispr.Client.Models;
using Whispr.Client.Services;
using Whispr.Client.ViewModels;
using Whispr.Core.Models;
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
        await (dialog.ShowDialog(owner ?? throw new InvalidOperationException("No owner window")));
    }

    async Task IChannelViewHost.ShowChannelPermissionsWindowAsync(Guid channelId, string channelName)
    {
        var owner = this.FindAncestorOfType<Window>();
        var dialog = new ChannelPermissionsWindow(owner, channelId, channelName, _channelService);
        await (dialog.ShowDialog(owner ?? throw new InvalidOperationException("No owner window")));
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
        _viewModel.SwitchChannelCommand.Execute(node.ChannelId.Value);
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

    private void ExpandAllNodesWhenReady()
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            var root = _viewModel.RootNode;
            if (root is null) return;
            var rootContainer = ChannelTree.TreeContainerFromItem(root);
            if (rootContainer is TreeViewItem rootItem)
                ChannelTree.ExpandSubTree(rootItem);
        }, Avalonia.Threading.DispatcherPriority.Loaded);
    }

    public void Dispose()
    {
        _viewModel.TreeRefreshed -= ExpandAllNodesWhenReady;
        _viewModel.Dispose();
    }
}

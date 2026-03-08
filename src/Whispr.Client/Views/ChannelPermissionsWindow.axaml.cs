using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Whispr.Client.Services;
using Whispr.Client.ViewModels;

namespace Whispr.Client.Views;

public partial class ChannelPermissionsWindow : Window
{
    public ChannelPermissionsWindow()
    {
        InitializeComponent();
    }

    public ChannelPermissionsWindow(Window? owner, Guid channelId, string channelName, ChannelService channelService)
        : this()
    {
        DataContext = new ChannelPermissionsViewModel(channelService, channelId, channelName);
        if (owner is not null)
            Owner = owner;
        AddHandler(KeyDownEvent, OnKeyDown, handledEventsToo: true);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close();
        }
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}

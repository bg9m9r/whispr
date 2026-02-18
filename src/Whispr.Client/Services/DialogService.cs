using Avalonia;
using Avalonia.Controls;

namespace Whispr.Client.Services;

/// <summary>
/// Static helpers for showing modal dialogs.
/// </summary>
public static class DialogService
{
    private static Window? Owner => Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
        ? desktop.MainWindow
        : null;

    private static object? FindResource(string key) =>
        Avalonia.Application.Current?.FindResource(key);

    public static async Task<bool> ShowYesNoAsync(string title, string message, string yesText = "Continue", string noText = "Cancel", bool isWarning = false)
    {
        var tcs = new TaskCompletionSource<bool>();
        var dialog = new Window
        {
            Title = title,
            SizeToContent = SizeToContent.WidthAndHeight,
            MinWidth = 320,
            MaxWidth = 450,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = (Avalonia.Media.IBrush?)FindResource("WhisprSurface") ?? Avalonia.Media.Brushes.Gray
        };
        var messageBlock = new TextBlock
        {
            Text = message,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            Foreground = isWarning
                ? (Avalonia.Media.IBrush?)FindResource("WhisprDanger") ?? Avalonia.Media.Brushes.OrangeRed
                : (Avalonia.Media.IBrush?)FindResource("WhisprTextSecondary") ?? Avalonia.Media.Brushes.White
        };
        var yesBtn = new Button { Content = yesText, Width = yesText.Length > 10 ? 120 : 100 };
        var noBtn = new Button { Content = noText, Width = 100 };
        var buttons = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 12 };
        buttons.Children.Add(yesBtn);
        buttons.Children.Add(noBtn);
        var panel = new StackPanel { Spacing = 16, Margin = new Avalonia.Thickness(24), Children = { messageBlock, buttons } };
        yesBtn.Click += (_, _) => { tcs.TrySetResult(true); dialog.Close(); };
        noBtn.Click += (_, _) => { tcs.TrySetResult(false); dialog.Close(); };
        dialog.Content = panel;
        dialog.Closed += (_, _) => tcs.TrySetResult(false);
        await dialog.ShowDialog(Owner!);
        return await tcs.Task;
    }

    public static async Task ShowOkAsync(string title, string message)
    {
        var dialog = new Window
        {
            Title = title,
            SizeToContent = SizeToContent.WidthAndHeight,
            MinWidth = 280,
            MaxWidth = 400,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = (Avalonia.Media.IBrush?)FindResource("WhisprSurface") ?? Avalonia.Media.Brushes.Gray
        };
        var ok = new Button { Content = "OK", Width = 80 };
        var panel = new StackPanel
        {
            Spacing = 16,
            Margin = new Avalonia.Thickness(24),
            Children =
            {
                new TextBlock
                {
                    Text = message,
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                    Foreground = (Avalonia.Media.IBrush?)FindResource("WhisprDanger") ?? Avalonia.Media.Brushes.OrangeRed
                },
                ok
            }
        };
        ok.Click += (_, _) => dialog.Close();
        dialog.Content = panel;
        await dialog.ShowDialog(Owner!);
    }
}

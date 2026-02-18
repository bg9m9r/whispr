using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Whispr.Client.ViewModels;

/// <summary>
/// ViewModel for MainWindow. Holds commands for window-level actions.
/// Navigation (ShowSettings, ShowLogin, ShowChannelView) is driven by child views.
/// </summary>
public sealed partial class MainWindowViewModel : ObservableObject
{
    [RelayCommand]
    private void Close()
    {
        // Invoked via binding; the view (MainWindow) must handle actual Close.
        // We use a callback since ViewModel cannot close the window directly.
        CloseRequested?.Invoke();
    }

    /// <summary>
    /// Raised when CloseCommand executes. MainWindow subscribes and calls Window.Close().
    /// </summary>
    public event Action? CloseRequested;
}

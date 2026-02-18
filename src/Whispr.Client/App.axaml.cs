using Avalonia;

namespace Whispr.Client;

public partial class App : Application
{
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new Views.MainWindow();

        base.OnFrameworkInitializationCompleted();
    }
}
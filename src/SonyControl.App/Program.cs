using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;

namespace SonyControl.App;

/// <summary>
/// Entry point. Replaces the XAML-generated Main so only one copy of the tray app runs.
/// </summary>
public static class Program
{
    private const string InstanceKey = "SonyControl";

    [STAThread]
    private static void Main()
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();

        // A second launch (startup task plus a manual start) exits right away.
        var instance = AppInstance.FindOrRegisterForKey(InstanceKey);
        if (!instance.IsCurrent)
        {
            return;
        }

        Application.Start(callbackParams =>
        {
            var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            _ = new App();
        });
    }
}

using System.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using SonyControl.Presentation.Notifications;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace SonyControl.App;

/// <summary>
/// <see cref="INotificationService"/> for both installs.
/// </summary>
/// <remarks>
/// The MSIX uses Windows App SDK app notifications. The classic (MSI) install uses Windows' own
/// toast notifications, tied to the Start menu shortcut the installer creates with
/// <see cref="AppIdentity.ClassicAppUserModelId"/>; Windows App SDK 2.0's notifications don't
/// load outside a package (a resource DLL they need isn't shipped).
/// https://learn.microsoft.com/windows/apps/design/shell/tiles-and-notifications/send-local-toast-other-apps
/// </remarks>
internal sealed class AppNotificationService : INotificationService, IDisposable
{
    private readonly ILogger _logger;
    private bool _registered;
    private ToastNotifier? _classicNotifier;

    public AppNotificationService(ILogger logger)
    {
        _logger = logger;
    }

    public void Register()
    {
        try
        {
            if (!AppIdentity.IsPackaged)
            {
                _classicNotifier = ToastNotificationManager.CreateToastNotifier(AppIdentity.ClassicAppUserModelId);
                return;
            }

            // Handle clicks in this process so a click doesn't start a second copy.
            AppNotificationManager.Default.NotificationInvoked += (_, _) => { };
            AppNotificationManager.Default.Register();
            _registered = true;
        }
        catch (Exception ex)
        {
            AppLog.NotificationsUnavailable(_logger, ex);
        }
    }

    public void ShowLowBattery(string deviceName, int level)
    {
        var title = $"{deviceName} battery is low";
        var message = $"{level}% left. Charge them soon.";

        if (_classicNotifier is not null)
        {
            ShowClassic(title, message);
            return;
        }
        if (!_registered)
        {
            return;
        }

        var notification = new AppNotificationBuilder()
            .AddText(title)
            .AddText(message)
            .BuildNotification();
        AppNotificationManager.Default.Show(notification);
    }

    public void Dispose()
    {
        if (_registered)
        {
            AppNotificationManager.Default.Unregister();
            _registered = false;
        }
    }

    private void ShowClassic(string title, string message)
    {
        try
        {
            var xml = new XmlDocument();
            xml.LoadXml($"<toast><visual><binding template=\"ToastGeneric\"><text>{SecurityElement.Escape(title)}</text><text>{SecurityElement.Escape(message)}</text></binding></visual></toast>");
            _classicNotifier!.Show(new ToastNotification(xml));
        }
        catch (Exception ex)
        {
            AppLog.NotificationsUnavailable(_logger, ex);
        }
    }
}

using IAlarm.Views;

namespace IAlarm;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute(nameof(AlarmEditorPage), typeof(AlarmEditorPage));
        Loaded += (_, _) => App.OpenPendingTab();
    }
}

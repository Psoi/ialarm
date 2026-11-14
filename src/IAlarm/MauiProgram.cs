using IAlarm.Services;
using IAlarm.ViewModels;
using IAlarm.Views;
using Microsoft.Extensions.Logging;

namespace IAlarm;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        builder.Services.AddSingleton(_ => AlarmRepository.Get(Platform.AppContext));

        // The two tabs live for the whole app; the editor is new each time.
        builder.Services.AddSingleton<AlarmsViewModel>();
        builder.Services.AddSingleton<AlarmsPage>();
        builder.Services.AddSingleton<StopwatchViewModel>();
        builder.Services.AddSingleton<StopwatchPage>();
        builder.Services.AddTransient<AlarmEditorViewModel>();
        builder.Services.AddTransient<AlarmEditorPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}

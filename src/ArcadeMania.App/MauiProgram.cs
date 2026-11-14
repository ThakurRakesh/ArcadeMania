using Microsoft.Extensions.Logging;
using Microsoft.Maui.LifecycleEvents;

namespace ArcadeMania.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("DMSans.ttf", "DMSans");
                fonts.AddFont("CormorantGaramond.ttf", "Cormorant");
            })
            .ConfigureLifecycleEvents(events =>
            {
#if WINDOWS
                events.AddWindows(w => w.OnWindowCreated(window => WinPlatform.WinWindow.Attach(window)));
#endif
#if ANDROID
                events.AddAndroid(a => a.OnCreate((activity, _) =>
                {
                    // Edge-to-edge immersive game view on phones/tablets/Android TV.
                    try { activity.Window?.AddFlags(Android.Views.WindowManagerFlags.KeepScreenOn); } catch { }
                }));
#endif
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif
        return builder.Build();
    }
}

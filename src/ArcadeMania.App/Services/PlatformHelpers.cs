namespace ArcadeMania.App.Services;

/// <summary>Hand (pointer) cursor on anything clickable — Windows and Mac pointer devices.</summary>
public static class PointerHover
{
    public static void Attach(View view)
    {
#if WINDOWS
        view.HandlerChanged += (_, _) =>
        {
            if (view.Handler?.PlatformView is Microsoft.UI.Xaml.UIElement el) WinPlatform.WinCursor.SetHand(el);
        };
#endif
    }
}

/// <summary>Borderless full-screen toggle (Windows). On phones the app is already full-screen.</summary>
public static class FullScreen
{
    public static void Toggle()
    {
#if WINDOWS
        WinPlatform.WinWindow.ToggleFullScreen();
#endif
    }
}

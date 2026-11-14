namespace ArcadeMania.App.Services;

/// <summary>"Warm Linen" theme: Day = Cool Mist, Night = Warm Ember. Applied through DynamicResource keys so it switches live.</summary>
public static class Theme
{
    const string PrefKey = "theme.night";
    public static bool IsNight { get; private set; } = true;

    public static void Load() { IsNight = Preferences.Default.Get(PrefKey, true); Apply(); }

    public static void Toggle() { IsNight = !IsNight; Preferences.Default.Set(PrefKey, IsNight); Apply(); }

    static void Apply()
    {
        var r = Application.Current!.Resources;
        if (IsNight)
        {
            r["Bg"] = Color.FromArgb("#0F0C09"); r["Surface"] = Color.FromArgb("#191410"); r["Border"] = Color.FromArgb("#2E2419");
            r["Text"] = Color.FromArgb("#EDE4D5"); r["Muted"] = Color.FromArgb("#8C7B65");
            r["Accent"] = Color.FromArgb("#C49A6C"); r["Accent2"] = Color.FromArgb("#E0B07A"); r["OnAccent"] = Color.FromArgb("#140E08");
        }
        else
        {
            r["Bg"] = Color.FromArgb("#F2F5F7"); r["Surface"] = Color.FromArgb("#F8FAFC"); r["Border"] = Color.FromArgb("#D8E2EA");
            r["Text"] = Color.FromArgb("#3D5061"); r["Muted"] = Color.FromArgb("#7A96AA");
            r["Accent"] = Color.FromArgb("#5B8FA8"); r["Accent2"] = Color.FromArgb("#7BAEC4"); r["OnAccent"] = Color.FromArgb("#F8FAFC");
        }
    }

    public const string Heading = "Cormorant", Body = "DMSans";
}

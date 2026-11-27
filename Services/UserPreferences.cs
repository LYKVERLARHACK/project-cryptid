using Microsoft.Win32;

namespace RealityCheckDesktop.Services;

public enum OverlayTheme
{
    Default,
    Kids,
    Inferno,
    Tech,
    White
}

public static class UserPreferences
{
    private const string RegistryPath = @"Software\RealityCheckDesktop";
    private const string KidsModeValue = "KidsMode";
    private const string ThemeValue = "Theme";

    public static OverlayTheme SelectedTheme
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryPath);
            var storedTheme = key?.GetValue(ThemeValue) as string;
            if (Enum.TryParse(storedTheme, ignoreCase: true, out OverlayTheme theme)
                && Enum.IsDefined(typeof(OverlayTheme), theme))
            {
                return theme;
            }

            return key?.GetValue(KidsModeValue, 0) is int value && value != 0
                ? OverlayTheme.Kids
                : OverlayTheme.Default;
        }
    }

    public static void SaveTheme(OverlayTheme theme)
    {
        if (!Enum.IsDefined(typeof(OverlayTheme), theme))
        {
            throw new ArgumentOutOfRangeException(nameof(theme));
        }

        using var key = Registry.CurrentUser.CreateSubKey(RegistryPath, writable: true)
            ?? throw new InvalidOperationException("Could not save the theme preference.");
        key.SetValue(ThemeValue, theme.ToString(), RegistryValueKind.String);
        key.SetValue(KidsModeValue, theme == OverlayTheme.Kids ? 1 : 0, RegistryValueKind.DWord);
    }
}

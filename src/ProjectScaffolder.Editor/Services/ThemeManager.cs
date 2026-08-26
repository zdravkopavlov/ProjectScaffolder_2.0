using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Win32;

namespace ProjectScaffolder.Editor.Services;

public enum ThemePreference
{
    System,
    Light,
    Dark,
}

internal static partial class ThemeManager
{
    public static bool IsDark { get; private set; }

    public static void Apply(ThemePreference preference)
    {
        IsDark = preference == ThemePreference.Dark ||
            (preference == ThemePreference.System && SystemUsesDarkTheme());
        ResourceDictionary resources = Application.Current.Resources.MergedDictionaries[0];
        resources.MergedDictionaries[0] = new ResourceDictionary
        {
            Source = new Uri(IsDark ? "Themes/Dark.xaml" : "Themes/Light.xaml", UriKind.Relative),
        };
    }

    public static void ApplyWindowChrome(Window window)
    {
        WindowInteropHelper helper = new(window);
        int dark = IsDark ? 1 : 0;
        _ = DwmSetWindowAttribute(helper.Handle, 20, ref dark, sizeof(int));
    }

    private static bool SystemUsesDarkTheme()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
    }

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int valueSize);
}

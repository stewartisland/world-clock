using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Win32;

namespace WorldClock;

public enum AppTheme { Dark, Light }

/// <summary>
/// Swaps the Themes/*.xaml brush dictionary at runtime and keeps each window's title bar in step.
/// Windows call <see cref="Attach"/> from their constructor.
/// </summary>
public static class ThemeManager
{
    private const int DwmwaUseImmersiveDarkMode = 20;
    private static bool _listening;

    /// <summary>The user's choice; null means follow the Windows app mode.</summary>
    public static AppTheme? Preference { get; private set; }

    /// <summary>The theme actually showing.</summary>
    public static AppTheme Current { get; private set; } = AppTheme.Dark;

    /// <summary>The Windows "app mode" setting (Settings → Personalization → Colors).</summary>
    public static AppTheme SystemTheme()
    {
        var value = Registry.GetValue(
            @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 0);
        return value is int light && light == 1 ? AppTheme.Light : AppTheme.Dark;
    }

    /// <summary>Applies a chosen theme, or follows Windows (including live changes) when null.</summary>
    public static void SetPreference(AppTheme? preference)
    {
        Preference = preference;
        Apply(preference ?? SystemTheme());

        if (!_listening)
        {
            _listening = true;
            SystemEvents.UserPreferenceChanged += (_, e) =>
            {
                if (e.Category == UserPreferenceCategory.General && Preference is null)
                    Application.Current?.Dispatcher.BeginInvoke(() => Apply(SystemTheme()));
            };
        }
    }

    public static void Attach(Window window) => window.SourceInitialized += (_, _) => SetTitleBar(window);

    private static void Apply(AppTheme theme)
    {
        Current = theme;
        var dictionaries = Application.Current.Resources.MergedDictionaries;
        dictionaries.Clear();
        dictionaries.Add(new ResourceDictionary { Source = new Uri($"Themes/{theme}.xaml", UriKind.Relative) });

        foreach (Window window in Application.Current.Windows)
            SetTitleBar(window);
    }

    private static void SetTitleBar(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;
        var dark = Current == AppTheme.Dark ? 1 : 0;
        // Supported on Windows 10 20H1 and later; silently does nothing on older versions.
        _ = DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref dark, sizeof(int));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}

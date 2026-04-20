using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace VortexModLists.Services;

public static class ThemeService
{
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaUseImmersiveDarkModeLegacy = 19;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

    public static void ApplyTheme(Application app)
    {
        var isDark = IsDarkModeEnabled();

        var appBackground = isDark ? Brush(31, 31, 31) : Brush(250, 250, 250);
        var appForeground = isDark ? Brush(240, 240, 240) : Brush(26, 26, 26);
        var controlBackground = isDark ? Brush(46, 46, 46) : Brush(255, 255, 255);
        var controlForeground = isDark ? Brush(240, 240, 240) : Brush(26, 26, 26);
        var border = isDark ? Brush(90, 90, 90) : Brush(200, 200, 200);
        var grid = isDark ? Brush(72, 72, 72) : Brush(220, 220, 220);
        var header = isDark ? Brush(56, 56, 56) : Brush(243, 243, 243);
        var selection = isDark ? Brush(62, 90, 132) : Brush(204, 228, 247);
        var inactiveSelection = isDark ? Brush(58, 58, 58) : Brush(225, 225, 225);

        app.Resources["AppBackgroundBrush"] = appBackground;
        app.Resources["AppForegroundBrush"] = appForeground;
        app.Resources["AppControlBackgroundBrush"] = controlBackground;
        app.Resources["AppControlForegroundBrush"] = controlForeground;
        app.Resources["AppControlBorderBrush"] = border;
        app.Resources["AppGridLineBrush"] = grid;
        app.Resources["AppHeaderBackgroundBrush"] = header;
        app.Resources["AppSelectionBrush"] = selection;

        app.Resources[SystemColors.WindowBrushKey] = controlBackground;
        app.Resources[SystemColors.WindowTextBrushKey] = controlForeground;
        app.Resources[SystemColors.ControlBrushKey] = controlBackground;
        app.Resources[SystemColors.ControlLightBrushKey] = controlBackground;
        app.Resources[SystemColors.ControlLightLightBrushKey] = controlBackground;
        app.Resources[SystemColors.ControlDarkBrushKey] = border;
        app.Resources[SystemColors.ControlDarkDarkBrushKey] = border;
        app.Resources[SystemColors.ControlTextBrushKey] = controlForeground;

        app.Resources[SystemColors.HighlightBrushKey] = selection;
        app.Resources[SystemColors.HighlightTextBrushKey] = controlForeground;
        app.Resources[SystemColors.InactiveSelectionHighlightBrushKey] = inactiveSelection;
        app.Resources[SystemColors.InactiveSelectionHighlightTextBrushKey] = controlForeground;
        app.Resources[SystemColors.GrayTextBrushKey] = isDark ? Brush(170, 170, 170) : Brush(109, 109, 109);
    }

    public static void ApplyWindowChromeTheme(Window window)
    {
        if (window is null)
        {
            return;
        }

        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        var useDark = IsDarkModeEnabled() ? 1 : 0;
        _ = DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref useDark, sizeof(int));
        _ = DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkModeLegacy, ref useDark, sizeof(int));
    }

    private static SolidColorBrush Brush(byte r, byte g, byte b)
    {
        return new SolidColorBrush(Color.FromRgb(r, g, b));
    }

    private static bool IsDarkModeEnabled()
    {
        const string key = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
        const string valueName = "AppsUseLightTheme";

        using var regKey = Registry.CurrentUser.OpenSubKey(key);
        var value = regKey?.GetValue(valueName);

        return value is int intValue && intValue == 0;
    }
}

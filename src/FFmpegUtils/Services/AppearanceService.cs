using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace FFmpegUtils.Services;

public enum AppTheme { System, Light, Dark }

public static class AppearanceService
{
    private static string SettingsPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FFmpegUtils", "appearance.json");

    public static AppTheme Load()
    {
        try
        {
            var theme = JsonSerializer.Deserialize<AppTheme>(File.ReadAllText(SettingsPath));
            return Enum.IsDefined(theme) ? theme : AppTheme.System;
        }
        catch { return AppTheme.System; }
    }

    public static void Save(AppTheme theme)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(theme));
    }

    public static void Apply(ResourceDictionary resources, AppTheme theme)
    {
        if (SystemParameters.HighContrast)
        {
            foreach (var key in new[] { "BackgroundBrush", "SurfaceBrush", "SurfaceRaisedBrush", "ControlBrush", "DisabledBrush", "ToolTipBrush" }) resources[key] = SystemColors.WindowBrush;
            foreach (var key in new[] { "TextBrush", "MutedTextBrush", "ErrorBrush", "StrongBorderBrush", "BorderBrush", "ScrollThumbBrush" }) resources[key] = SystemColors.WindowTextBrush;
            foreach (var key in new[] { "AccentBrush", "AccentHoverBrush", "FocusBrush", "SelectionBrush", "ControlPressedBrush", "ScrollThumbHoverBrush" }) resources[key] = SystemColors.HighlightBrush;
            resources["OnAccentBrush"] = SystemColors.HighlightTextBrush;
            resources["NavSelectedTextBrush"] = SystemColors.HighlightTextBrush;
            resources["ControlHoverBrush"] = SystemColors.ControlBrush;
            return;
        }

        var dark = theme == AppTheme.Dark || theme == AppTheme.System && IsSystemDark();
        var colors = new Dictionary<string, string>
        {
            ["BackgroundBrush"] = dark ? "#171B22" : "#F4F6FA",
            ["SurfaceBrush"] = dark ? "#202630" : "#FFFFFF",
            ["SurfaceRaisedBrush"] = dark ? "#2B3340" : "#F0F3F8",
            ["ControlBrush"] = dark ? "#252D39" : "#FFFFFF",
            ["ControlHoverBrush"] = dark ? "#303E53" : "#EDF3FF",
            ["ControlPressedBrush"] = dark ? "#293F61" : "#E5EEFF",
            ["DisabledBrush"] = dark ? "#272D37" : "#EDF0F5",
            ["TextBrush"] = dark ? "#EFF3FA" : "#202B3D",
            ["MutedTextBrush"] = dark ? "#B0BDCF" : "#5E6C80",
            ["BorderBrush"] = dark ? "#3B4657" : "#DEE4ED",
            ["StrongBorderBrush"] = dark ? "#606F85" : "#AAB6C8",
            ["AccentBrush"] = "#2563EB",
            ["AccentHoverBrush"] = "#1D4ED8",
            ["OnAccentBrush"] = "#FFFFFF",
            ["FocusBrush"] = dark ? "#91B8FF" : "#245BC7",
            ["NavSelectedTextBrush"] = dark ? "#91B8FF" : "#245BC7",
            ["SelectionBrush"] = "#2563EB",
            ["ErrorBrush"] = dark ? "#FFAAA4" : "#B42318",
            ["ToolTipBrush"] = dark ? "#293240" : "#FFFFFF",
            ["ScrollThumbBrush"] = dark ? "#66758B" : "#AAB7C9",
            ["ScrollThumbHoverBrush"] = dark ? "#91A4BD" : "#71839D"
        };
        foreach (var (key, value) in colors)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(value));
            brush.Freeze();
            resources[key] = brush;
        }
    }

    private static bool IsSystemDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
        }
        catch { return false; }
    }
}

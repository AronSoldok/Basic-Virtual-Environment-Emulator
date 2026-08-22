using System.IO;
using System.Windows;

namespace ClosedEnv.Services;

public static class ThemeService
{
    public const string Dark = "dark";
    public const string Light = "light";

    public static string Current { get; private set; } = Dark;

    public static string ToggleLabel => Current == Dark ? "Светлая" : "Тёмная";

    public static event Action? Changed;

    public static string FilePath => Path.Combine(AppPaths.Root, "ui-theme.txt");

    public static void Load()
    {
        var theme = Dark;
        try
        {
            if (File.Exists(FilePath))
            {
                var raw = File.ReadAllText(FilePath).Trim();
                if (string.Equals(raw, Light, StringComparison.OrdinalIgnoreCase))
                {
                    theme = Light;
                }
            }
        }
        catch
        {
            // keep default
        }

        Apply(theme, persist: false);
    }

    public static void Toggle() => Apply(Current == Dark ? Light : Dark, persist: true);

    public static void Apply(string theme, bool persist)
    {
        Current = string.Equals(theme, Light, StringComparison.OrdinalIgnoreCase) ? Light : Dark;
        var app = Application.Current;
        if (app is null)
        {
            return;
        }

        var source = Current == Light ? "Themes/Light.xaml" : "Themes/Dark.xaml";
        var dict = new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/" + source, UriKind.Absolute)
        };

        var merged = app.Resources.MergedDictionaries;
        if (merged.Count == 0)
        {
            merged.Add(dict);
        }
        else
        {
            merged[0] = dict;
        }

        if (persist)
        {
            try
            {
                AppPaths.EnsureLayout();
                File.WriteAllText(FilePath, Current);
            }
            catch
            {
                // ignore
            }
        }

        Changed?.Invoke();
    }
}

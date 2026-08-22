using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace ClosedEnv.Services;

public sealed class SandboxStatus
{
    public bool ExePresent { get; init; }
    public bool IsHomeEdition { get; init; }
    public string Edition { get; init; } = "";
    public string Summary { get; init; } = "";
    public bool CanLaunchSandbox => ExePresent && !IsHomeEdition;
}

public static class SandboxFeature
{
    public static readonly string SandboxExe = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Windows),
        "System32",
        "WindowsSandbox.exe");

    public static SandboxStatus Detect()
    {
        var edition = ReadEdition();
        var home = IsHome(edition);
        var present = File.Exists(SandboxExe);

        string summary;
        if (home)
        {
            summary = "Windows Home: песочница недоступна. Для MAX остаётся веб-режим.";
        }
        else if (present)
        {
            summary = "Windows Sandbox доступен.";
        }
        else
        {
            summary = "Windows Sandbox выключен. Включите компоненту (нужны права администратора и перезагрузка).";
        }

        return new SandboxStatus
        {
            ExePresent = present,
            IsHomeEdition = home,
            Edition = edition,
            Summary = summary
        };
    }

    public static void RequestEnable()
    {
        var script = Path.Combine(AppPaths.BundledScripts, "enable-windows-sandbox.ps1");
        if (!File.Exists(script))
        {
            script = Path.Combine(AppPaths.HostScripts, "enable-windows-sandbox.ps1");
        }

        if (!File.Exists(script))
        {
            throw new FileNotFoundException("Не найден scripts/enable-windows-sandbox.ps1");
        }

        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = "-NoProfile -ExecutionPolicy Bypass -File \"" + script + "\"",
            UseShellExecute = true,
            Verb = "runas"
        };
        Process.Start(psi);
    }

    private static string ReadEdition()
    {
        using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
        return key?.GetValue("EditionID") as string
            ?? key?.GetValue("ProductName") as string
            ?? "";
    }

    private static bool IsHome(string edition) =>
        edition.Contains("Home", StringComparison.OrdinalIgnoreCase) ||
        edition.Equals("Core", StringComparison.OrdinalIgnoreCase) ||
        edition.Equals("CoreSingleLanguage", StringComparison.OrdinalIgnoreCase) ||
        edition.Equals("CoreCountrySpecific", StringComparison.OrdinalIgnoreCase);
}

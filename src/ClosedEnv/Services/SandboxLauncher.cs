using System.Diagnostics;
using System.IO;
using System.Text.Json;
using ClosedEnv.Models;

namespace ClosedEnv.Services;

public static class SandboxLauncher
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static void Launch(AppProfile profile, SessionOptions options, string? payloadHostPath)
    {
        var status = SandboxFeature.Detect();
        if (!status.CanLaunchSandbox)
        {
            throw new InvalidOperationException(status.Summary);
        }

        if (profile.RequiresPayload)
        {
            if (string.IsNullOrWhiteSpace(payloadHostPath) || !File.Exists(payloadHostPath))
            {
                throw new InvalidOperationException("Выберите файл .exe или .msi для запуска в песочнице.");
            }
        }

        var dataDir = AppPaths.ProfileData(profile.Id);
        Directory.CreateDirectory(dataDir);

        string? guestPayload = null;
        if (!string.IsNullOrWhiteSpace(payloadHostPath))
        {
            guestPayload = CopyPayload(dataDir, payloadHostPath);
        }

        var session = new SessionConfig
        {
            ProfileId = profile.Id,
            Mode = profile.RequiresPayload ? "generic" : profile.Id,
            GuestFirewall = profile.GuestFirewall && options.Networking,
            Allowlist = profile.Allowlist.ToList(),
            PersistFolders = profile.PersistFolders.ToList(),
            DownloadUrl = profile.DownloadUrl,
            InstallerFileName = profile.InstallerFileName,
            LaunchRelativePath = profile.LaunchRelativePath,
            PayloadPath = guestPayload
        };

        File.WriteAllText(
            Path.Combine(dataDir, "session.json"),
            JsonSerializer.Serialize(session, JsonOptions));

        var wsb = WsbGenerator.Write(profile, options);
        var psi = new ProcessStartInfo
        {
            FileName = SandboxFeature.SandboxExe,
            Arguments = "\"" + wsb + "\"",
            UseShellExecute = true
        };
        Process.Start(psi);
    }

    private static string CopyPayload(string dataDir, string sourcePath)
    {
        var full = Path.GetFullPath(sourcePath);
        if (full.StartsWith(@"\\", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Нельзя запускать файл с UNC-пути.");
        }

        var name = Path.GetFileName(full);
        if (string.IsNullOrWhiteSpace(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new InvalidOperationException("Некорректное имя файла.");
        }

        var payloadDir = Path.Combine(dataDir, "payload");
        Directory.CreateDirectory(payloadDir);
        var dest = Path.Combine(payloadDir, name);
        File.Copy(full, dest, overwrite: true);
        return @"C:\ClosedEnv\data\payload\" + name;
    }
}

using System.IO;

namespace ClosedEnv.Services;

public static class AppPaths
{
    public static string Root { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ClosedEnv");

    public static string BundledProfiles => Path.Combine(AppContext.BaseDirectory, "profiles");
    public static string BundledScripts => Path.Combine(AppContext.BaseDirectory, "scripts");
    public static string HostScripts => Path.Combine(Root, "scripts");
    public static string Generated => Path.Combine(Root, "generated");

    public static string ProfileData(string profileId) =>
        Path.Combine(Root, "profiles", Sanitize(profileId), "data");

    public static string WebViewData(string profileId) =>
        Path.Combine(Root, "profiles", Sanitize(profileId), "webview");

    public static string RequestLog(string profileId) =>
        Path.Combine(Root, "profiles", Sanitize(profileId), "requests.jsonl");

    public static string GuestLog(string profileId) =>
        Path.Combine(ProfileData(profileId), "guest.log");

    public static string WsbFile(string profileId) =>
        Path.Combine(Generated, Sanitize(profileId) + ".wsb");

    public static void EnsureLayout()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(HostScripts);
        Directory.CreateDirectory(Generated);
    }

    public static void SyncScripts()
    {
        EnsureLayout();
        if (!Directory.Exists(BundledScripts))
        {
            return;
        }

        foreach (var file in Directory.GetFiles(BundledScripts))
        {
            var dest = Path.Combine(HostScripts, Path.GetFileName(file));
            File.Copy(file, dest, overwrite: true);
        }
    }

    public static string Sanitize(string id)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = id.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray();
        var value = new string(chars).Trim();
        return string.IsNullOrWhiteSpace(value) ? "profile" : value;
    }
}

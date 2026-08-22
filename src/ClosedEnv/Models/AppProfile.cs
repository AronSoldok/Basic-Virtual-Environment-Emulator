namespace ClosedEnv.Models;

public sealed class AppProfile
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Kind { get; set; } = "sandbox";
    public string Description { get; set; } = "";
    public bool AudioInput { get; set; }
    public bool VideoInput { get; set; }
    public bool Clipboard { get; set; }
    public bool Networking { get; set; } = true;
    public bool ProtectedClient { get; set; } = true;
    public bool GuestFirewall { get; set; }
    public int MemoryMb { get; set; } = 4096;
    public bool RequiresPayload { get; set; }
    public string? DownloadUrl { get; set; }
    public string? InstallerFileName { get; set; }
    public string? LaunchRelativePath { get; set; }
    public string? WebUrl { get; set; }
    public List<string> Allowlist { get; set; } = new();
    public List<string> PersistFolders { get; set; } = new();

    public bool IsWeb => string.Equals(Kind, "web", StringComparison.OrdinalIgnoreCase);

    public string DisplayKind => IsWeb ? "WebView" : "Windows Sandbox";
}

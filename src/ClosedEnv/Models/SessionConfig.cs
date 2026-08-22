namespace ClosedEnv.Models;

public sealed class SessionConfig
{
    public string ProfileId { get; set; } = "";
    public string Mode { get; set; } = "";
    public bool GuestFirewall { get; set; }
    public List<string> Allowlist { get; set; } = new();
    public List<string> PersistFolders { get; set; } = new();
    public string? DownloadUrl { get; set; }
    public string? InstallerFileName { get; set; }
    public string InstallRoot { get; set; } = @"C:\ClosedEnv\data\app";
    public string? LaunchRelativePath { get; set; }
    public string? PayloadPath { get; set; }
}

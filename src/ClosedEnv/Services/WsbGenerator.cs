using System.IO;
using System.Xml.Linq;
using ClosedEnv.Models;

namespace ClosedEnv.Services;

public static class WsbGenerator
{
    public static string Write(AppProfile profile, SessionOptions options)
    {
        var dataDir = AppPaths.ProfileData(profile.Id);
        Directory.CreateDirectory(dataDir);
        AppPaths.SyncScripts();

        AssertLocalPath(dataDir);
        AssertLocalPath(AppPaths.HostScripts);

        var config = new XElement("Configuration",
            new XElement("VGpu", "Enable"),
            new XElement("Networking", options.Networking ? "Enable" : "Disable"),
            new XElement("AudioInput", options.AudioInput ? "Enable" : "Disable"),
            new XElement("VideoInput", options.VideoInput ? "Enable" : "Disable"),
            new XElement("ProtectedClient", options.ProtectedClient ? "Enable" : "Disable"),
            new XElement("PrinterRedirection", "Disable"),
            new XElement("ClipboardRedirection", options.Clipboard ? "Enable" : "Disable"),
            new XElement("MappedFolders",
                MappedFolder(dataDir, @"C:\ClosedEnv\data", readOnly: false),
                MappedFolder(AppPaths.HostScripts, @"C:\ClosedEnv\scripts", readOnly: true)),
            new XElement("LogonCommand",
                new XElement("Command", @"C:\ClosedEnv\scripts\sandbox-logon.cmd")));

        if (options.MemoryMb > 0)
        {
            config.Add(new XElement("MemoryInMB", options.MemoryMb.ToString()));
        }

        var path = AppPaths.WsbFile(profile.Id);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var document = new XDocument(new XDeclaration("1.0", "utf-8", "yes"), config);
        document.Save(path);
        return path;
    }

    private static XElement MappedFolder(string host, string sandbox, bool readOnly) =>
        new("MappedFolder",
            new XElement("HostFolder", host),
            new XElement("SandboxFolder", sandbox),
            new XElement("ReadOnly", readOnly ? "true" : "false"));

    private static void AssertLocalPath(string path)
    {
        var full = Path.GetFullPath(path);
        if (!Path.IsPathRooted(full) || full.StartsWith(@"\\", StringComparison.Ordinal) || full.StartsWith("//", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Mapped folder не может быть UNC-путём.");
        }
    }
}

public sealed class SessionOptions
{
    public bool Networking { get; init; } = true;
    public bool AudioInput { get; init; }
    public bool VideoInput { get; init; }
    public bool Clipboard { get; init; }
    public bool ProtectedClient { get; init; } = true;
    public int MemoryMb { get; init; } = 4096;
}

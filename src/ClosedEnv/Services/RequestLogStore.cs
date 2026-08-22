using System.IO;
using System.Text.Json;
using ClosedEnv.Models;

namespace ClosedEnv.Services;

public static class RequestLogStore
{
    private static readonly object Gate = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static void Append(string profileId, RequestLogEntry entry)
    {
        var path = AppPaths.RequestLog(profileId);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var line = JsonSerializer.Serialize(entry, JsonOptions);
        lock (Gate)
        {
            File.AppendAllText(path, line + Environment.NewLine);
        }
    }

    public static void ClearFile(string profileId)
    {
        var path = AppPaths.RequestLog(profileId);
        lock (Gate)
        {
            if (File.Exists(path))
            {
                File.WriteAllText(path, string.Empty);
            }
        }
    }
}

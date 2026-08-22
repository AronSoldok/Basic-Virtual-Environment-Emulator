using System.IO;
using System.Reflection;
using System.Text.Json;
using ClosedEnv.Models;

namespace ClosedEnv.Services;

public static class ProfileStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static IReadOnlyList<AppProfile> Load()
    {
        var result = new List<AppProfile>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (Directory.Exists(AppPaths.BundledProfiles))
        {
            foreach (var file in Directory.GetFiles(AppPaths.BundledProfiles, "*.json"))
            {
                AddJson(result, seen, File.ReadAllText(file), file);
            }
        }
        else
        {
            LoadEmbedded(result, seen);
        }

        var userDir = Path.Combine(AppPaths.Root, "profiles-config");
        if (Directory.Exists(userDir))
        {
            foreach (var file in Directory.GetFiles(userDir, "*.json"))
            {
                AddJson(result, seen, File.ReadAllText(file), file);
            }
        }

        return result
            .OrderBy(p => p.Id switch
            {
                "max-official" => 0,
                "max-web" => 1,
                "generic" => 2,
                _ => 10
            })
            .ThenBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static void LoadEmbedded(List<AppProfile> result, HashSet<string> seen)
    {
        var assembly = typeof(ProfileStore).Assembly;
        foreach (var name in assembly.GetManifestResourceNames())
        {
            if (!name.StartsWith("ClosedEnv.Profiles.", StringComparison.Ordinal) ||
                !name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            using var stream = assembly.GetManifestResourceStream(name);
            if (stream is null)
            {
                continue;
            }

            using var reader = new StreamReader(stream);
            AddJson(result, seen, reader.ReadToEnd(), name);
        }
    }

    private static void AddJson(List<AppProfile> result, HashSet<string> seen, string json, string source)
    {
        try
        {
            var profile = JsonSerializer.Deserialize<AppProfile>(json, JsonOptions);
            if (profile is null || string.IsNullOrWhiteSpace(profile.Id) || !seen.Add(profile.Id))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(profile.Name))
            {
                profile.Name = profile.Id;
            }

            result.Add(profile);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Не удалось прочитать профиль {source}: {ex.Message}", ex);
        }
    }
}

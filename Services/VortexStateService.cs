using System.IO;
using System.Linq;
using System.Text.Json;
using VortexModLists.Models;

namespace VortexModLists.Services;

public sealed class VortexStateService
{
    public string GetDefaultStatePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "Vortex", "temp", "state_backups_full", "hourly.json");
    }

    public IReadOnlyList<ModEntry> LoadMods(string statePath)
    {
        return LoadModsWithSource(statePath).Mods;
    }

    public StateLoadResult LoadModsWithSource(string statePath)
    {
        var candidates = ResolveCandidateFiles(statePath)
            .OrderByDescending(f => 
            {
                try
                {
                    return File.GetLastWriteTimeUtc(f);
                }
                catch
                {
                    return DateTime.MinValue;
                }
            })
            .ToList();

        foreach (var candidate in candidates)
        {
            if (TryLoadFromFile(candidate, out var mods))
            {
                return new StateLoadResult(mods, candidate);
            }
        }

        return new StateLoadResult([], null);
    }

    private static IEnumerable<string> ResolveCandidateFiles(string statePath)
    {
        if (string.IsNullOrWhiteSpace(statePath))
        {
            yield break;
        }

        var fullPath = Path.GetFullPath(statePath.Trim());

        var ordered = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(string candidate)
        {
            if (!File.Exists(candidate))
            {
                return;
            }

            var normalized = Path.GetFullPath(candidate);
            if (seen.Add(normalized))
            {
                ordered.Add(normalized);
            }
        }

        if (File.Exists(fullPath))
        {
            Add(fullPath);
        }

        if (Directory.Exists(fullPath))
        {
            AddPreferredBackupFiles(fullPath, Add);
            AddKnownFiles(fullPath, Add);
            AddSearchPatterns(fullPath, Add);
        }
        else
        {
            var parent = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(parent) && Directory.Exists(parent))
            {
                Add(fullPath);

                var fileName = Path.GetFileName(fullPath);
                if (!string.IsNullOrWhiteSpace(fileName))
                {
                    var maybeDirectory = Path.Combine(parent, fileName);
                    if (Directory.Exists(maybeDirectory))
                    {
                        AddPreferredBackupFiles(maybeDirectory, Add);
                        AddKnownFiles(maybeDirectory, Add);
                        AddSearchPatterns(maybeDirectory, Add);
                    }
                }
            }
        }

        foreach (var item in ordered)
        {
            yield return item;
        }
    }

    private static void AddPreferredBackupFiles(string directoryPath, Action<string> add)
    {
        add(Path.Combine(directoryPath, "temp", "state_backups_full", "hourly.json"));
        add(Path.Combine(directoryPath, "state_backups_full", "hourly.json"));
        add(Path.Combine(directoryPath, "hourly.json"));
    }

    private static void AddKnownFiles(string directoryPath, Action<string> add)
    {
        add(Path.Combine(directoryPath, "state.json"));
        add(Path.Combine(directoryPath, "vortex-state.json"));
    }

    private static void AddSearchPatterns(string directoryPath, Action<string> add)
    {
        foreach (var candidate in Directory.EnumerateFiles(directoryPath, "*.json", SearchOption.AllDirectories))
        {
            add(candidate);
        }
    }

    private static bool TryLoadFromFile(string filePath, out IReadOnlyList<ModEntry> mods)
    {
        mods = [];

        try
        {
            var json = File.ReadAllText(filePath);
            using var document = JsonDocument.Parse(json);

            var root = document.RootElement;
            if (!TryGetPath(root, out var modsNode, "persistent", "mods") || modsNode.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            mods = ParseMods(root, modsNode);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static IReadOnlyList<ModEntry> ParseMods(JsonElement root, JsonElement modsNode)
    {
        var activeMap = BuildActiveMap(root);
        var mods = new List<ModEntry>();

        foreach (var gameProperty in modsNode.EnumerateObject())
        {
            var game = gameProperty.Name;
            if (gameProperty.Value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            foreach (var modProperty in gameProperty.Value.EnumerateObject())
            {
                if (modProperty.Value.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var modNode = modProperty.Value;
                var modId = GetString(modNode, "id")
                            ?? GetString(modNode, "modId")
                            ?? GetNestedString(modNode, "attributes", "modId")
                            ?? modProperty.Name;

                var modName = GetString(modNode, "modName")
                              ?? GetNestedString(modNode, "attributes", "modName")
                              ?? GetNestedString(modNode, "attributes", "customFileName")
                              ?? GetString(modNode, "name")
                              ?? GetNestedString(modNode, "attributes", "name")
                              ?? modProperty.Name;

                var version = GetString(modNode, "version")
                              ?? GetNestedString(modNode, "attributes", "version")
                              ?? string.Empty;

                var homepage = GetString(modNode, "homepage")
                               ?? GetNestedString(modNode, "attributes", "homepage")
                               ?? GetNestedString(modNode, "attributes", "source")
                               ?? string.Empty;

                var key = GetCompositeKey(game, modId);
                var isActive = activeMap.TryGetValue(key, out var active) && active;

                mods.Add(new ModEntry
                {
                    Game = game,
                    ModName = modName,
                    Id = modId,
                    Version = version,
                    Homepage = NormalizeHomepage(homepage),
                    IsActive = isActive
                });
            }
        }

        return mods
            .OrderBy(m => m.Game, StringComparer.OrdinalIgnoreCase)
            .ThenBy(m => m.ModName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static Dictionary<string, bool> BuildActiveMap(JsonElement root)
    {
        var map = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        if (!TryGetPath(root, out var profilesNode, "persistent", "profiles") || profilesNode.ValueKind != JsonValueKind.Object)
        {
            return map;
        }

        foreach (var profileProperty in profilesNode.EnumerateObject())
        {
            var profile = profileProperty.Value;
            if (profile.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var game = GetString(profile, "gameId")
                       ?? GetString(profile, "game")
                       ?? string.Empty;

            if (string.IsNullOrWhiteSpace(game))
            {
                continue;
            }

            if (!TryGetPath(profile, out var modStateNode, "modState") || modStateNode.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            foreach (var stateProperty in modStateNode.EnumerateObject())
            {
                var modState = stateProperty.Value;
                if (modState.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var modId = stateProperty.Name;
                var enabled = GetBool(modState, "enabled") ?? false;
                var key = GetCompositeKey(game, modId);

                if (!map.TryGetValue(key, out var current))
                {
                    map[key] = enabled;
                    continue;
                }

                map[key] = current || enabled;
            }
        }

        return map;
    }

    private static bool TryGetPath(JsonElement element, out JsonElement value, params string[] path)
    {
        value = element;
        foreach (var segment in path)
        {
            if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(segment, out value))
            {
                return false;
            }
        }

        return true;
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };
    }

    private static string? GetNestedString(JsonElement element, string objectName, string propertyName)
    {
        if (!element.TryGetProperty(objectName, out var obj) || obj.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return GetString(obj, propertyName);
    }

    private static bool? GetBool(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.True)
        {
            return true;
        }

        if (value.ValueKind == JsonValueKind.False)
        {
            return false;
        }

        return null;
    }

    private static string NormalizeHomepage(string homepage)
    {
        if (string.IsNullOrWhiteSpace(homepage))
        {
            return string.Empty;
        }

        if (Uri.TryCreate(homepage, UriKind.Absolute, out _))
        {
            return homepage;
        }

        return string.Empty;
    }

    private static string GetCompositeKey(string game, string modId)
    {
        return $"{game}::{modId}";
    }

    public sealed record StateLoadResult(IReadOnlyList<ModEntry> Mods, string? ResolvedSourcePath);
}

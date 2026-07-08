using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using FileSearchPro.Models;

namespace FileSearchPro.Services;

public class ExclusionService
{
    private readonly string _exclusionsPath;

    public ExclusionService()
    {
        var settingsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings");
        _exclusionsPath = Path.Combine(settingsDir, "exclusions.json");
        Directory.CreateDirectory(settingsDir);
    }

    public List<ExclusionRule> LoadExclusions()
    {
        if (!File.Exists(_exclusionsPath))
            return GetDefaultExclusions();

        var json = File.ReadAllText(_exclusionsPath);
        return JsonSerializer.Deserialize<List<ExclusionRule>>(json) ?? GetDefaultExclusions();
    }

    public void SaveExclusions(List<ExclusionRule> exclusions)
    {
        var json = JsonSerializer.Serialize(exclusions, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_exclusionsPath, json);
    }

    public bool IsExcluded(string path, List<ExclusionRule> exclusions)
    {
        var pathParts = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        foreach (var rule in exclusions.Where(e => e.IsEnabled))
        {
            if (rule.Type == "folder")
            {
                if (pathParts.Any(part => MatchGlob(rule.Pattern, part)))
                    return true;
            }
            else if (rule.Type == "file")
            {
                var fileName = Path.GetFileName(path);
                if (MatchGlob(rule.Pattern, fileName))
                    return true;
            }
        }
        return false;
    }

    public bool IsExcludedDirectory(string dirPath, List<ExclusionRule> exclusions)
    {
        var dirName = Path.GetFileName(dirPath);
        return exclusions
            .Where(e => e.IsEnabled && e.Type == "folder")
            .Any(rule => MatchGlob(rule.Pattern, dirName));
    }

    private static bool MatchGlob(string pattern, string name)
    {
        if (pattern.Contains('*'))
        {
            var regexPattern = "^" + Regex.Escape(pattern)
                .Replace("\\*", ".*")
                .Replace("\\?", ".") + "$";
            return Regex.IsMatch(name, regexPattern, RegexOptions.IgnoreCase);
        }
        return string.Equals(pattern, name, StringComparison.OrdinalIgnoreCase);
    }

    private static List<ExclusionRule> GetDefaultExclusions() => new()
    {
        new ExclusionRule { Pattern = "System Volume Information", Type = "folder" },
        new ExclusionRule { Pattern = "$Recycle.Bin", Type = "folder" },
        new ExclusionRule { Pattern = "Windows", Type = "folder" },
        new ExclusionRule { Pattern = "*.tmp", Type = "file" },
        new ExclusionRule { Pattern = "Thumbs.db", Type = "file" },
        new ExclusionRule { Pattern = "LICENSE", Type = "file" },
        new ExclusionRule { Pattern = "LICENSE.*", Type = "file" },
        new ExclusionRule { Pattern = "COPYING", Type = "file" },
        new ExclusionRule { Pattern = "COPYING.*", Type = "file" }
    };
}

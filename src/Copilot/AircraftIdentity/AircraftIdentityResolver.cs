using System.Collections.Concurrent;

namespace Msfs2024Ai.Copilot.AircraftIdentity;

internal sealed class AircraftIdentityResolver
{
    private readonly string[] _packageRoots;
    private readonly ConcurrentDictionary<string, AircraftIdentity?> _cache =
        new(StringComparer.OrdinalIgnoreCase);

    public AircraftIdentityResolver()
        : this(GetDefaultPackageRoots())
    {
    }

    public AircraftIdentityResolver(IEnumerable<string> packageRoots)
    {
        _packageRoots = packageRoots
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public AircraftIdentity? Resolve(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        return _cache.GetOrAdd(title.Trim(), ResolveUncached);
    }

    private AircraftIdentity? ResolveUncached(string title)
    {
        foreach (var root in _packageRoots)
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            AircraftIdentity? fallbackPartialMatch = null;
            foreach (var packageDirectory in EnumerateCandidatePackageDirectories(root, title))
            {
                foreach (var cfgPath in EnumerateAircraftCfgFiles(packageDirectory).Take(500))
                {
                    var identity = TryReadAircraftCfg(cfgPath);
                    if (identity == null)
                    {
                        continue;
                    }

                    if (string.Equals(identity.Title, title, StringComparison.OrdinalIgnoreCase))
                    {
                        return identity;
                    }

                    if (fallbackPartialMatch == null
                        && title.IndexOf(identity.Title, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        fallbackPartialMatch = identity;
                    }
                }
            }

            if (fallbackPartialMatch != null)
            {
                return fallbackPartialMatch;
            }
        }

        return null;
    }

    private static IEnumerable<string> EnumerateCandidatePackageDirectories(
        string root,
        string aircraftTitle)
    {
        IEnumerable<string> packageDirectories;
        try
        {
            packageDirectories = Directory.EnumerateDirectories(
                    root,
                    "*",
                    SearchOption.TopDirectoryOnly)
                .ToArray();
        }
        catch
        {
            yield break;
        }

        var title = aircraftTitle.ToLowerInvariant();
        foreach (var packageDirectory in packageDirectories)
        {
            var name = Path.GetFileName(packageDirectory).ToLowerInvariant();
            if (IsLikelyPackageForTitle(name, title))
            {
                yield return packageDirectory;
            }
        }
    }

    private static bool IsLikelyPackageForTitle(string packageName, string title)
    {
        if (title.Contains("737") || title.Contains("738"))
        {
            return packageName.Contains("737")
                   || packageName.Contains("738")
                   || packageName.Contains("pmdg");
        }

        if (title.Contains("a32nx") || title.Contains("flybywire"))
        {
            return packageName.Contains("flybywire")
                   || packageName.Contains("a32nx")
                   || packageName.Contains("a320");
        }

        if (title.Contains("a321"))
        {
            return packageName.Contains("a321")
                   || packageName.Contains("inibuild")
                   || packageName.Contains("airbus");
        }

        if (title.Contains("a330"))
        {
            return packageName.Contains("a330")
                   || packageName.Contains("e330")
                   || packageName.Contains("inibuild")
                   || packageName.Contains("airbus");
        }

        if (title.Contains("a320") || title.Contains("a20n"))
        {
            return packageName.Contains("a320")
                   || packageName.Contains("a20n")
                   || packageName.Contains("inibuild")
                   || packageName.Contains("airbus");
        }

        return false;
    }

    private static IEnumerable<string> EnumerateAircraftCfgFiles(string root)
    {
        try
        {
            var simObjectCfgs = Directory.EnumerateDirectories(
                    root,
                    "SimObjects",
                    SearchOption.AllDirectories)
                .SelectMany(directory =>
                    Directory.EnumerateFiles(
                        directory,
                        "aircraft.cfg",
                        SearchOption.AllDirectories));

            return simObjectCfgs.Concat(
                Directory.EnumerateFiles(
                    root,
                    "aircraft.cfg",
                    SearchOption.TopDirectoryOnly));
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static AircraftIdentity? TryReadAircraftCfg(string cfgPath)
    {
        try
        {
            var values = ParseCfgValues(cfgPath);
            if (!values.TryGetValue("title", out var title)
                || string.IsNullOrWhiteSpace(title))
            {
                return null;
            }

            var cfgDirectory = Path.GetDirectoryName(cfgPath) ?? "";
            var aircraftDirectory = ResolveAircraftDirectory(cfgDirectory);
            var thumbnailPaths = ResolveThumbnailPaths(values, cfgDirectory, aircraftDirectory);
            var thumbnailPath = thumbnailPaths.FirstOrDefault();

            return new AircraftIdentity
            {
                Title = title,
                Manufacturer = values.TryGetValue("ui_manufacturer", out var manufacturer)
                    ? manufacturer
                    : values.TryGetValue("icao_manufacturer", out var icaoManufacturer)
                        ? icaoManufacturer
                        : "",
                Type = values.TryGetValue("ui_type", out var type)
                    ? type
                    : values.TryGetValue("icao_model", out var icaoModel)
                        ? icaoModel
                        : "",
                Variation = values.TryGetValue("ui_variation", out var variation)
                    ? variation
                    : "",
                CreatedBy = values.TryGetValue("ui_createdby", out var createdBy)
                    ? createdBy
                    : "",
                ThumbnailPath = thumbnailPath,
                ThumbnailPaths = thumbnailPaths
            };
        }
        catch
        {
            return null;
        }
    }

    private static Dictionary<string, string> ParseCfgValues(string cfgPath)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rawLine in File.ReadLines(cfgPath))
        {
            var line = StripComment(rawLine).Trim();
            if (line.Length == 0
                || line.StartsWith("[", StringComparison.Ordinal))
            {
                continue;
            }

            var separator = line.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            var key = line.Substring(0, separator).Trim();
            if (!IsInterestingKey(key) || values.ContainsKey(key))
            {
                continue;
            }

            values[key] = CleanValue(line.Substring(separator + 1));
        }

        return values;
    }

    private static bool IsInterestingKey(string key) =>
        key.Equals("title", StringComparison.OrdinalIgnoreCase)
        || key.Equals("ui_manufacturer", StringComparison.OrdinalIgnoreCase)
        || key.Equals("ui_type", StringComparison.OrdinalIgnoreCase)
        || key.Equals("ui_variation", StringComparison.OrdinalIgnoreCase)
        || key.Equals("ui_createdby", StringComparison.OrdinalIgnoreCase)
        || key.Equals("ui_thumbnailfile", StringComparison.OrdinalIgnoreCase)
        || key.Equals("texture", StringComparison.OrdinalIgnoreCase)
        || key.Equals("icao_manufacturer", StringComparison.OrdinalIgnoreCase)
        || key.Equals("icao_model", StringComparison.OrdinalIgnoreCase);

    private static string StripComment(string line)
    {
        var quote = false;
        for (var index = 0; index < line.Length; index++)
        {
            if (line[index] == '"')
            {
                quote = !quote;
            }
            else if (line[index] == ';' && !quote)
            {
                return line.Substring(0, index);
            }
        }

        return line;
    }

    private static string CleanValue(string value) =>
        value.Trim().Trim('"').Trim();

    private static string ResolveAircraftDirectory(string cfgDirectory)
    {
        var directory = new DirectoryInfo(cfgDirectory);
        if (directory.Name.Equals("config", StringComparison.OrdinalIgnoreCase)
            && directory.Parent != null)
        {
            return directory.Parent.FullName;
        }

        return cfgDirectory;
    }

    private static IReadOnlyList<string> ResolveThumbnailPaths(
        IReadOnlyDictionary<string, string> values,
        string cfgDirectory,
        string aircraftDirectory)
    {
        var candidates = new List<string>();
        if (values.TryGetValue("ui_thumbnailfile", out var uiThumbnail)
            && TryResolveFile(uiThumbnail, cfgDirectory, aircraftDirectory, out var explicitThumbnail))
        {
            candidates.Add(explicitThumbnail);
        }

        AddRelatedLiveryThumbnailCandidates(values, aircraftDirectory, candidates);
        candidates.Add(Path.Combine(aircraftDirectory, "thumbnail", "thumbnail.png"));
        candidates.Add(Path.Combine(aircraftDirectory, "thumbnail", "thumbnail_button.png"));
        candidates.Add(Path.Combine(aircraftDirectory, "thumbnail", "thumbnail_side.png"));
        candidates.Add(Path.Combine(aircraftDirectory, "thumbnail", "thumbnail_variation.png"));
        candidates.Add(Path.Combine(cfgDirectory, "thumbnail", "thumbnail.png"));
        candidates.Add(Path.Combine(cfgDirectory, "thumbnail", "thumbnail_variation.png"));

        var texture = values.TryGetValue("texture", out var textureSuffix)
            ? textureSuffix
            : "";
        var textureDirectory = string.IsNullOrWhiteSpace(texture)
            ? Path.Combine(aircraftDirectory, "TEXTURE")
            : Path.IsPathRooted(texture)
                ? texture
                : Path.Combine(aircraftDirectory, $"TEXTURE.{texture}");
        candidates.Add(Path.Combine(textureDirectory, "thumbnail.png"));
        candidates.Add(Path.Combine(textureDirectory, "thumbnail.jpg"));
        candidates.Add(Path.Combine(textureDirectory, "thumbnail.JPG"));
        candidates.Add(Path.Combine(textureDirectory, "thumbnail_small.jpg"));
        candidates.Add(Path.Combine(textureDirectory, "thumbnail_small.JPG"));

        try
        {
            candidates.AddRange(Directory.EnumerateFiles(
                    aircraftDirectory,
                    "thumbnail*.*",
                    SearchOption.AllDirectories)
                .Where(path =>
                    path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                    || path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                    || path.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))
                .Take(50));
        }
        catch
        {
        }

        return candidates
            .Where(File.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => ScoreThumbnailPath(path, values))
            .ThenBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void AddRelatedLiveryThumbnailCandidates(
        IReadOnlyDictionary<string, string> values,
        string aircraftDirectory,
        List<string> candidates)
    {
        var aircraftRoot = aircraftDirectory;
        var parent = new DirectoryInfo(aircraftRoot);
        while (parent != null
               && !Directory.Exists(Path.Combine(parent.FullName, "liveries")))
        {
            aircraftRoot = parent.FullName;
            parent = parent.Parent;
        }
        if (parent == null)
        {
            return;
        }
        aircraftRoot = parent.FullName;

        var type = values.TryGetValue("ui_type", out var uiType)
            ? uiType
            : "";
        var variation = values.TryGetValue("ui_variation", out var uiVariation)
            ? uiVariation
            : "";
        var tokens = Tokenize($"{type} {variation}");
        var liveriesDirectory = Path.Combine(aircraftRoot, "liveries");
        if (!Directory.Exists(liveriesDirectory))
        {
            return;
        }

        try
        {
            candidates.AddRange(Directory.EnumerateDirectories(liveriesDirectory, "*", SearchOption.AllDirectories)
                .SelectMany(directory => new[]
                {
                    Path.Combine(directory, "thumbnail", "thumbnail.png"),
                    Path.Combine(directory, "thumbnail", "thumbnail_button.png"),
                    Path.Combine(directory, "thumbnail", "thumbnail_side.png")
                })
                .Where(path => CandidateLooksRelated(path, tokens))
                .Take(20));
        }
        catch
        {
        }
    }

    private static bool CandidateLooksRelated(string path, IReadOnlyList<string> tokens)
    {
        var folder = Path.GetDirectoryName(Path.GetDirectoryName(path) ?? "") ?? "";
        var name = folder.ToLowerInvariant();
        return tokens.Count == 0
               || tokens.Any(token => name.Contains(token));
    }

    private static IReadOnlyList<string> Tokenize(string value) =>
        value.ToLowerInvariant()
            .Split(new[] { ' ', '|', '-', '_', '/', '\\', '"', '\'' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(token => token.Length >= 2
                            && token is not "class" and not "two" and not "hd" and not "sc" and not "tc")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static int ScoreThumbnailPath(
        string path,
        IReadOnlyDictionary<string, string> values)
    {
        var lower = path.ToLowerInvariant();
        var score = 100;
        if (lower.Contains("\\liveries\\"))
        {
            score -= 60;
        }
        if (lower.EndsWith("thumbnail.png", StringComparison.OrdinalIgnoreCase))
        {
            score -= 30;
        }
        if (lower.Contains("thumbnail_button") || lower.Contains("thumbnail_side"))
        {
            score -= 10;
        }
        if (lower.Contains("thumbnail_variation"))
        {
            score += 50;
        }
        if (lower.Contains("\\presets\\"))
        {
            score += 20;
        }

        var tokens = Tokenize(
            values.TryGetValue("ui_type", out var type)
                ? type
                : "");
        score -= tokens.Count(token => lower.Contains(token)) * 4;
        return score;
    }

    private static bool TryResolveFile(
        string rawPath,
        string cfgDirectory,
        string aircraftDirectory,
        out string resolved)
    {
        resolved = "";
        if (string.IsNullOrWhiteSpace(rawPath))
        {
            return false;
        }

        var candidates = Path.IsPathRooted(rawPath)
            ? new[] { rawPath }
            : new[]
            {
                Path.Combine(aircraftDirectory, rawPath),
                Path.Combine(cfgDirectory, rawPath)
            };
        resolved = candidates.FirstOrDefault(File.Exists) ?? "";
        return resolved.Length > 0;
    }

    private static string[] GetDefaultPackageRoots()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var packageCache = Path.Combine(
            localAppData,
            "Packages",
            "Microsoft.Limitless_8wekyb3d8bbwe",
            "LocalCache",
            "Packages");

        return new[]
        {
            Path.Combine(packageCache, "Community"),
            Path.Combine(packageCache, "Official"),
            Path.Combine(packageCache, "Official", "OneStore"),
            Path.Combine(packageCache, "Official", "Steam")
        };
    }
}

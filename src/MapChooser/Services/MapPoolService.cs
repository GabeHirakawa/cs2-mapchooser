using MapChooser.Config;
using MapChooser.Contracts.Models;
using Microsoft.Extensions.Logging;

namespace MapChooser.Services;

public class MapPoolService
{
    private readonly ILogger<MapPoolService> _logger;
    private List<Map> _maps = [];

    public MapPoolService(ILogger<MapPoolService> logger)
    {
        _logger = logger;
    }

    public IReadOnlyList<Map> AllMaps => _maps.AsReadOnly();

    public void LoadMaps(string moduleDirectory, MapPoolConfig config)
    {
        var mapListPath = Path.Combine(moduleDirectory, config.MapListFile);
        if (!File.Exists(mapListPath))
        {
            _logger.LogError("Map list file not found: {Path}", mapListPath);
            _maps = [];
            return;
        }

        var maps = new List<Map>();
        foreach (var rawLine in File.ReadAllLines(mapListPath))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith("//"))
                continue;

            var parts = line.Split(':', 2);
            var name = parts[0].Trim();
            var workshopId = parts.Length > 1 ? parts[1].Trim() : null;

            if (!string.IsNullOrEmpty(name))
                maps.Add(new Map(name, workshopId));
        }

        _maps = maps;
        _logger.LogInformation("Loaded {Count} maps from {Path}", _maps.Count, mapListPath);
    }

    public Map? FindMap(string mapName)
    {
        return _maps.FirstOrDefault(m =>
            m.Name.Equals(mapName, StringComparison.OrdinalIgnoreCase));
    }

    public Map? FindMapPartial(string partial)
    {
        var exact = FindMap(partial);
        if (exact is not null) return exact;

        var matches = _maps
            .Where(m => m.Name.Contains(partial, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return matches.Count == 1 ? matches[0] : null;
    }
}

using System.Text.Json;
using System.Text.Json.Serialization;
using MapChooser.Config;
using Microsoft.Extensions.Logging;

namespace MapChooser.Services;

public class MapHistoryEntry
{
    [JsonPropertyName("map")]
    public string Map { get; set; } = "";

    [JsonPropertyName("playedAt")]
    public DateTime PlayedAt { get; set; }
}

public class MapHistoryData
{
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("history")]
    public List<MapHistoryEntry> History { get; set; } = [];
}

public class CooldownService
{
    private readonly ILogger<CooldownService> _logger;
    private MapHistoryData _data = new();
    private string _filePath = "";
    private int _mapsInCoolDown = 3;
    private int _maxHistoryEntries = 20;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public CooldownService(ILogger<CooldownService> logger)
    {
        _logger = logger;
    }

    public void Initialize(string moduleDirectory, MapPoolConfig config)
    {
        _mapsInCoolDown = config.MapsInCoolDown;
        _maxHistoryEntries = config.MaxHistoryEntries;

        var dataDir = Path.Combine(moduleDirectory, "data");
        Directory.CreateDirectory(dataDir);
        _filePath = Path.Combine(dataDir, "map_history.json");

        LoadFromDisk();
    }

    public bool IsMapOnCooldown(string mapName)
    {
        if (_mapsInCoolDown <= 0) return false;

        var recentMaps = _data.History
            .TakeLast(_mapsInCoolDown)
            .Select(h => h.Map);

        return recentMaps.Any(m => m.Equals(mapName, StringComparison.OrdinalIgnoreCase));
    }

    public void RecordMapPlayed(string mapName)
    {
        _data.History.Add(new MapHistoryEntry
        {
            Map = mapName,
            PlayedAt = DateTime.UtcNow
        });

        if (_data.History.Count > _maxHistoryEntries)
        {
            _data.History = _data.History
                .Skip(_data.History.Count - _maxHistoryEntries)
                .ToList();
        }

        SaveToDisk();
    }

    private void LoadFromDisk()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                var json = File.ReadAllText(_filePath);
                _data = JsonSerializer.Deserialize<MapHistoryData>(json, JsonOptions) ?? new MapHistoryData();
                _logger.LogInformation("Loaded {Count} map history entries from disk", _data.History.Count);
            }
            else
            {
                _data = new MapHistoryData();
                _logger.LogInformation("No map history file found, starting fresh");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load map history, starting fresh");
            _data = new MapHistoryData();
        }
    }

    private void SaveToDisk()
    {
        try
        {
            var json = JsonSerializer.Serialize(_data, JsonOptions);
            File.WriteAllText(_filePath, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save map history to disk");
        }
    }
}

# CS2 MapChooser Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Build a modular CS2 map voting plugin suite (MapChooser + RockTheVote + Nominations) for CounterStrikeSharp with native menus, vote weighting, persistent cooldowns, and map change retry.

**Architecture:** Three separate CS# plugins communicating via the Capabilities pattern, with a shared contracts library. MapChooser is the core engine; RTV and Nominations are thin trigger plugins. All use DI via `IPluginServiceCollection<T>`.

**Tech Stack:** .NET 8.0 / C# 12, CounterStrikeSharp API 1.0.315, Microsoft.Extensions.DependencyInjection, System.Text.Json

**Reference Projects:**
- Legacy plugin: `/home/gkh/projects/cs2-rockthevote-legacy/`
- CS# framework: `/home/gkh/projects/CounterStrikeSharp/`
- CS# examples: `/home/gkh/projects/CounterStrikeSharp/examples/`

**Important CS# Notes:**
- `CenterHtmlMenu` constructor requires `BasePlugin` reference: `new CenterHtmlMenu(title, plugin)`
- `MenuManager.OpenCenterHtmlMenu(plugin, player, menu)` requires plugin as first arg
- `PluginCapability<T>` uses string name for matching between plugins
- Config uses `IPluginConfig<T>` interface on plugin class + `BasePluginConfig` for config class
- `Utilities.GetPlayers()` returns all connected players
- `Server.MapName` returns current map name
- `Server.ExecuteCommand(cmd)` runs server console commands
- Plugin DI: implement `IPluginServiceCollection<TPlugin>` alongside plugin class

---

## Task 1: Create Solution Structure & Project Files

**Files:**
- Create: `cs2-mapchooser.sln`
- Create: `src/MapChooser.Contracts/MapChooser.Contracts.csproj`
- Create: `src/MapChooser/MapChooser.csproj`
- Create: `src/RockTheVote/RockTheVote.csproj`
- Create: `src/Nominations/Nominations.csproj`

**Step 1: Create directory structure**

```bash
mkdir -p src/MapChooser.Contracts/Models
mkdir -p src/MapChooser/Services
mkdir -p src/MapChooser/Config
mkdir -p src/MapChooser/Commands
mkdir -p src/RockTheVote/Config
mkdir -p src/Nominations/Config
mkdir -p lang/mapchooser
mkdir -p lang/rockthevote
mkdir -p lang/nominations
```

**Step 2: Create MapChooser.Contracts.csproj**

```xml
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFramework>net8.0</TargetFramework>
        <RootNamespace>MapChooser.Contracts</RootNamespace>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
    </PropertyGroup>
    <ItemGroup>
        <PackageReference Include="CounterStrikeSharp.API" Version="1.0.315" />
    </ItemGroup>
</Project>
```

Note: Contracts references CS# API because `IMapChooserApi` uses `CCSPlayerController`. This is the same pattern the legacy plugin uses. The contracts project is still a plain library — it just needs the CS# types for the API surface.

**Step 3: Create MapChooser.csproj**

```xml
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFramework>net8.0</TargetFramework>
        <RootNamespace>MapChooser</RootNamespace>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
    </PropertyGroup>
    <ItemGroup>
        <PackageReference Include="CounterStrikeSharp.API" Version="1.0.315" />
    </ItemGroup>
    <ItemGroup>
        <ProjectReference Include="..\MapChooser.Contracts\MapChooser.Contracts.csproj" />
    </ItemGroup>
    <ItemGroup>
        <None Update="lang\**\*.*" CopyToOutputDirectory="PreserveNewest" />
        <None Update="maplist.txt" CopyToOutputDirectory="PreserveNewest" />
    </ItemGroup>
</Project>
```

**Step 4: Create RockTheVote.csproj**

```xml
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFramework>net8.0</TargetFramework>
        <RootNamespace>RockTheVote</RootNamespace>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
    </PropertyGroup>
    <ItemGroup>
        <PackageReference Include="CounterStrikeSharp.API" Version="1.0.315" />
    </ItemGroup>
    <ItemGroup>
        <ProjectReference Include="..\MapChooser.Contracts\MapChooser.Contracts.csproj" />
    </ItemGroup>
    <ItemGroup>
        <None Update="lang\**\*.*" CopyToOutputDirectory="PreserveNewest" />
    </ItemGroup>
</Project>
```

**Step 5: Create Nominations.csproj**

```xml
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFramework>net8.0</TargetFramework>
        <RootNamespace>Nominations</RootNamespace>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
    </PropertyGroup>
    <ItemGroup>
        <PackageReference Include="CounterStrikeSharp.API" Version="1.0.315" />
    </ItemGroup>
    <ItemGroup>
        <ProjectReference Include="..\MapChooser.Contracts\MapChooser.Contracts.csproj" />
    </ItemGroup>
    <ItemGroup>
        <None Update="lang\**\*.*" CopyToOutputDirectory="PreserveNewest" />
    </ItemGroup>
</Project>
```

**Step 6: Create solution file and add projects**

```bash
dotnet new sln --name cs2-mapchooser
dotnet sln add src/MapChooser.Contracts/MapChooser.Contracts.csproj
dotnet sln add src/MapChooser/MapChooser.csproj
dotnet sln add src/RockTheVote/RockTheVote.csproj
dotnet sln add src/Nominations/Nominations.csproj
```

**Step 7: Create default maplist.txt**

Create `src/MapChooser/maplist.txt`:
```
de_dust2
de_mirage
de_inferno
de_nuke
de_overpass
de_ancient
de_anubis
de_vertigo
cs_office
cs_italy
```

**Step 8: Verify build**

```bash
dotnet build cs2-mapchooser.sln
```

Expected: Build succeeded with 0 errors.

**Step 9: Commit**

```bash
git add -A
git commit -m "feat: scaffold solution structure with 4 projects"
```

---

## Task 2: Create Shared Contracts (Models + Interfaces)

**Files:**
- Create: `src/MapChooser.Contracts/Models/Map.cs`
- Create: `src/MapChooser.Contracts/Models/VoteConfig.cs`
- Create: `src/MapChooser.Contracts/Models/VoteResult.cs`
- Create: `src/MapChooser.Contracts/Models/NominationResult.cs`
- Create: `src/MapChooser.Contracts/IMapChooserApi.cs`

**Step 1: Create Map model**

```csharp
// src/MapChooser.Contracts/Models/Map.cs
namespace MapChooser.Contracts.Models;

public record Map(string Name, string? WorkshopId = null, string? DisplayName = null)
{
    public string GetDisplayName() => DisplayName ?? Name;
}
```

**Step 2: Create VoteConfig model**

```csharp
// src/MapChooser.Contracts/Models/VoteConfig.cs
namespace MapChooser.Contracts.Models;

public record VoteConfig(
    int MapsToShow = 6,
    int VoteDurationSeconds = 30,
    bool AllowExtend = false,
    int ExtendMinutes = 0,
    bool ChangeMapImmediately = false,
    string TriggerSource = "endofmap"
);
```

**Step 3: Create VoteResult model**

```csharp
// src/MapChooser.Contracts/Models/VoteResult.cs
namespace MapChooser.Contracts.Models;

public record VoteResult(
    Map? Winner,
    float WinnerVotes,
    int TotalVoters,
    bool IsExtend,
    string TriggerSource
);
```

**Step 4: Create NominationResult enum**

```csharp
// src/MapChooser.Contracts/Models/NominationResult.cs
namespace MapChooser.Contracts.Models;

public enum NominationResult
{
    Success,
    MapOnCooldown,
    MapNotInPool,
    AlreadyNominated,
    MaxNominationsReached,
    VoteInProgress,
    IsCurrentMap
}
```

**Step 5: Create IMapChooserApi interface**

```csharp
// src/MapChooser.Contracts/IMapChooserApi.cs
using CounterStrikeSharp.API.Core;
using MapChooser.Contracts.Models;

namespace MapChooser.Contracts;

public class VoteStartedEventArgs : EventArgs
{
    public required VoteConfig Config { get; init; }
    public required IReadOnlyList<Map> Maps { get; init; }
}

public interface IMapChooserApi
{
    // Vote Management
    bool StartVote(VoteConfig config);
    bool IsVoteInProgress { get; }
    Map? NextMap { get; }
    void SetNextMap(Map map);

    // Map Pool
    IReadOnlyList<Map> GetAvailableMaps();
    IReadOnlyList<Map> GetAllMaps();
    bool IsMapOnCooldown(string mapName);

    // Nominations
    NominationResult Nominate(CCSPlayerController player, Map map);
    void RemoveNomination(CCSPlayerController player);
    IReadOnlyDictionary<Map, List<ulong>> GetNominations();

    // Vote Weighting
    float GetVoteWeight(CCSPlayerController player);

    // Events
    event Action<VoteStartedEventArgs>? VoteStarted;
    event Action<VoteResult>? VoteEnded;
    event Action<Map>? MapChangeScheduled;
}
```

**Step 6: Verify build**

```bash
dotnet build cs2-mapchooser.sln
```

Expected: Build succeeded.

**Step 7: Commit**

```bash
git add -A
git commit -m "feat: add shared contracts with IMapChooserApi and models"
```

---

## Task 3: Create MapChooser Configuration

**Files:**
- Create: `src/MapChooser/Config/MapChooserConfig.cs`

**Step 1: Create config classes**

```csharp
// src/MapChooser/Config/MapChooserConfig.cs
using System.Text.Json.Serialization;
using CounterStrikeSharp.API.Core;

namespace MapChooser.Config;

public class MapPoolConfig
{
    [JsonPropertyName("MapListFile")]
    public string MapListFile { get; set; } = "maplist.txt";

    [JsonPropertyName("MapsInCoolDown")]
    public int MapsInCoolDown { get; set; } = 3;

    [JsonPropertyName("MaxHistoryEntries")]
    public int MaxHistoryEntries { get; set; } = 20;
}

public class VoteSettings
{
    [JsonPropertyName("MapsToShow")]
    public int MapsToShow { get; set; } = 6;

    [JsonPropertyName("VoteDurationSeconds")]
    public int VoteDurationSeconds { get; set; } = 30;

    [JsonPropertyName("HideMenuAfterVote")]
    public bool HideMenuAfterVote { get; set; } = true;

    [JsonPropertyName("ShowVoteCounts")]
    public bool ShowVoteCounts { get; set; } = true;
}

public class EndOfMapVoteConfig
{
    [JsonPropertyName("Enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("TriggerSecondsBeforeEnd")]
    public int TriggerSecondsBeforeEnd { get; set; } = 120;

    [JsonPropertyName("TriggerRoundsBeforeEnd")]
    public int TriggerRoundsBeforeEnd { get; set; } = 2;

    [JsonPropertyName("AllowExtend")]
    public bool AllowExtend { get; set; } = true;

    [JsonPropertyName("ExtendMinutes")]
    public int ExtendMinutes { get; set; } = 30;

    [JsonPropertyName("MaxExtends")]
    public int MaxExtends { get; set; } = 3;
}

public class MapChangeConfig
{
    [JsonPropertyName("DelaySeconds")]
    public float DelaySeconds { get; set; } = 3.0f;

    [JsonPropertyName("RetryTimeoutSeconds")]
    public int RetryTimeoutSeconds { get; set; } = 10;

    [JsonPropertyName("MaxRetries")]
    public int MaxRetries { get; set; } = 3;

    [JsonPropertyName("DelayToChangeInTheEnd")]
    public float DelayToChangeInTheEnd { get; set; } = 7.0f;
}

public class CommandSettings
{
    [JsonPropertyName("NextMapShowToAll")]
    public bool NextMapShowToAll { get; set; } = false;

    [JsonPropertyName("TimeLeftShowToAll")]
    public bool TimeLeftShowToAll { get; set; } = false;
}

public class MapChooserConfig : BasePluginConfig
{
    [JsonPropertyName("MapPool")]
    public MapPoolConfig MapPool { get; set; } = new();

    [JsonPropertyName("Vote")]
    public VoteSettings Vote { get; set; } = new();

    [JsonPropertyName("EndOfMapVote")]
    public EndOfMapVoteConfig EndOfMapVote { get; set; } = new();

    [JsonPropertyName("MapChange")]
    public MapChangeConfig MapChange { get; set; } = new();

    [JsonPropertyName("VoteWeights")]
    public Dictionary<string, float> VoteWeights { get; set; } = new()
    {
        ["#silver"] = 1.25f,
        ["#gold"] = 1.5f,
        ["#platinum"] = 2.0f,
        ["#royal"] = 2.5f
    };

    [JsonPropertyName("DefaultVoteWeight")]
    public float DefaultVoteWeight { get; set; } = 1.0f;

    [JsonPropertyName("Commands")]
    public CommandSettings Commands { get; set; } = new();
}
```

**Step 2: Verify build**

```bash
dotnet build src/MapChooser/MapChooser.csproj
```

**Step 3: Commit**

```bash
git add -A
git commit -m "feat: add MapChooser configuration classes"
```

---

## Task 4: Create MapPoolService

Loads maps from maplist.txt. Parses `name` or `name:workshopId` format.

**Files:**
- Create: `src/MapChooser/Services/MapPoolService.cs`

**Step 1: Implement MapPoolService**

```csharp
// src/MapChooser/Services/MapPoolService.cs
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
        // Exact match first
        var exact = FindMap(partial);
        if (exact is not null) return exact;

        // Partial match (contains)
        var matches = _maps
            .Where(m => m.Name.Contains(partial, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return matches.Count == 1 ? matches[0] : null;
    }
}
```

**Step 2: Verify build**

```bash
dotnet build src/MapChooser/MapChooser.csproj
```

**Step 3: Commit**

```bash
git add -A
git commit -m "feat: add MapPoolService for loading map lists"
```

---

## Task 5: Create CooldownService with JSON Persistence

**Files:**
- Create: `src/MapChooser/Services/CooldownService.cs`

**Step 1: Implement CooldownService**

```csharp
// src/MapChooser/Services/CooldownService.cs
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

        // Trim to max entries
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
```

**Step 2: Verify build**

```bash
dotnet build src/MapChooser/MapChooser.csproj
```

**Step 3: Commit**

```bash
git add -A
git commit -m "feat: add CooldownService with JSON persistence"
```

---

## Task 6: Create NominationService

**Files:**
- Create: `src/MapChooser/Services/NominationService.cs`

**Step 1: Implement NominationService**

```csharp
// src/MapChooser/Services/NominationService.cs
using CounterStrikeSharp.API.Core;
using MapChooser.Contracts.Models;
using Microsoft.Extensions.Logging;

namespace MapChooser.Services;

public class NominationService
{
    private readonly ILogger<NominationService> _logger;
    private readonly MapPoolService _mapPool;
    private readonly CooldownService _cooldown;

    // SteamId64 -> nominated Map
    private readonly Dictionary<ulong, Map> _nominations = new();
    private int _maxNominationsPerPlayer = 1;

    public NominationService(
        ILogger<NominationService> logger,
        MapPoolService mapPool,
        CooldownService cooldown)
    {
        _logger = logger;
        _mapPool = mapPool;
        _cooldown = cooldown;
    }

    public void Configure(int maxNominationsPerPlayer)
    {
        _maxNominationsPerPlayer = maxNominationsPerPlayer;
    }

    public NominationResult Nominate(CCSPlayerController player, Map map, string currentMap, bool voteInProgress)
    {
        if (voteInProgress)
            return NominationResult.VoteInProgress;

        if (map.Name.Equals(currentMap, StringComparison.OrdinalIgnoreCase))
            return NominationResult.IsCurrentMap;

        if (_mapPool.FindMap(map.Name) is null)
            return NominationResult.MapNotInPool;

        if (_cooldown.IsMapOnCooldown(map.Name))
            return NominationResult.MapOnCooldown;

        var steamId = player.SteamID;

        // Check if player already nominated this exact map
        if (_nominations.TryGetValue(steamId, out var existing) &&
            existing.Name.Equals(map.Name, StringComparison.OrdinalIgnoreCase))
        {
            return NominationResult.AlreadyNominated;
        }

        // Replace any existing nomination for this player
        _nominations[steamId] = map;
        _logger.LogInformation("Player {SteamId} nominated {Map}", steamId, map.Name);
        return NominationResult.Success;
    }

    public void RemoveNomination(ulong steamId)
    {
        _nominations.Remove(steamId);
    }

    /// Returns nominated maps ordered by number of nominators (most first).
    public List<Map> GetNominationWinners()
    {
        return _nominations
            .GroupBy(kvp => kvp.Value.Name, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .Select(g => g.First().Value)
            .ToList();
    }

    /// Returns nominations as map -> list of nominator SteamIds.
    public IReadOnlyDictionary<Map, List<ulong>> GetNominations()
    {
        var result = new Dictionary<Map, List<ulong>>();
        foreach (var (steamId, map) in _nominations)
        {
            // Group by map name to handle same map nominated by different references
            var key = result.Keys.FirstOrDefault(k =>
                k.Name.Equals(map.Name, StringComparison.OrdinalIgnoreCase)) ?? map;

            if (!result.ContainsKey(key))
                result[key] = [];

            result[key].Add(steamId);
        }
        return result;
    }

    public int GetNominationCount(string mapName)
    {
        return _nominations.Values
            .Count(m => m.Name.Equals(mapName, StringComparison.OrdinalIgnoreCase));
    }

    public void Reset()
    {
        _nominations.Clear();
    }
}
```

**Step 2: Verify build**

```bash
dotnet build src/MapChooser/MapChooser.csproj
```

**Step 3: Commit**

```bash
git add -A
git commit -m "feat: add NominationService for tracking player nominations"
```

---

## Task 7: Create VoteWeightService

**Files:**
- Create: `src/MapChooser/Services/VoteWeightService.cs`

**Step 1: Implement VoteWeightService**

```csharp
// src/MapChooser/Services/VoteWeightService.cs
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using Microsoft.Extensions.Logging;

namespace MapChooser.Services;

public class VoteWeightService
{
    private readonly ILogger<VoteWeightService> _logger;
    private Dictionary<string, float> _weights = new();
    private float _defaultWeight = 1.0f;

    public VoteWeightService(ILogger<VoteWeightService> logger)
    {
        _logger = logger;
    }

    public void Configure(Dictionary<string, float> weights, float defaultWeight)
    {
        _weights = new Dictionary<string, float>(weights);
        _defaultWeight = defaultWeight;
        _logger.LogInformation("Configured {Count} vote weight groups", _weights.Count);
    }

    public float GetVoteWeight(CCSPlayerController player)
    {
        if (!player.IsValid || player.IsBot || player.IsHLTV)
            return _defaultWeight;

        float highestWeight = _defaultWeight;

        foreach (var (groupName, weight) in _weights)
        {
            if (AdminManager.PlayerInGroup(player, groupName))
            {
                if (weight > highestWeight)
                    highestWeight = weight;
            }
        }

        return highestWeight;
    }
}
```

**Step 2: Verify build**

```bash
dotnet build src/MapChooser/MapChooser.csproj
```

**Step 3: Commit**

```bash
git add -A
git commit -m "feat: add VoteWeightService for admin group-based vote multipliers"
```

---

## Task 8: Create MapChangeService with Retry

**Files:**
- Create: `src/MapChooser/Services/MapChangeService.cs`

**Step 1: Implement MapChangeService**

```csharp
// src/MapChooser/Services/MapChangeService.cs
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using MapChooser.Config;
using MapChooser.Contracts.Models;
using Microsoft.Extensions.Logging;

namespace MapChooser.Services;

public class MapChangeService
{
    private readonly ILogger<MapChangeService> _logger;
    private BasePlugin? _plugin;
    private MapChangeConfig _config = new();

    private Map? _pendingMap;
    private bool _changeInProgress;
    private CounterStrikeSharp.API.Modules.Timers.Timer? _verificationTimer;
    private int _verificationElapsed;
    private int _currentAttempt;

    public Map? PendingMap => _pendingMap;
    public bool IsChangeScheduled => _pendingMap is not null;

    public MapChangeService(ILogger<MapChangeService> logger)
    {
        _logger = logger;
    }

    public void Initialize(BasePlugin plugin, MapChangeConfig config)
    {
        _plugin = plugin;
        _config = config;
    }

    public void ScheduleMapChange(Map map, bool immediate = false)
    {
        if (_changeInProgress)
        {
            _logger.LogWarning("Map change already in progress, replacing target: {Old} -> {New}",
                _pendingMap?.Name, map.Name);
        }

        CancelPendingChange();
        _pendingMap = map;

        if (immediate)
        {
            ExecuteChange(map, 0);
        }
        else
        {
            _logger.LogInformation("Scheduled map change to {Map} in {Delay}s", map.Name, _config.DelaySeconds);
            _plugin!.AddTimer(_config.DelaySeconds, () => ExecuteChange(map, 0));
        }
    }

    public void ScheduleEndOfMapChange(Map map)
    {
        _pendingMap = map;
        _logger.LogInformation("End-of-map change scheduled to {Map}", map.Name);
        // Will be triggered by OnMatchEnd event
    }

    public void TriggerEndOfMapChange()
    {
        if (_pendingMap is null) return;

        var map = _pendingMap;
        var delay = Math.Max(0, _config.DelayToChangeInTheEnd - _config.DelaySeconds);

        _logger.LogInformation("Triggering end-of-map change to {Map} in {Delay}s", map.Name, delay);
        _plugin!.AddTimer(delay, () => ExecuteChange(map, 0));
    }

    private void ExecuteChange(Map map, int attempt)
    {
        if (_plugin is null) return;

        _changeInProgress = true;
        _currentAttempt = attempt;

        var command = GetChangeCommand(map);
        _logger.LogInformation("Executing map change: {Command} (attempt {Attempt}/{Max})",
            command, attempt + 1, _config.MaxRetries);

        Server.ExecuteCommand(command);
        StartVerification(map, attempt);
    }

    private void StartVerification(Map map, int attempt)
    {
        _verificationElapsed = 0;

        _verificationTimer = _plugin!.AddTimer(1.0f, () =>
        {
            _verificationElapsed++;

            // Check if map changed successfully
            if (Server.MapName.Equals(map.Name, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Map change to {Map} verified successfully", map.Name);
                CleanupVerification();
                return;
            }

            if (_verificationElapsed >= _config.RetryTimeoutSeconds)
            {
                CleanupVerification();

                if (attempt + 1 < _config.MaxRetries)
                {
                    _logger.LogWarning("Map change to {Map} failed after {Timeout}s, retrying (attempt {Next}/{Max})",
                        map.Name, _config.RetryTimeoutSeconds, attempt + 2, _config.MaxRetries);
                    ExecuteChange(map, attempt + 1);
                }
                else
                {
                    _logger.LogError("Map change to {Map} failed after {Max} attempts, giving up",
                        map.Name, _config.MaxRetries);
                    Server.PrintToChatAll($" \x02[MapChooser]\x01 Failed to change map to {map.Name} after {_config.MaxRetries} attempts!");
                    _changeInProgress = false;
                }
            }
        }, CounterStrikeSharp.API.Modules.Timers.TimerFlags.REPEAT);
    }

    private string GetChangeCommand(Map map)
    {
        if (Server.IsMapValid(map.Name))
            return $"changelevel {map.Name}";

        if (map.WorkshopId is not null)
            return $"host_workshop_map {map.WorkshopId}";

        return $"ds_workshop_changelevel {map.Name}";
    }

    private void CleanupVerification()
    {
        _verificationTimer?.Kill();
        _verificationTimer = null;
    }

    public void CancelPendingChange()
    {
        CleanupVerification();
        _changeInProgress = false;
    }

    public void Reset()
    {
        CancelPendingChange();
        _pendingMap = null;
    }
}
```

**Step 2: Verify build**

```bash
dotnet build src/MapChooser/MapChooser.csproj
```

**Step 3: Commit**

```bash
git add -A
git commit -m "feat: add MapChangeService with retry verification mechanism"
```

---

## Task 9: Create VoteService (Voting Engine + CenterHtmlMenu)

This is the core voting engine. It creates the CenterHtmlMenu, tracks weighted votes, runs the timer, and determines the winner.

**Files:**
- Create: `src/MapChooser/Services/VoteService.cs`

**Step 1: Implement VoteService**

```csharp
// src/MapChooser/Services/VoteService.cs
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Menu;
using MapChooser.Config;
using MapChooser.Contracts;
using MapChooser.Contracts.Models;
using Microsoft.Extensions.Logging;

namespace MapChooser.Services;

public class VoteService
{
    private readonly ILogger<VoteService> _logger;
    private readonly MapPoolService _mapPool;
    private readonly CooldownService _cooldown;
    private readonly NominationService _nominations;
    private readonly VoteWeightService _voteWeight;
    private readonly MapChangeService _mapChange;

    private BasePlugin? _plugin;
    private MapChooserConfig _config = new();

    // Vote state
    private bool _voteInProgress;
    private readonly Dictionary<string, float> _votes = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<ulong> _votedPlayers = [];
    private List<Map> _voteMaps = [];
    private VoteConfig _currentVoteConfig = new();
    private int _timeRemaining;
    private CounterStrikeSharp.API.Modules.Timers.Timer? _voteTimer;
    private int _extendCount;

    // Extend sentinel
    private const string ExtendOptionKey = "__extend__";

    public bool IsVoteInProgress => _voteInProgress;
    public Map? NextMap { get; private set; }

    // Events
    public event Action<VoteStartedEventArgs>? VoteStarted;
    public event Action<VoteResult>? VoteEnded;
    public event Action<Map>? MapChangeScheduled;

    public VoteService(
        ILogger<VoteService> logger,
        MapPoolService mapPool,
        CooldownService cooldown,
        NominationService nominations,
        VoteWeightService voteWeight,
        MapChangeService mapChange)
    {
        _logger = logger;
        _mapPool = mapPool;
        _cooldown = cooldown;
        _nominations = nominations;
        _voteWeight = voteWeight;
        _mapChange = mapChange;
    }

    public void Initialize(BasePlugin plugin, MapChooserConfig config)
    {
        _plugin = plugin;
        _config = config;
    }

    public void SetNextMap(Map map)
    {
        NextMap = map;
        MapChangeScheduled?.Invoke(map);
    }

    public bool StartVote(VoteConfig voteConfig)
    {
        if (_voteInProgress)
        {
            _logger.LogWarning("Vote already in progress, ignoring StartVote");
            return false;
        }

        if (_mapChange.IsChangeScheduled)
        {
            _logger.LogWarning("Map change already scheduled, ignoring StartVote");
            return false;
        }

        _currentVoteConfig = voteConfig;
        _voteInProgress = true;
        _votes.Clear();
        _votedPlayers.Clear();

        // Build map list: nominations first, then random from pool
        var nominated = _nominations.GetNominationWinners();
        var currentMap = Server.MapName;
        var available = _mapPool.AllMaps
            .Where(m => !m.Name.Equals(currentMap, StringComparison.OrdinalIgnoreCase))
            .Where(m => !_cooldown.IsMapOnCooldown(m.Name))
            .Where(m => !nominated.Any(n => n.Name.Equals(m.Name, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(_ => Random.Shared.Next())
            .ToList();

        var mapsToShow = voteConfig.MapsToShow;
        _voteMaps = nominated
            .Concat(available)
            .Take(mapsToShow)
            .ToList();

        if (_voteMaps.Count == 0)
        {
            _logger.LogWarning("No maps available for vote");
            _voteInProgress = false;
            return false;
        }

        // Initialize vote counts
        foreach (var map in _voteMaps)
            _votes[map.Name] = 0;

        // Add extend option if allowed
        var allowExtend = voteConfig.AllowExtend &&
                          voteConfig.ExtendMinutes > 0 &&
                          _extendCount < _config.EndOfMapVote.MaxExtends;

        if (allowExtend)
            _votes[ExtendOptionKey] = 0;

        // Create and open menu
        OpenVoteMenu(allowExtend);

        // Start timer
        _timeRemaining = voteConfig.VoteDurationSeconds;
        _voteTimer = _plugin!.AddTimer(1.0f, VoteTimerTick,
            CounterStrikeSharp.API.Modules.Timers.TimerFlags.REPEAT);

        VoteStarted?.Invoke(new VoteStartedEventArgs
        {
            Config = voteConfig,
            Maps = _voteMaps.AsReadOnly()
        });

        _logger.LogInformation("Vote started with {Count} maps for {Duration}s (source: {Source})",
            _voteMaps.Count, voteConfig.VoteDurationSeconds, voteConfig.TriggerSource);

        return true;
    }

    private void OpenVoteMenu(bool allowExtend)
    {
        if (_plugin is null) return;

        var menu = new CenterHtmlMenu(_plugin.Localizer["emv.hud.menu-title"], _plugin);
        menu.PostSelectAction = PostSelectAction.Close;

        foreach (var map in _voteMaps)
        {
            var isNominated = _nominations.GetNominationCount(map.Name) > 0;
            var prefix = isNominated ? "\u2605 " : "";
            var displayText = $"{prefix}{map.GetDisplayName()}";

            menu.AddMenuOption(displayText, (player, option) =>
            {
                OnPlayerVote(player, map.Name);
            });
        }

        if (allowExtend)
        {
            var extendText = _plugin.Localizer["emv.extend-option",
                _currentVoteConfig.ExtendMinutes];
            menu.AddMenuOption(extendText, (player, option) =>
            {
                OnPlayerVote(player, ExtendOptionKey);
            });
        }

        // Open for all valid players
        foreach (var player in Utilities.GetPlayers()
            .Where(p => p is { IsValid: true, IsBot: false, IsHLTV: false }))
        {
            MenuManager.OpenCenterHtmlMenu(_plugin, player, menu);
        }
    }

    private void OnPlayerVote(CCSPlayerController player, string mapKey)
    {
        var steamId = player.SteamID;
        if (_votedPlayers.Contains(steamId))
            return;

        _votedPlayers.Add(steamId);
        var weight = _voteWeight.GetVoteWeight(player);
        _votes[mapKey] += weight;

        if (mapKey == ExtendOptionKey)
        {
            player.PrintToChat($" \x02[MapChooser]\x01 {_plugin!.Localizer["emv.you-voted-extend"]}");
        }
        else
        {
            var map = _voteMaps.FirstOrDefault(m => m.Name.Equals(mapKey, StringComparison.OrdinalIgnoreCase));
            player.PrintToChat($" \x02[MapChooser]\x01 {_plugin!.Localizer["emv.you-voted", map?.GetDisplayName() ?? mapKey]}");
        }

        // Check if all valid players have voted
        var validPlayerCount = Utilities.GetPlayers()
            .Count(p => p is { IsValid: true, IsBot: false, IsHLTV: false });

        if (_votedPlayers.Count >= validPlayerCount)
        {
            EndVote();
        }
    }

    private void VoteTimerTick()
    {
        _timeRemaining--;

        if (_timeRemaining <= 0)
        {
            EndVote();
        }
    }

    private void EndVote()
    {
        if (!_voteInProgress) return;
        _voteInProgress = false;

        _voteTimer?.Kill();
        _voteTimer = null;

        // Close all menus
        foreach (var player in Utilities.GetPlayers()
            .Where(p => p is { IsValid: true, IsBot: false, IsHLTV: false }))
        {
            MenuManager.CloseActiveMenu(player);
        }

        // Determine winner
        var totalVotes = _votes.Values.Sum();
        string winnerKey;
        float winnerVotes;

        if (totalVotes <= 0)
        {
            // No votes - pick random from vote maps
            var randomWinner = _voteMaps[Random.Shared.Next(_voteMaps.Count)];
            winnerKey = randomWinner.Name;
            winnerVotes = 0;
        }
        else
        {
            // Highest votes, random tiebreak
            var maxVotes = _votes.Values.Max();
            var winners = _votes.Where(kvp => Math.Abs(kvp.Value - maxVotes) < 0.01f).ToList();
            var winner = winners[Random.Shared.Next(winners.Count)];
            winnerKey = winner.Key;
            winnerVotes = winner.Value;
        }

        var isExtend = winnerKey == ExtendOptionKey;

        if (isExtend)
        {
            HandleExtend();
            VoteEnded?.Invoke(new VoteResult(null, winnerVotes, _votedPlayers.Count, true,
                _currentVoteConfig.TriggerSource));
            return;
        }

        var winnerMap = _voteMaps.FirstOrDefault(m =>
            m.Name.Equals(winnerKey, StringComparison.OrdinalIgnoreCase)) ?? new Map(winnerKey);

        NextMap = winnerMap;

        // Announce
        var percentage = totalVotes > 0 ? (winnerVotes / totalVotes) * 100 : 0;
        Server.PrintToChatAll(
            $" \x02[MapChooser]\x01 {_plugin!.Localizer["emv.vote-ended", winnerMap.GetDisplayName(), percentage, _votedPlayers.Count]}");

        _logger.LogInformation("Vote ended: {Map} won with {Votes} votes ({Percent:F1}%)",
            winnerMap.Name, winnerVotes, percentage);

        VoteEnded?.Invoke(new VoteResult(winnerMap, winnerVotes, _votedPlayers.Count, false,
            _currentVoteConfig.TriggerSource));

        // Schedule map change
        if (_currentVoteConfig.ChangeMapImmediately)
        {
            _mapChange.ScheduleMapChange(winnerMap);
        }
        else
        {
            _mapChange.ScheduleEndOfMapChange(winnerMap);
        }

        MapChangeScheduled?.Invoke(winnerMap);

        // Clear nominations
        _nominations.Reset();
    }

    private void HandleExtend()
    {
        _extendCount++;
        var minutes = _currentVoteConfig.ExtendMinutes;

        _logger.LogInformation("Map extended by {Minutes} minutes (extend #{Count})", minutes, _extendCount);
        Server.PrintToChatAll(
            $" \x02[MapChooser]\x01 {_plugin!.Localizer["emv.vote-extended", minutes]}");

        // Extend the timelimit
        var timeLimit = ConVar.Find("mp_timelimit");
        if (timeLimit is not null)
        {
            var currentValue = timeLimit.GetPrimitiveValue<float>();
            Server.ExecuteCommand($"mp_timelimit {currentValue + minutes}");
        }

        // Extend max rounds if applicable
        var maxRounds = ConVar.Find("mp_maxrounds");
        if (maxRounds is not null)
        {
            var currentValue = maxRounds.GetPrimitiveValue<int>();
            if (currentValue > 0)
            {
                // Extend by a proportional number of rounds (rough: 2 rounds per minute)
                var extraRounds = minutes * 2;
                Server.ExecuteCommand($"mp_maxrounds {currentValue + extraRounds}");
            }
        }
    }

    public void RemovePlayerVotes(ulong steamId)
    {
        _votedPlayers.Remove(steamId);
        // Note: we don't retroactively remove weighted votes from the count
        // since we'd need to know what they voted for. This is acceptable -
        // the vote weight was earned at vote time.
    }

    public void Reset()
    {
        _voteTimer?.Kill();
        _voteTimer = null;
        _voteInProgress = false;
        _votes.Clear();
        _votedPlayers.Clear();
        _voteMaps.Clear();
        NextMap = null;
        _extendCount = 0;
    }
}
```

**Step 2: Verify build**

```bash
dotnet build src/MapChooser/MapChooser.csproj
```

**Step 3: Commit**

```bash
git add -A
git commit -m "feat: add VoteService with CenterHtmlMenu and weighted voting"
```

---

## Task 10: Create Time & Round Trackers

**Files:**
- Create: `src/MapChooser/Services/TimeTracker.cs`
- Create: `src/MapChooser/Services/RoundTracker.cs`
- Create: `src/MapChooser/Services/GameRulesService.cs`

**Step 1: Create GameRulesService**

```csharp
// src/MapChooser/Services/GameRulesService.cs
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using Microsoft.Extensions.Logging;

namespace MapChooser.Services;

public class GameRulesService
{
    private readonly ILogger<GameRulesService> _logger;
    private CCSGameRules? _gameRules;

    public GameRulesService(ILogger<GameRulesService> logger)
    {
        _logger = logger;
    }

    public CCSGameRules? GameRules
    {
        get
        {
            if (_gameRules is null)
                RefreshGameRules();
            return _gameRules;
        }
    }

    public void RefreshGameRules()
    {
        try
        {
            var gameRulesEntities = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules");
            var proxy = gameRulesEntities.FirstOrDefault();
            _gameRules = proxy?.GameRules;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get game rules");
            _gameRules = null;
        }
    }

    public bool IsWarmup => GameRules?.WarmupPeriod ?? false;
}
```

**Step 2: Create TimeTracker**

```csharp
// src/MapChooser/Services/TimeTracker.cs
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Modules.Cvars;
using Microsoft.Extensions.Logging;

namespace MapChooser.Services;

public class TimeTracker
{
    private readonly ILogger<TimeTracker> _logger;
    private readonly GameRulesService _gameRules;

    public TimeTracker(ILogger<TimeTracker> logger, GameRulesService gameRules)
    {
        _logger = logger;
        _gameRules = gameRules;
    }

    public float? GetTimeLimit()
    {
        var cvar = ConVar.Find("mp_timelimit");
        if (cvar is null) return null;
        var value = cvar.GetPrimitiveValue<float>();
        return value > 0 ? value : null;
    }

    public float? GetTimeRemaining()
    {
        var timeLimit = GetTimeLimit();
        if (timeLimit is null) return null;

        var rules = _gameRules.GameRules;
        if (rules is null) return null;

        var gameStart = rules.GameStartTime;
        var currentTime = Server.CurrentTime;
        var elapsed = currentTime - gameStart;
        var remaining = (timeLimit.Value * 60) - elapsed;

        return Math.Max(0, remaining);
    }

    public bool IsTimeBasedMode()
    {
        return GetTimeLimit() is not null;
    }
}
```

**Step 3: Create RoundTracker**

```csharp
// src/MapChooser/Services/RoundTracker.cs
using CounterStrikeSharp.API.Modules.Cvars;
using Microsoft.Extensions.Logging;

namespace MapChooser.Services;

public class RoundTracker
{
    private readonly ILogger<RoundTracker> _logger;
    private readonly GameRulesService _gameRules;

    private int _ctWins;
    private int _tWins;

    public RoundTracker(ILogger<RoundTracker> logger, GameRulesService gameRules)
    {
        _logger = logger;
        _gameRules = gameRules;
    }

    public void OnRoundEnd(int winnerTeam)
    {
        // Team 3 = CT, Team 2 = T
        if (winnerTeam == 3) _ctWins++;
        else if (winnerTeam == 2) _tWins++;
    }

    public int TotalRoundsPlayed => _ctWins + _tWins;

    public int? GetMaxRounds()
    {
        var cvar = ConVar.Find("mp_maxrounds");
        if (cvar is null) return null;
        var value = cvar.GetPrimitiveValue<int>();
        return value > 0 ? value : null;
    }

    public int? GetRoundsRemaining()
    {
        var maxRounds = GetMaxRounds();
        if (maxRounds is null) return null;

        // Check for match point / clinch scenarios
        var winsNeeded = (maxRounds.Value / 2) + 1;
        var maxRoundsLeft = maxRounds.Value - TotalRoundsPlayed;

        // Check if either team can clinch
        var ctCanClinch = winsNeeded - _ctWins;
        var tCanClinch = winsNeeded - _tWins;
        var clinchRemaining = Math.Min(ctCanClinch, tCanClinch);

        return Math.Min(maxRoundsLeft, Math.Max(0, clinchRemaining));
    }

    public bool IsRoundBasedMode()
    {
        return GetMaxRounds() is not null;
    }

    public void Reset()
    {
        _ctWins = 0;
        _tWins = 0;
    }
}
```

**Step 4: Verify build**

```bash
dotnet build src/MapChooser/MapChooser.csproj
```

**Step 5: Commit**

```bash
git add -A
git commit -m "feat: add GameRulesService, TimeTracker, and RoundTracker"
```

---

## Task 11: Create EndOfMapService

Monitors time/rounds and triggers the automatic end-of-map vote.

**Files:**
- Create: `src/MapChooser/Services/EndOfMapService.cs`

**Step 1: Implement EndOfMapService**

```csharp
// src/MapChooser/Services/EndOfMapService.cs
using CounterStrikeSharp.API.Core;
using MapChooser.Config;
using MapChooser.Contracts.Models;
using Microsoft.Extensions.Logging;

namespace MapChooser.Services;

public class EndOfMapService
{
    private readonly ILogger<EndOfMapService> _logger;
    private readonly VoteService _voteService;
    private readonly TimeTracker _timeTracker;
    private readonly RoundTracker _roundTracker;
    private readonly GameRulesService _gameRules;

    private BasePlugin? _plugin;
    private EndOfMapVoteConfig _config = new();
    private MapChooserConfig _fullConfig = new();
    private bool _voteTriggered;

    public EndOfMapService(
        ILogger<EndOfMapService> logger,
        VoteService voteService,
        TimeTracker timeTracker,
        RoundTracker roundTracker,
        GameRulesService gameRules)
    {
        _logger = logger;
        _voteService = voteService;
        _timeTracker = timeTracker;
        _roundTracker = roundTracker;
        _gameRules = gameRules;
    }

    public void Initialize(BasePlugin plugin, MapChooserConfig config)
    {
        _plugin = plugin;
        _config = config.EndOfMapVote;
        _fullConfig = config;
    }

    public void CheckTimeBasedTrigger()
    {
        if (!_config.Enabled || _voteTriggered || _voteService.IsVoteInProgress)
            return;

        if (_voteService.NextMap is not null)
            return;

        if (_gameRules.IsWarmup)
            return;

        var remaining = _timeTracker.GetTimeRemaining();
        if (remaining is null) return;

        if (remaining.Value <= _config.TriggerSecondsBeforeEnd)
        {
            TriggerVote();
        }
    }

    public void CheckRoundBasedTrigger()
    {
        if (!_config.Enabled || _voteTriggered || _voteService.IsVoteInProgress)
            return;

        if (_voteService.NextMap is not null)
            return;

        if (_gameRules.IsWarmup)
            return;

        var remaining = _roundTracker.GetRoundsRemaining();
        if (remaining is null) return;

        if (remaining.Value <= _config.TriggerRoundsBeforeEnd)
        {
            TriggerVote();
        }
    }

    private void TriggerVote()
    {
        _voteTriggered = true;
        _logger.LogInformation("End-of-map vote triggered");

        var voteConfig = new VoteConfig(
            MapsToShow: _fullConfig.Vote.MapsToShow,
            VoteDurationSeconds: _fullConfig.Vote.VoteDurationSeconds,
            AllowExtend: _config.AllowExtend,
            ExtendMinutes: _config.ExtendMinutes,
            ChangeMapImmediately: false,
            TriggerSource: "endofmap"
        );

        _voteService.StartVote(voteConfig);
    }

    public void Reset()
    {
        _voteTriggered = false;
    }
}
```

**Step 2: Verify build**

```bash
dotnet build src/MapChooser/MapChooser.csproj
```

**Step 3: Commit**

```bash
git add -A
git commit -m "feat: add EndOfMapService for automatic end-of-map vote triggers"
```

---

## Task 12: Create MapChooserApi Implementation

This is the facade that implements `IMapChooserApi` and delegates to internal services.

**Files:**
- Create: `src/MapChooser/MapChooserApi.cs`

**Step 1: Implement MapChooserApi**

```csharp
// src/MapChooser/MapChooserApi.cs
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using MapChooser.Contracts;
using MapChooser.Contracts.Models;
using MapChooser.Services;

namespace MapChooser;

public class MapChooserApi : IMapChooserApi
{
    private readonly VoteService _voteService;
    private readonly MapPoolService _mapPool;
    private readonly CooldownService _cooldown;
    private readonly NominationService _nominations;
    private readonly VoteWeightService _voteWeight;

    public MapChooserApi(
        VoteService voteService,
        MapPoolService mapPool,
        CooldownService cooldown,
        NominationService nominations,
        VoteWeightService voteWeight)
    {
        _voteService = voteService;
        _mapPool = mapPool;
        _cooldown = cooldown;
        _nominations = nominations;
        _voteWeight = voteWeight;

        // Forward events
        _voteService.VoteStarted += args => VoteStarted?.Invoke(args);
        _voteService.VoteEnded += result => VoteEnded?.Invoke(result);
        _voteService.MapChangeScheduled += map => MapChangeScheduled?.Invoke(map);
    }

    // Vote Management
    public bool StartVote(VoteConfig config) => _voteService.StartVote(config);
    public bool IsVoteInProgress => _voteService.IsVoteInProgress;
    public Map? NextMap => _voteService.NextMap;
    public void SetNextMap(Map map) => _voteService.SetNextMap(map);

    // Map Pool
    public IReadOnlyList<Map> GetAvailableMaps()
    {
        var currentMap = Server.MapName;
        return _mapPool.AllMaps
            .Where(m => !m.Name.Equals(currentMap, StringComparison.OrdinalIgnoreCase))
            .Where(m => !_cooldown.IsMapOnCooldown(m.Name))
            .ToList()
            .AsReadOnly();
    }

    public IReadOnlyList<Map> GetAllMaps() => _mapPool.AllMaps;

    public bool IsMapOnCooldown(string mapName) => _cooldown.IsMapOnCooldown(mapName);

    // Nominations
    public NominationResult Nominate(CCSPlayerController player, Map map)
    {
        return _nominations.Nominate(player, map, Server.MapName, _voteService.IsVoteInProgress);
    }

    public void RemoveNomination(CCSPlayerController player)
    {
        _nominations.RemoveNomination(player.SteamID);
    }

    public IReadOnlyDictionary<Map, List<ulong>> GetNominations()
    {
        return _nominations.GetNominations();
    }

    // Vote Weighting
    public float GetVoteWeight(CCSPlayerController player)
    {
        return _voteWeight.GetVoteWeight(player);
    }

    // Events
    public event Action<VoteStartedEventArgs>? VoteStarted;
    public event Action<VoteResult>? VoteEnded;
    public event Action<Map>? MapChangeScheduled;
}
```

**Step 2: Verify build**

```bash
dotnet build src/MapChooser/MapChooser.csproj
```

**Step 3: Commit**

```bash
git add -A
git commit -m "feat: add MapChooserApi facade implementing IMapChooserApi"
```

---

## Task 13: Create MapChooserPlugin Entry Point

Wires everything together: DI, capability registration, event handlers, lifecycle.

**Files:**
- Create: `src/MapChooser/MapChooserPlugin.cs`

**Step 1: Implement MapChooserPlugin**

```csharp
// src/MapChooser/MapChooserPlugin.cs
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Events;
using MapChooser.Config;
using MapChooser.Contracts;
using MapChooser.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using static CounterStrikeSharp.API.Core.Listeners;

namespace MapChooser;

public class MapChooserServiceCollection : IPluginServiceCollection<MapChooserPlugin>
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<GameRulesService>();
        services.AddSingleton<MapPoolService>();
        services.AddSingleton<CooldownService>();
        services.AddSingleton<NominationService>();
        services.AddSingleton<VoteWeightService>();
        services.AddSingleton<MapChangeService>();
        services.AddSingleton<VoteService>();
        services.AddSingleton<TimeTracker>();
        services.AddSingleton<RoundTracker>();
        services.AddSingleton<EndOfMapService>();
        services.AddSingleton<MapChooserApi>();
    }
}

public class MapChooserPlugin : BasePlugin, IPluginConfig<MapChooserConfig>
{
    public override string ModuleName => "MapChooser";
    public override string ModuleVersion => "1.0.0";
    public override string ModuleAuthor => "cs2-mapchooser";
    public override string ModuleDescription => "Core map voting and selection engine";

    public MapChooserConfig Config { get; set; } = new();

    public static PluginCapability<IMapChooserApi> Capability { get; } = new("mapchooser:api");

    private readonly MapChooserApi _api;
    private readonly MapPoolService _mapPool;
    private readonly CooldownService _cooldown;
    private readonly NominationService _nominations;
    private readonly VoteWeightService _voteWeight;
    private readonly MapChangeService _mapChange;
    private readonly VoteService _voteService;
    private readonly GameRulesService _gameRules;
    private readonly TimeTracker _timeTracker;
    private readonly RoundTracker _roundTracker;
    private readonly EndOfMapService _endOfMap;

    public MapChooserPlugin(
        MapChooserApi api,
        MapPoolService mapPool,
        CooldownService cooldown,
        NominationService nominations,
        VoteWeightService voteWeight,
        MapChangeService mapChange,
        VoteService voteService,
        GameRulesService gameRules,
        TimeTracker timeTracker,
        RoundTracker roundTracker,
        EndOfMapService endOfMap)
    {
        _api = api;
        _mapPool = mapPool;
        _cooldown = cooldown;
        _nominations = nominations;
        _voteWeight = voteWeight;
        _mapChange = mapChange;
        _voteService = voteService;
        _gameRules = gameRules;
        _timeTracker = timeTracker;
        _roundTracker = roundTracker;
        _endOfMap = endOfMap;
    }

    public void OnConfigParsed(MapChooserConfig config)
    {
        Config = config;

        _mapPool.LoadMaps(ModuleDirectory, config.MapPool);
        _cooldown.Initialize(ModuleDirectory, config.MapPool);
        _nominations.Configure(1); // max nominations per player
        _voteWeight.Configure(config.VoteWeights, config.DefaultVoteWeight);
        _mapChange.Initialize(this, config.MapChange);
        _voteService.Initialize(this, config);
        _endOfMap.Initialize(this, config);
    }

    public override void Load(bool hotReload)
    {
        // Register capability
        Capabilities.RegisterPluginCapability(Capability, () => _api);

        // Map lifecycle
        RegisterListener<OnMapStart>(OnMapStart);
        RegisterListener<OnMapEnd>(OnMapEnd);
        RegisterListener<OnTick>(OnTick);

        // Player events
        RegisterEventHandler<EventPlayerDisconnect>(OnPlayerDisconnect);
        RegisterEventHandler<EventRoundEnd>(OnRoundEnd);
        RegisterEventHandler<EventCsWinPanelMatch>(OnMatchEnd);

        Logger.LogInformation("MapChooser loaded");

        if (hotReload)
        {
            _gameRules.RefreshGameRules();
        }
    }

    private void OnMapStart(string mapName)
    {
        // Reset all state for new map
        _voteService.Reset();
        _mapChange.Reset();
        _nominations.Reset();
        _roundTracker.Reset();
        _endOfMap.Reset();
        _gameRules.RefreshGameRules();

        // Record this map in cooldown history
        _cooldown.RecordMapPlayed(mapName);

        // Reload map pool (in case maplist.txt changed)
        _mapPool.LoadMaps(ModuleDirectory, Config.MapPool);

        Logger.LogInformation("Map started: {Map}", mapName);
    }

    private void OnMapEnd()
    {
        _voteService.Reset();
        _mapChange.Reset();
    }

    private void OnTick()
    {
        // Check time-based end-of-map vote trigger
        _endOfMap.CheckTimeBasedTrigger();
    }

    private HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player is null || !player.IsValid || player.IsBot || player.IsHLTV)
            return HookResult.Continue;

        var steamId = player.SteamID;
        _nominations.RemoveNomination(steamId);
        _voteService.RemovePlayerVotes(steamId);

        return HookResult.Continue;
    }

    private HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info)
    {
        _roundTracker.OnRoundEnd(@event.Winner);
        _gameRules.RefreshGameRules();

        // Check round-based trigger
        _endOfMap.CheckRoundBasedTrigger();

        return HookResult.Continue;
    }

    private HookResult OnMatchEnd(EventCsWinPanelMatch @event, GameEventInfo info)
    {
        // Trigger the actual map change at match end
        _mapChange.TriggerEndOfMapChange();
        return HookResult.Continue;
    }
}
```

**Step 2: Verify build**

```bash
dotnet build src/MapChooser/MapChooser.csproj
```

**Step 3: Commit**

```bash
git add -A
git commit -m "feat: add MapChooserPlugin entry point with DI, capability, and event handlers"
```

---

## Task 14: Create MapChooser Commands

**Files:**
- Create: `src/MapChooser/Commands/NextMapCommand.cs`
- Create: `src/MapChooser/Commands/TimeLeftCommand.cs`
- Create: `src/MapChooser/Commands/AdminCommands.cs`

**Step 1: Create NextMapCommand**

```csharp
// src/MapChooser/Commands/NextMapCommand.cs
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using MapChooser.Config;
using MapChooser.Services;

namespace MapChooser.Commands;

public class NextMapCommand
{
    private readonly VoteService _voteService;

    public NextMapCommand(VoteService voteService)
    {
        _voteService = voteService;
    }

    public void Handle(CCSPlayerController? player, CommandSettings settings, BasePlugin plugin)
    {
        var nextMap = _voteService.NextMap;
        string message;

        if (nextMap is not null)
        {
            message = $" \x02[MapChooser]\x01 {plugin.Localizer["nextmap", nextMap.GetDisplayName()]}";
        }
        else
        {
            message = $" \x02[MapChooser]\x01 {plugin.Localizer["nextmap.decided-by-vote"]}";
        }

        if (settings.NextMapShowToAll || player is null)
        {
            Server.PrintToChatAll(message);
        }
        else
        {
            player.PrintToChat(message);
        }
    }
}
```

**Step 2: Create TimeLeftCommand**

```csharp
// src/MapChooser/Commands/TimeLeftCommand.cs
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using MapChooser.Config;
using MapChooser.Services;

namespace MapChooser.Commands;

public class TimeLeftCommand
{
    private readonly TimeTracker _timeTracker;
    private readonly RoundTracker _roundTracker;

    public TimeLeftCommand(TimeTracker timeTracker, RoundTracker roundTracker)
    {
        _timeTracker = timeTracker;
        _roundTracker = roundTracker;
    }

    public void Handle(CCSPlayerController? player, CommandSettings settings, BasePlugin plugin)
    {
        string message;

        var roundsRemaining = _roundTracker.GetRoundsRemaining();
        if (roundsRemaining is not null)
        {
            if (roundsRemaining.Value <= 0)
            {
                message = $" \x02[Timeleft]\x01 {plugin.Localizer["timeleft.last-round"]}";
            }
            else
            {
                message = $" \x02[Timeleft]\x01 {plugin.Localizer["timeleft.remaining-rounds", roundsRemaining.Value]}";
            }
        }
        else
        {
            var remaining = _timeTracker.GetTimeRemaining();
            if (remaining is null)
            {
                message = $" \x02[Timeleft]\x01 {plugin.Localizer["timeleft.no-time-limit"]}";
            }
            else if (remaining.Value <= 0)
            {
                message = $" \x02[Timeleft]\x01 {plugin.Localizer["timeleft.time-over"]}";
            }
            else
            {
                var ts = TimeSpan.FromSeconds(remaining.Value);
                if (ts.Hours > 0)
                {
                    message = $" \x02[Timeleft]\x01 {plugin.Localizer["timeleft.remaining-time-hour", ts.Hours, ts.Minutes, ts.Seconds]}";
                }
                else if (ts.Minutes > 0)
                {
                    message = $" \x02[Timeleft]\x01 {plugin.Localizer["timeleft.remaining-time-minute", ts.Minutes, ts.Seconds]}";
                }
                else
                {
                    message = $" \x02[Timeleft]\x01 {plugin.Localizer["timeleft.remaining-time-second", ts.Seconds]}";
                }
            }
        }

        if (settings.TimeLeftShowToAll || player is null)
        {
            Server.PrintToChatAll(message);
        }
        else
        {
            player.PrintToChat(message);
        }
    }
}
```

**Step 3: Create AdminCommands**

```csharp
// src/MapChooser/Commands/AdminCommands.cs
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using MapChooser.Contracts.Models;
using MapChooser.Services;

namespace MapChooser.Commands;

public class AdminCommands
{
    private readonly VoteService _voteService;
    private readonly MapPoolService _mapPool;
    private readonly MapChangeService _mapChange;

    public AdminCommands(VoteService voteService, MapPoolService mapPool, MapChangeService mapChange)
    {
        _voteService = voteService;
        _mapPool = mapPool;
        _mapChange = mapChange;
    }

    public void ForceRtv(CCSPlayerController? player, int mapsToShow, int voteDuration)
    {
        if (_voteService.IsVoteInProgress)
        {
            player?.PrintToChat(" \x02[MapChooser]\x01 A vote is already in progress.");
            return;
        }

        Server.PrintToChatAll(" \x02[MapChooser]\x01 Admin forced a map vote!");

        var config = new VoteConfig(
            MapsToShow: mapsToShow,
            VoteDurationSeconds: voteDuration,
            AllowExtend: false,
            ChangeMapImmediately: true,
            TriggerSource: "admin"
        );

        _voteService.StartVote(config);
    }

    public void SetNextMap(CCSPlayerController? player, string mapName)
    {
        var map = _mapPool.FindMapPartial(mapName);
        if (map is null)
        {
            player?.PrintToChat($" \x02[MapChooser]\x01 Map '{mapName}' not found in map pool.");
            return;
        }

        _voteService.SetNextMap(map);
        Server.PrintToChatAll($" \x02[MapChooser]\x01 Admin set next map to \x04{map.GetDisplayName()}\x01.");
    }

    public void ChangeMap(CCSPlayerController? player, string mapName)
    {
        var map = _mapPool.FindMapPartial(mapName);
        if (map is null)
        {
            // Allow changing to maps not in pool (custom maps)
            map = new Map(mapName);
        }

        Server.PrintToChatAll($" \x02[MapChooser]\x01 Admin is changing map to \x04{map.GetDisplayName()}\x01...");
        _mapChange.ScheduleMapChange(map);
    }
}
```

**Step 4: Register commands in MapChooserPlugin**

Add to the `Load` method in `MapChooserPlugin.cs`, after the existing event registrations. Also inject the new command classes.

Add these fields and constructor parameters to `MapChooserPlugin`:

```csharp
// Add to constructor parameters and fields:
private readonly NextMapCommand _nextMapCommand;
private readonly TimeLeftCommand _timeLeftCommand;
private readonly AdminCommands _adminCommands;
```

Add to `Load()`:

```csharp
// Chat commands
AddCommand("css_nextmap", "Show next map", (player, info) =>
{
    _nextMapCommand.Handle(player, Config.Commands, this);
});

AddCommand("css_timeleft", "Show time remaining", (player, info) =>
{
    _timeLeftCommand.Handle(player, Config.Commands, this);
});

// Admin commands
AddCommand("css_forcertv", "Force a map vote", [RequiresPermissions("@css/changemap")] (player, info) =>
{
    _adminCommands.ForceRtv(player, Config.Vote.MapsToShow, Config.Vote.VoteDurationSeconds);
});

AddCommand("css_setnextmap", "Set the next map", [RequiresPermissions("@css/changemap")] (player, info) =>
{
    if (info.ArgCount < 2)
    {
        info.ReplyToCommand("Usage: css_setnextmap <mapname>");
        return;
    }
    _adminCommands.SetNextMap(player, info.GetArg(1));
});

AddCommand("css_changemap", "Immediately change map", [RequiresPermissions("@css/changemap")] (player, info) =>
{
    if (info.ArgCount < 2)
    {
        info.ReplyToCommand("Usage: css_changemap <mapname>");
        return;
    }
    _adminCommands.ChangeMap(player, info.GetArg(1));
});
```

Also register `NextMapCommand`, `TimeLeftCommand`, `AdminCommands` in the DI service collection.

**Step 5: Verify build**

```bash
dotnet build src/MapChooser/MapChooser.csproj
```

**Step 6: Commit**

```bash
git add -A
git commit -m "feat: add nextmap, timeleft, and admin commands"
```

---

## Task 15: Create RockTheVote Plugin

**Files:**
- Create: `src/RockTheVote/Config/RtvConfig.cs`
- Create: `src/RockTheVote/RtvService.cs`
- Create: `src/RockTheVote/RockTheVotePlugin.cs`

**Step 1: Create RtvConfig**

```csharp
// src/RockTheVote/Config/RtvConfig.cs
using System.Text.Json.Serialization;
using CounterStrikeSharp.API.Core;

namespace RockTheVote.Config;

public class RtvConfig : BasePluginConfig
{
    [JsonPropertyName("VotePercentage")]
    public float VotePercentage { get; set; } = 0.6f;

    [JsonPropertyName("MinPlayers")]
    public int MinPlayers { get; set; } = 0;

    [JsonPropertyName("MinRounds")]
    public int MinRounds { get; set; } = 0;

    [JsonPropertyName("EnabledInWarmup")]
    public bool EnabledInWarmup { get; set; } = false;

    [JsonPropertyName("ChangeMapImmediately")]
    public bool ChangeMapImmediately { get; set; } = true;

    [JsonPropertyName("MapsToShow")]
    public int MapsToShow { get; set; } = 6;

    [JsonPropertyName("VoteDurationSeconds")]
    public int VoteDurationSeconds { get; set; } = 30;
}
```

**Step 2: Create RtvService**

```csharp
// src/RockTheVote/RtvService.cs
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using MapChooser.Contracts;
using MapChooser.Contracts.Models;
using RockTheVote.Config;

namespace RockTheVote;

public enum RtvResult
{
    Added,
    AlreadyVoted,
    VotesReached,
    Disabled,
    InWarmup,
    MinPlayersNotMet,
    MinRoundsNotMet,
    VoteInProgress,
    MapChangeScheduled,
    MapChooserUnavailable
}

public class RtvService
{
    private readonly Dictionary<ulong, float> _rtvVotes = new();
    private int _roundsPlayed;
    private bool _isWarmup = true;

    public void OnRoundEnd()
    {
        _roundsPlayed++;
    }

    public void OnWarmupEnd()
    {
        _isWarmup = false;
    }

    public void OnMapStart()
    {
        _rtvVotes.Clear();
        _roundsPlayed = 0;
        _isWarmup = true;
    }

    public void OnPlayerDisconnect(ulong steamId)
    {
        _rtvVotes.Remove(steamId);
    }

    public RtvResult TryRtv(CCSPlayerController player, RtvConfig config, IMapChooserApi? api)
    {
        if (api is null)
            return RtvResult.MapChooserUnavailable;

        if (api.IsVoteInProgress)
            return RtvResult.VoteInProgress;

        if (api.NextMap is not null)
            return RtvResult.MapChangeScheduled;

        if (_isWarmup && !config.EnabledInWarmup)
            return RtvResult.InWarmup;

        var validPlayerCount = Utilities.GetPlayers()
            .Count(p => p is { IsValid: true, IsBot: false, IsHLTV: false });

        if (validPlayerCount < config.MinPlayers)
            return RtvResult.MinPlayersNotMet;

        if (_roundsPlayed < config.MinRounds)
            return RtvResult.MinRoundsNotMet;

        var steamId = player.SteamID;
        if (_rtvVotes.ContainsKey(steamId))
            return RtvResult.AlreadyVoted;

        // Add weighted vote
        var weight = api.GetVoteWeight(player);
        _rtvVotes[steamId] = weight;

        // Check if threshold reached
        var totalWeightedVotes = _rtvVotes.Values.Sum();
        var requiredVotes = (int)Math.Ceiling(validPlayerCount * config.VotePercentage);

        if (totalWeightedVotes >= requiredVotes)
            return RtvResult.VotesReached;

        return RtvResult.Added;
    }

    public float GetTotalWeightedVotes() => _rtvVotes.Values.Sum();

    public int GetVoterCount() => _rtvVotes.Count;

    public int GetRequiredVotes(float votePercentage)
    {
        var validPlayerCount = Utilities.GetPlayers()
            .Count(p => p is { IsValid: true, IsBot: false, IsHLTV: false });

        return (int)Math.Ceiling(validPlayerCount * votePercentage);
    }
}
```

**Step 3: Create RockTheVotePlugin**

```csharp
// src/RockTheVote/RockTheVotePlugin.cs
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using MapChooser.Contracts;
using MapChooser.Contracts.Models;
using Microsoft.Extensions.Logging;
using RockTheVote.Config;
using static CounterStrikeSharp.API.Core.Listeners;

namespace RockTheVote;

public class RockTheVotePlugin : BasePlugin, IPluginConfig<RtvConfig>
{
    public override string ModuleName => "RockTheVote";
    public override string ModuleVersion => "1.0.0";
    public override string ModuleAuthor => "cs2-mapchooser";
    public override string ModuleDescription => "Rock the Vote - trigger map votes";

    public RtvConfig Config { get; set; } = new();

    private static readonly PluginCapability<IMapChooserApi> MapChooserCapability = new("mapchooser:api");

    private readonly RtvService _rtvService = new();

    public void OnConfigParsed(RtvConfig config)
    {
        Config = config;
    }

    public override void Load(bool hotReload)
    {
        RegisterListener<OnMapStart>(mapName => _rtvService.OnMapStart());

        RegisterEventHandler<EventRoundEnd>((@event, info) =>
        {
            _rtvService.OnRoundEnd();
            return HookResult.Continue;
        });

        RegisterEventHandler<EventRoundAnnounceWarmup>((@event, info) =>
        {
            return HookResult.Continue;
        });

        RegisterEventHandler<EventRoundAnnounceLastRoundHalf>((@event, info) =>
        {
            _rtvService.OnWarmupEnd();
            return HookResult.Continue;
        });

        RegisterEventHandler<EventPlayerDisconnect>((@event, info) =>
        {
            var player = @event.Userid;
            if (player is not null && player.IsValid && !player.IsBot)
                _rtvService.OnPlayerDisconnect(player.SteamID);
            return HookResult.Continue;
        });

        // Register rtv command
        AddCommand("css_rtv", "Rock the vote", OnRtvCommand);

        Logger.LogInformation("RockTheVote loaded");
    }

    private void OnRtvCommand(CCSPlayerController? player, CounterStrikeSharp.API.Modules.Commands.CommandInfo info)
    {
        if (player is null || !player.IsValid || player.IsBot)
            return;

        var api = MapChooserCapability.Get();
        var result = _rtvService.TryRtv(player, Config, api);

        switch (result)
        {
            case RtvResult.Added:
                var needed = _rtvService.GetRequiredVotes(Config.VotePercentage);
                var current = _rtvService.GetTotalWeightedVotes();
                Server.PrintToChatAll(
                    $" \x02[RTV]\x01 {Localizer["rtv.rocked-the-vote", player.PlayerName]} " +
                    $"{Localizer["general.votes-needed", $"{current:F0}", needed]}");
                break;

            case RtvResult.VotesReached:
                Server.PrintToChatAll($" \x02[RTV]\x01 {Localizer["rtv.votes-reached"]}");
                var voteConfig = new VoteConfig(
                    MapsToShow: Config.MapsToShow,
                    VoteDurationSeconds: Config.VoteDurationSeconds,
                    AllowExtend: false,
                    ChangeMapImmediately: Config.ChangeMapImmediately,
                    TriggerSource: "rtv"
                );
                api?.StartVote(voteConfig);
                break;

            case RtvResult.AlreadyVoted:
                player.PrintToChat($" \x02[RTV]\x01 {Localizer["rtv.already-rocked-the-vote"]}");
                break;

            case RtvResult.InWarmup:
                player.PrintToChat($" \x02[RTV]\x01 {Localizer["general.validation.warmup"]}");
                break;

            case RtvResult.MinPlayersNotMet:
                player.PrintToChat($" \x02[RTV]\x01 {Localizer["general.validation.minimum-players", Config.MinPlayers]}");
                break;

            case RtvResult.MinRoundsNotMet:
                player.PrintToChat($" \x02[RTV]\x01 {Localizer["general.validation.minimum-rounds", Config.MinRounds]}");
                break;

            case RtvResult.VoteInProgress:
            case RtvResult.MapChangeScheduled:
            case RtvResult.Disabled:
                player.PrintToChat($" \x02[RTV]\x01 {Localizer["rtv.disabled"]}");
                break;

            case RtvResult.MapChooserUnavailable:
                player.PrintToChat(" \x02[RTV]\x01 MapChooser plugin is not loaded.");
                Logger.LogWarning("MapChooser capability not available");
                break;
        }
    }
}
```

**Step 4: Verify build**

```bash
dotnet build src/RockTheVote/RockTheVote.csproj
```

**Step 5: Commit**

```bash
git add -A
git commit -m "feat: add RockTheVote plugin with weighted voting and MapChooser integration"
```

---

## Task 16: Create Nominations Plugin

**Files:**
- Create: `src/Nominations/Config/NominationsConfig.cs`
- Create: `src/Nominations/NominationsPlugin.cs`

**Step 1: Create NominationsConfig**

```csharp
// src/Nominations/Config/NominationsConfig.cs
using System.Text.Json.Serialization;
using CounterStrikeSharp.API.Core;

namespace Nominations.Config;

public class NominationsConfig : BasePluginConfig
{
    [JsonPropertyName("MaxNominationsPerPlayer")]
    public int MaxNominationsPerPlayer { get; set; } = 1;

    [JsonPropertyName("EnabledInWarmup")]
    public bool EnabledInWarmup { get; set; } = false;

    [JsonPropertyName("MinPlayers")]
    public int MinPlayers { get; set; } = 0;
}
```

**Step 2: Create NominationsPlugin**

```csharp
// src/Nominations/NominationsPlugin.cs
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Menu;
using MapChooser.Contracts;
using MapChooser.Contracts.Models;
using Microsoft.Extensions.Logging;
using Nominations.Config;

namespace Nominations;

public class NominationsPlugin : BasePlugin, IPluginConfig<NominationsConfig>
{
    public override string ModuleName => "Nominations";
    public override string ModuleVersion => "1.0.0";
    public override string ModuleAuthor => "cs2-mapchooser";
    public override string ModuleDescription => "Map nomination system";

    public NominationsConfig Config { get; set; } = new();

    private static readonly PluginCapability<IMapChooserApi> MapChooserCapability = new("mapchooser:api");

    public void OnConfigParsed(NominationsConfig config)
    {
        Config = config;
    }

    public override void Load(bool hotReload)
    {
        AddCommand("css_nominate", "Nominate a map", OnNominateCommand);

        Logger.LogInformation("Nominations loaded");
    }

    private void OnNominateCommand(CCSPlayerController? player, CounterStrikeSharp.API.Modules.Commands.CommandInfo info)
    {
        if (player is null || !player.IsValid || player.IsBot)
            return;

        var api = MapChooserCapability.Get();
        if (api is null)
        {
            player.PrintToChat(" \x02[Nominate]\x01 MapChooser plugin is not loaded.");
            return;
        }

        // If player provided a map name argument, nominate directly
        if (info.ArgCount >= 2)
        {
            var mapName = info.GetArg(1);
            NominateDirect(player, api, mapName);
            return;
        }

        // Otherwise, open a menu
        OpenNominationMenu(player, api);
    }

    private void NominateDirect(CCSPlayerController player, IMapChooserApi api, string mapName)
    {
        // Try to find the map
        var available = api.GetAvailableMaps();
        var map = available.FirstOrDefault(m =>
            m.Name.Contains(mapName, StringComparison.OrdinalIgnoreCase));

        if (map is null)
        {
            // Check all maps (might be on cooldown)
            var allMap = api.GetAllMaps().FirstOrDefault(m =>
                m.Name.Contains(mapName, StringComparison.OrdinalIgnoreCase));

            if (allMap is not null && api.IsMapOnCooldown(allMap.Name))
            {
                player.PrintToChat($" \x02[Nominate]\x01 {Localizer["general.validation.map-played-recently"]}");
                return;
            }

            player.PrintToChat($" \x02[Nominate]\x01 {Localizer["general.invalid-map"]}");
            return;
        }

        SubmitNomination(player, api, map);
    }

    private void OpenNominationMenu(CCSPlayerController player, IMapChooserApi api)
    {
        var available = api.GetAvailableMaps();
        if (available.Count == 0)
        {
            player.PrintToChat(" \x02[Nominate]\x01 No maps available for nomination.");
            return;
        }

        var menu = new CenterHtmlMenu(Localizer["nominate.menu-title"], this);
        menu.PostSelectAction = PostSelectAction.Close;

        foreach (var map in available.OrderBy(m => m.Name))
        {
            var nominationCount = api.GetNominations()
                .Where(kvp => kvp.Key.Name.Equals(map.Name, StringComparison.OrdinalIgnoreCase))
                .Select(kvp => kvp.Value.Count)
                .FirstOrDefault();

            var displayText = nominationCount > 0
                ? $"{map.GetDisplayName()} ({nominationCount})"
                : map.GetDisplayName();

            menu.AddMenuOption(displayText, (p, option) =>
            {
                SubmitNomination(p, api, map);
            });
        }

        MenuManager.OpenCenterHtmlMenu(this, player, menu);
    }

    private void SubmitNomination(CCSPlayerController player, IMapChooserApi api, Map map)
    {
        var result = api.Nominate(player, map);

        switch (result)
        {
            case NominationResult.Success:
                var count = api.GetNominations()
                    .Where(kvp => kvp.Key.Name.Equals(map.Name, StringComparison.OrdinalIgnoreCase))
                    .Select(kvp => kvp.Value.Count)
                    .FirstOrDefault();
                Server.PrintToChatAll(
                    $" \x02[Nominate]\x01 {Localizer["nominate.nominated", player.PlayerName, map.GetDisplayName(), count]}");
                break;

            case NominationResult.AlreadyNominated:
                var existingCount = api.GetNominations()
                    .Where(kvp => kvp.Key.Name.Equals(map.Name, StringComparison.OrdinalIgnoreCase))
                    .Select(kvp => kvp.Value.Count)
                    .FirstOrDefault();
                player.PrintToChat(
                    $" \x02[Nominate]\x01 {Localizer["nominate.already-nominated", map.GetDisplayName(), existingCount]}");
                break;

            case NominationResult.MapOnCooldown:
                player.PrintToChat($" \x02[Nominate]\x01 {Localizer["general.validation.map-played-recently"]}");
                break;

            case NominationResult.MapNotInPool:
                player.PrintToChat($" \x02[Nominate]\x01 {Localizer["general.invalid-map"]}");
                break;

            case NominationResult.IsCurrentMap:
                player.PrintToChat($" \x02[Nominate]\x01 {Localizer["general.validation.current-map"]}");
                break;

            case NominationResult.VoteInProgress:
                player.PrintToChat($" \x02[Nominate]\x01 {Localizer["general.validation.disabled"]}");
                break;

            case NominationResult.MaxNominationsReached:
                player.PrintToChat(" \x02[Nominate]\x01 Maximum nominations reached.");
                break;
        }
    }
}
```

**Step 3: Verify build**

```bash
dotnet build src/Nominations/Nominations.csproj
```

**Step 4: Commit**

```bash
git add -A
git commit -m "feat: add Nominations plugin with menu and direct nomination"
```

---

## Task 17: Create Localization Files

Port all 10 languages from the legacy plugin, adapted for the new plugin names and keys.

**Files:**
- Create: `src/MapChooser/lang/en.json` (and 9 other languages)
- Create: `src/RockTheVote/lang/en.json` (and 9 other languages)
- Create: `src/Nominations/lang/en.json` (and 9 other languages)

**Step 1: Create MapChooser English localization**

Create `src/MapChooser/lang/en.json`:
```json
{
  "emv.hud.menu-title": "Vote for the next map:",
  "emv.hud.hud-timer": "Vote for the next map: {0}s",
  "emv.hud.finished": "Vote finished, next map: {0}",
  "emv.you-voted": "You voted for {0}",
  "emv.you-voted-extend": "You voted to extend the map!",
  "emv.vote-ended": "Vote ended, the next map will be {green}{0}{default} ({1:N2}% of {2} vote(s))",
  "emv.vote-ended-no-votes": "No votes, the next map will be {green}{0}",
  "emv.vote-extended": "The map has been extended by {0} minutes!",
  "emv.extend-option": "Extend Map ({0} min)",
  "general.changing-map": "Changing map to {green}{0}",
  "general.changing-map-next-round": "The map will be changed to {green}{0}{default} in the next round...",
  "general.invalid-map": "Invalid map",
  "general.votes-needed": "({0} voted, {1} needed)",
  "general.validation.current-map": "You can't choose the current map",
  "general.validation.minimum-rounds": "Minimum rounds to use this command is {0}",
  "general.validation.warmup": "Command disabled during warmup.",
  "general.validation.minimum-players": "Minimum players to use this command is {0}",
  "general.validation.disabled": "Command disabled right now",
  "general.validation.map-played-recently": "Map has been played recently",
  "nextmap": "Next map will be {green}{0}",
  "nextmap.decided-by-vote": "Next map will be decided by vote",
  "timeleft.remaining-rounds": "{0} round(s) remaining",
  "timeleft.remaining-time-hour": "Remaining time {0}:{1}:{2}",
  "timeleft.remaining-time-minute": "Remaining time {0} minute(s) and {1} second(s)",
  "timeleft.remaining-time-second": "Remaining time {0} second(s)",
  "timeleft.no-time-limit": "There is no time limit",
  "timeleft.last-round": "This is the last round",
  "timeleft.time-over": "Time is over, this is the last round"
}
```

**Step 2: Create RockTheVote English localization**

Create `src/RockTheVote/lang/en.json`:
```json
{
  "rtv.rocked-the-vote": "Player {green}{0}{default} wants to rock the vote",
  "rtv.already-rocked-the-vote": "You already rocked the vote",
  "rtv.votes-reached": "Number of votes reached, starting vote...",
  "rtv.disabled": "RTV is disabled right now",
  "general.votes-needed": "({0} voted, {1} needed)",
  "general.validation.warmup": "Command disabled during warmup.",
  "general.validation.minimum-players": "Minimum players to use this command is {0}",
  "general.validation.minimum-rounds": "Minimum rounds to use this command is {0}"
}
```

**Step 3: Create Nominations English localization**

Create `src/Nominations/lang/en.json`:
```json
{
  "nominate.menu-title": "Nominate a map:",
  "nominate.nominated": "Player {green}{0}{default} nominated map {green}{1}{default}, now it has {2} vote(s)",
  "nominate.already-nominated": "You already nominated the map {green}{0}{default}, it has {1} vote(s)",
  "general.invalid-map": "Invalid map",
  "general.validation.current-map": "You can't choose the current map",
  "general.validation.disabled": "Command disabled right now",
  "general.validation.map-played-recently": "Map has been played recently"
}
```

**Step 4: Port remaining 9 languages**

For each of the remaining languages (fr, hu, lv, pl, pt-BR, ru, tr, ua, zh-Hans), create corresponding files in each plugin's lang directory by copying the legacy translations and adapting keys.

Reference: Legacy translations are at `/home/gkh/projects/cs2-rockthevote-legacy/lang/`

The structure for each language file follows the same keys as English but with translated values. Copy from the legacy plugin's lang files and split between the three plugins based on key prefixes:
- `emv.*`, `general.*`, `nextmap.*`, `timeleft.*` -> MapChooser
- `rtv.*` + shared `general.*` -> RockTheVote
- `nominate.*` + shared `general.*` -> Nominations

**Step 5: Verify build**

```bash
dotnet build cs2-mapchooser.sln
```

**Step 6: Commit**

```bash
git add -A
git commit -m "feat: add localization files for all 10 languages"
```

---

## Task 18: Final Wiring, Build Verification & Cleanup

**Step 1: Update MapChooserPlugin to inject all command classes**

Ensure the final `MapChooserPlugin.cs` has all command classes in its constructor and the `MapChooserServiceCollection` registers them.

Add to `MapChooserServiceCollection.ConfigureServices()`:
```csharp
services.AddSingleton<NextMapCommand>();
services.AddSingleton<TimeLeftCommand>();
services.AddSingleton<AdminCommands>();
```

Add to `MapChooserPlugin` constructor:
```csharp
NextMapCommand nextMapCommand,
TimeLeftCommand timeLeftCommand,
AdminCommands adminCommands
```

And the corresponding field assignments and command registrations in `Load()`.

**Step 2: Full solution build**

```bash
dotnet build cs2-mapchooser.sln --configuration Release
```

Expected: Build succeeded with 0 errors.

**Step 3: Verify output structure**

Check that each plugin produces its own output directory:
```bash
ls -la src/MapChooser/bin/Release/net8.0/
ls -la src/RockTheVote/bin/Release/net8.0/
ls -la src/Nominations/bin/Release/net8.0/
```

Each should contain:
- The plugin DLL
- MapChooser.Contracts.dll (shared dependency)
- lang/ directory with localization files
- maplist.txt (MapChooser only)

**Step 4: Create .gitignore**

```
bin/
obj/
*.user
*.suo
.vs/
.idea/
*.DotSettings.user
```

**Step 5: Commit**

```bash
git add -A
git commit -m "feat: complete wiring, build verification, and cleanup"
```

---

## Summary of All Tasks

| # | Task | Key Output |
|---|---|---|
| 1 | Solution structure & project files | .sln, 4 .csproj files, directories |
| 2 | Shared contracts | IMapChooserApi, Map, VoteConfig, VoteResult, NominationResult |
| 3 | MapChooser config | MapChooserConfig with all sections |
| 4 | MapPoolService | Map list loading from maplist.txt |
| 5 | CooldownService | JSON-persisted map history |
| 6 | NominationService | Player nomination tracking |
| 7 | VoteWeightService | Admin group-based vote multipliers |
| 8 | MapChangeService | Map change execution with retry |
| 9 | VoteService | Core voting engine with CenterHtmlMenu |
| 10 | Time & Round trackers | TimeTracker, RoundTracker, GameRulesService |
| 11 | EndOfMapService | Automatic end-of-map vote triggers |
| 12 | MapChooserApi | Facade implementing IMapChooserApi |
| 13 | MapChooserPlugin | Plugin entry point, DI, capability, events |
| 14 | Commands | nextmap, timeleft, admin commands |
| 15 | RockTheVote plugin | RTV command + weighted threshold |
| 16 | Nominations plugin | Nomination menu + direct nomination |
| 17 | Localization | All 10 languages for all 3 plugins |
| 18 | Final wiring & build | Complete integration, .gitignore |

## Manual Testing Checklist

After deployment to a CS2 server:

1. **MapChooser loads**: Check console for "MapChooser loaded" message
2. **RockTheVote loads**: Check console for "RockTheVote loaded" message
3. **Nominations loads**: Check console for "Nominations loaded" message
4. **`/rtv` works**: Type rtv, see vote count message
5. **RTV threshold**: Multiple players rtv until threshold triggers vote
6. **Vote menu appears**: CenterHtmlMenu shows with map options
7. **Vote counting**: Players can vote, weighted counts display
8. **Vote ends**: Timer expires, winner announced
9. **Map change**: Map actually changes to winner
10. **Map change retry**: If change fails, retry mechanism kicks in
11. **Cooldown persists**: Restart server, check map_history.json exists and is respected
12. **`/nominate`**: Opens menu, selecting a map nominates it
13. **`/nominate de_dust2`**: Direct nomination works
14. **Nominated maps appear first in vote**
15. **`/nextmap`**: Shows next map or "decided by vote"
16. **`/timeleft`**: Shows remaining time/rounds
17. **Admin `css_forcertv`**: Forces a vote
18. **Admin `css_setnextmap`**: Sets next map
19. **Admin `css_changemap`**: Immediately changes map
20. **End-of-map vote**: Triggers automatically near end of time/rounds
21. **Extend option**: Appears in end-of-map vote, extends time when won
22. **Player disconnect**: Votes removed, thresholds recalculated
23. **Vote weight groups**: Players in #silver/#gold groups get weighted votes

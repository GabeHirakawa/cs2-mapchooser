# CS2 MapChooser & RockTheVote Redesign

**Date:** 2026-02-28
**Status:** Approved

## Overview

Remake of cs2-rockthevote-legacy as a modular plugin suite for CounterStrikeSharp (.NET 8.0 / C# 12). Replaces monolithic plugin with separate, capability-linked plugins following SourceMod's mapchooser/rockthevote separation pattern.

## Goals

- Native CenterHtmlMenu UI instead of text-based chat menus
- Vote weighting via CS# admin groups (silver 1.25x, gold 1.5x, platinum 2.0x, royal 2.5x)
- Persistent map cooldown via JSON file (survives server crashes)
- Map change retry mechanism with verification
- Modular plugin architecture with shared contracts
- Feature parity with legacy plugin
- Maximum stability

## Architecture

### Plugin Split

Three plugins + shared contracts library:

| Component | Responsibility |
|---|---|
| **MapChooser.Contracts** | Shared interfaces and models (IMapChooserApi, Map, VoteResult, etc.) |
| **MapChooser** | Core engine: map pool, voting, nominations, cooldown, map changes, end-of-map triggers, /nextmap, /timeleft, admin commands |
| **RockTheVote** | Thin plugin: /rtv command, RTV vote counting, triggers MapChooser vote on threshold |
| **Nominations** | Thin plugin: /nominate command, opens map selection menu, calls MapChooser API |

### Inter-Plugin Communication

Uses CS# Capabilities pattern:
- MapChooser registers `PluginCapability<IMapChooserApi>`
- RTV and Nominations resolve the capability and call methods on it
- If MapChooser isn't loaded, capability returns null; consumers handle gracefully

### Dependency Injection

Each plugin uses `IPluginServiceCollection<T>` to register services via Microsoft.Extensions.DependencyInjection. All services registered as singletons.

## Solution Structure

```
cs2-mapchooser/
├── cs2-mapchooser.sln
├── src/
│   ├── MapChooser.Contracts/
│   │   ├── IMapChooserApi.cs
│   │   ├── IMapPool.cs
│   │   ├── Models/
│   │   │   ├── Map.cs
│   │   │   ├── VoteResult.cs
│   │   │   ├── VoteConfig.cs
│   │   │   └── NominationResult.cs
│   │   └── MapChooser.Contracts.csproj
│   ├── MapChooser/
│   │   ├── MapChooserPlugin.cs
│   │   ├── MapChooserApi.cs
│   │   ├── Services/
│   │   │   ├── MapPoolService.cs
│   │   │   ├── CooldownService.cs
│   │   │   ├── VoteService.cs
│   │   │   ├── MapChangeService.cs
│   │   │   ├── NominationService.cs
│   │   │   ├── EndOfMapService.cs
│   │   │   ├── RoundTracker.cs
│   │   │   └── TimeTracker.cs
│   │   ├── Config/
│   │   │   └── MapChooserConfig.cs
│   │   ├── Commands/
│   │   │   ├── NextMapCommand.cs
│   │   │   ├── TimeLeftCommand.cs
│   │   │   └── AdminCommands.cs
│   │   └── MapChooser.csproj
│   ├── RockTheVote/
│   │   ├── RockTheVotePlugin.cs
│   │   ├── RtvService.cs
│   │   ├── Config/RtvConfig.cs
│   │   └── RockTheVote.csproj
│   └── Nominations/
│       ├── NominationsPlugin.cs
│       ├── NominationTracker.cs
│       ├── Config/NominationsConfig.cs
│       └── Nominations.csproj
├── lang/
│   ├── mapchooser/ (10 languages)
│   ├── rockthevote/ (10 languages)
│   └── nominations/ (10 languages)
└── docs/plans/
```

## Core API: IMapChooserApi

```csharp
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

### Key Models

```csharp
public record Map(string Name, string? WorkshopId = null, string? DisplayName = null);

public record VoteConfig(
    int MapsToShow = 6,
    int VoteDurationSeconds = 30,
    bool AllowExtend = false,
    int ExtendMinutes = 0,
    string TriggerSource = "rtv"
);

public enum NominationResult
{
    Success, MapOnCooldown, MapNotInPool,
    AlreadyNominated, MaxNominationsReached, VoteInProgress
}
```

## Vote Weighting System

### Configuration

MapChooser config maps CS# admin group names to vote multipliers:

```json
{
    "VoteWeights": {
        "#silver": 1.25,
        "#gold": 1.5,
        "#platinum": 2.0,
        "#royal": 2.5
    },
    "DefaultVoteWeight": 1.0
}
```

### Resolution

- Check `AdminManager.PlayerInGroup(player, groupName)` for each configured group
- Highest matching weight wins (player in both silver and gold gets 1.5x)
- Default weight (1.0) for unmatched players
- Applied to both RTV counting and map vote counting

### Counting

- `weightedVotes[map] += GetVoteWeight(player)` per vote cast
- RTV threshold: `weightedRtvVotes >= validPlayerCount * rtvPercentage`
- Map winner: highest weighted votes with random tiebreak

## Map Cooldown Persistence

### Storage

JSON file at `{ModuleDirectory}/data/map_history.json`:

```json
{
    "version": 1,
    "history": [
        { "map": "de_dust2", "playedAt": "2026-02-28T14:30:00Z" },
        { "map": "de_mirage", "playedAt": "2026-02-28T13:45:00Z" }
    ]
}
```

### Behavior

- On map start: append current map, trim to MaxHistoryEntries (default 20), write to file
- On cooldown check: check if map is within last MapsInCoolDown entries (default 3)
- On plugin load: read from file; if corrupted/missing, start fresh
- All writes on main game thread (no concurrency issues)

## Map Change Retry Mechanism

### Flow

1. `ScheduleMapChange(map)` called after vote or admin command
2. Wait configurable delay (default 3s) for end-of-round screen
3. Execute change command:
   - Official maps: `changelevel {name}`
   - Workshop with ID: `host_workshop_map {id}`
   - Workshop without ID: `ds_workshop_changelevel {name}`
4. Start verification timer (1s interval)
5. If `Server.MapName != targetMap` after 10s: retry
6. Max 3 retries. On final failure: log error, notify admins in chat.

### Stability Measures

- Idempotent scheduling (replace, don't stack)
- State cleanup on every map start
- Player disconnect removes their votes and recalculates thresholds
- Graceful degradation when MapChooser capability unavailable

## Vote Flow

### End-of-Map Vote

1. EndOfMapService detects trigger (time or rounds threshold)
2. VoteService builds CenterHtmlMenu with nominated maps (priority) + random maps from pool
3. Optional "Extend Map" option if configured
4. Opens menu for all players, starts countdown timer
5. Players press number keys, votes weighted and counted
6. On timeout/all voted: determine winner, announce, schedule map change

### RTV Flow

1. Player types /rtv, RTV plugin adds weighted vote
2. On threshold reached, RTV calls `IMapChooserApi.StartVote(rtvConfig)`
3. MapChooser takes over for the vote (same as end-of-map flow)

### Nomination Flow

1. Player types /nominate, Nominations plugin opens CenterHtmlMenu of available maps
2. Player selects map, calls `IMapChooserApi.Nominate(player, map)`
3. Nominated maps get priority slots in next vote

## Configuration

### MapChooser.json

```json
{
    "Version": 1,
    "MapPool": {
        "MapListFile": "maplist.txt",
        "MapsInCoolDown": 3,
        "MaxHistoryEntries": 20
    },
    "Vote": {
        "MapsToShow": 6,
        "VoteDurationSeconds": 30,
        "HideMenuAfterVote": true,
        "ShowVoteCounts": true
    },
    "EndOfMapVote": {
        "Enabled": true,
        "TriggerSecondsBeforeEnd": 120,
        "TriggerRoundsBeforeEnd": 2,
        "AllowExtend": true,
        "ExtendMinutes": 30,
        "MaxExtends": 3
    },
    "MapChange": {
        "DelaySeconds": 3.0,
        "RetryTimeoutSeconds": 10,
        "MaxRetries": 3
    },
    "VoteWeights": {
        "#silver": 1.25,
        "#gold": 1.5,
        "#platinum": 2.0,
        "#royal": 2.5
    },
    "DefaultVoteWeight": 1.0,
    "Commands": {
        "NextMapShowToAll": false,
        "TimeLeftShowToAll": false
    }
}
```

### RockTheVote.json

```json
{
    "Version": 1,
    "VotePercentage": 0.6,
    "MinPlayers": 0,
    "MinRounds": 0,
    "EnabledInWarmup": false,
    "ChangeMapImmediately": true
}
```

### Nominations.json

```json
{
    "Version": 1,
    "MaxNominationsPerPlayer": 1,
    "EnabledInWarmup": false,
    "MinPlayers": 0
}
```

## Error Handling

| Scenario | Handling |
|---|---|
| Player disconnects during vote | Remove vote, recalculate totals |
| Player disconnects after RTV | Remove RTV count, recalculate threshold |
| All players leave during vote | Cancel vote, reset state |
| Map change fails | Retry up to 3x with 10s verification |
| Vote started while one in progress | Return false, inform caller |
| Plugin hot-reload | Reset all state |
| Corrupted map_history.json | Log warning, start fresh |
| MapChooser not loaded | Capability returns null, consumers handle |
| Map not in pool nominated | Return MapNotInPool |
| Workshop map ID lookup fails | Fall back to ds_workshop_changelevel |
| Server crash mid-vote | Clean state on restart, cooldown persisted |

## Localization

Port all 10 languages from legacy plugin (en, fr, hu, lv, pl, pt-BR, ru, tr, ua, zh-Hans). Each plugin has its own lang directory. Uses CS#'s built-in IStringLocalizer.

## Technology

- .NET 8.0 / C# 12
- CounterStrikeSharp API
- Microsoft.Extensions.DependencyInjection
- System.Text.Json for config/persistence
- No external NuGet dependencies beyond CS#

using CounterStrikeSharp.API.Core;
using MapChooser.Contracts.Models;
using Microsoft.Extensions.Logging;

namespace MapChooser.Services;

public class NominationService
{
    private readonly ILogger<NominationService> _logger;
    private readonly MapPoolService _mapPool;
    private readonly CooldownService _cooldown;

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

        if (_nominations.TryGetValue(steamId, out var existing) &&
            existing.Name.Equals(map.Name, StringComparison.OrdinalIgnoreCase))
        {
            return NominationResult.AlreadyNominated;
        }

        _nominations[steamId] = map;
        _logger.LogInformation("Player {SteamId} nominated {Map}", steamId, map.Name);
        return NominationResult.Success;
    }

    public void RemoveNomination(ulong steamId)
    {
        _nominations.Remove(steamId);
    }

    public List<Map> GetNominationWinners()
    {
        return _nominations
            .GroupBy(kvp => kvp.Value.Name, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .Select(g => g.First().Value)
            .ToList();
    }

    public IReadOnlyDictionary<Map, List<ulong>> GetNominations()
    {
        var result = new Dictionary<Map, List<ulong>>();
        foreach (var (steamId, map) in _nominations)
        {
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

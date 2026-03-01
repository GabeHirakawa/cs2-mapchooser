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

        _voteService.VoteStarted += args => VoteStarted?.Invoke(args);
        _voteService.VoteEnded += result => VoteEnded?.Invoke(result);
        _voteService.MapChangeScheduled += map => MapChangeScheduled?.Invoke(map);
    }

    public bool StartVote(VoteConfig config) => _voteService.StartVote(config);
    public bool IsVoteInProgress => _voteService.IsVoteInProgress;
    public Map? NextMap => _voteService.NextMap;
    public void SetNextMap(Map map) => _voteService.SetNextMap(map);

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

    public float GetVoteWeight(CCSPlayerController player)
    {
        return _voteWeight.GetVoteWeight(player);
    }

    public event Action<VoteStartedEventArgs>? VoteStarted;
    public event Action<VoteResult>? VoteEnded;
    public event Action<Map>? MapChangeScheduled;
}

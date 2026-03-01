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

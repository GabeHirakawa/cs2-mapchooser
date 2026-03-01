using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using MapChooser.Contracts;
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

        var weight = api.GetVoteWeight(player);
        _rtvVotes[steamId] = weight;

        var totalWeightedVotes = _rtvVotes.Values.Sum();
        var requiredVotes = (int)Math.Ceiling(validPlayerCount * config.VotePercentage);

        if (totalWeightedVotes >= requiredVotes)
            return RtvResult.VotesReached;

        return RtvResult.Added;
    }

    public float GetTotalWeightedVotes() => _rtvVotes.Values.Sum();

    public int GetRequiredVotes(float votePercentage)
    {
        var validPlayerCount = Utilities.GetPlayers()
            .Count(p => p is { IsValid: true, IsBot: false, IsHLTV: false });
        return (int)Math.Ceiling(validPlayerCount * votePercentage);
    }
}

using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Cvars;
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
                var extraRounds = minutes * 2;
                Server.ExecuteCommand($"mp_maxrounds {currentValue + extraRounds}");
            }
        }
    }

    public void RemovePlayerVotes(ulong steamId)
    {
        _votedPlayers.Remove(steamId);
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

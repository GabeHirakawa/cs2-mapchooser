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

using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Modules.Cvars;
using Microsoft.Extensions.Logging;

namespace MapChooser.Services;

public class TimeTracker
{
    private readonly ILogger<TimeTracker> _logger;
    private readonly GameRulesService _gameRules;
    private ConVar? _timeLimitCvar;

    public TimeTracker(ILogger<TimeTracker> logger, GameRulesService gameRules)
    {
        _logger = logger;
        _gameRules = gameRules;
    }

    public void Initialize()
    {
        _timeLimitCvar = ConVar.Find("mp_timelimit");
    }

    public float? GetTimeLimit()
    {
        if (_timeLimitCvar is null) return null;
        var value = _timeLimitCvar.GetPrimitiveValue<float>();
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
}

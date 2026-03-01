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
}

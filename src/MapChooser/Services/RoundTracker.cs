using CounterStrikeSharp.API.Modules.Cvars;
using Microsoft.Extensions.Logging;

namespace MapChooser.Services;

public class RoundTracker
{
    private readonly ILogger<RoundTracker> _logger;

    private int _ctWins;
    private int _tWins;

    public RoundTracker(ILogger<RoundTracker> logger)
    {
        _logger = logger;
    }

    public void OnRoundEnd(int winnerTeam)
    {
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

        var winsNeeded = (maxRounds.Value / 2) + 1;
        var maxRoundsLeft = maxRounds.Value - TotalRoundsPlayed;
        var ctCanClinch = winsNeeded - _ctWins;
        var tCanClinch = winsNeeded - _tWins;
        var clinchRemaining = Math.Min(ctCanClinch, tCanClinch);

        return Math.Min(maxRoundsLeft, Math.Max(0, clinchRemaining));
    }

    public void Reset()
    {
        _ctWins = 0;
        _tWins = 0;
    }
}

using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using Microsoft.Extensions.Logging;

namespace MapChooser.Services;

public class GameRulesService
{
    private readonly ILogger<GameRulesService> _logger;
    private CCSGameRules? _gameRules;

    public GameRulesService(ILogger<GameRulesService> logger)
    {
        _logger = logger;
    }

    public CCSGameRules? GameRules
    {
        get
        {
            if (_gameRules is null)
                RefreshGameRules();
            return _gameRules;
        }
    }

    public void RefreshGameRules()
    {
        try
        {
            var gameRulesEntities = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules");
            var proxy = gameRulesEntities.FirstOrDefault();
            _gameRules = proxy?.GameRules;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get game rules");
            _gameRules = null;
        }
    }

    public bool IsWarmup => GameRules?.WarmupPeriod ?? false;
}

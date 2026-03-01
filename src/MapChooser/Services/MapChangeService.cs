using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using MapChooser.Config;
using MapChooser.Contracts.Models;
using Microsoft.Extensions.Logging;

namespace MapChooser.Services;

public class MapChangeService
{
    private readonly ILogger<MapChangeService> _logger;
    private BasePlugin? _plugin;
    private MapChangeConfig _config = new();

    private Map? _pendingMap;
    private bool _changeInProgress;
    private CounterStrikeSharp.API.Modules.Timers.Timer? _verificationTimer;
    private int _verificationElapsed;
    private int _currentAttempt;

    public Map? PendingMap => _pendingMap;
    public bool IsChangeScheduled => _pendingMap is not null;

    public MapChangeService(ILogger<MapChangeService> logger)
    {
        _logger = logger;
    }

    public void Initialize(BasePlugin plugin, MapChangeConfig config)
    {
        _plugin = plugin;
        _config = config;
    }

    public void ScheduleMapChange(Map map, bool immediate = false)
    {
        if (_changeInProgress)
        {
            _logger.LogWarning("Map change already in progress, replacing target: {Old} -> {New}",
                _pendingMap?.Name, map.Name);
        }

        CancelPendingChange();
        _pendingMap = map;

        if (immediate)
        {
            ExecuteChange(map, 0);
        }
        else
        {
            _logger.LogInformation("Scheduled map change to {Map} in {Delay}s", map.Name, _config.DelaySeconds);
            _plugin!.AddTimer(_config.DelaySeconds, () => ExecuteChange(map, 0));
        }
    }

    public void ScheduleEndOfMapChange(Map map)
    {
        _pendingMap = map;
        _logger.LogInformation("End-of-map change scheduled to {Map}", map.Name);
    }

    public void TriggerEndOfMapChange()
    {
        if (_pendingMap is null) return;

        var map = _pendingMap;
        var delay = Math.Max(0, _config.DelayToChangeInTheEnd - _config.DelaySeconds);

        _logger.LogInformation("Triggering end-of-map change to {Map} in {Delay}s", map.Name, delay);
        _plugin!.AddTimer(delay, () => ExecuteChange(map, 0));
    }

    private void ExecuteChange(Map map, int attempt)
    {
        if (_plugin is null) return;

        _changeInProgress = true;
        _currentAttempt = attempt;

        var command = GetChangeCommand(map);
        _logger.LogInformation("Executing map change: {Command} (attempt {Attempt}/{Max})",
            command, attempt + 1, _config.MaxRetries);

        Server.ExecuteCommand(command);
        StartVerification(map, attempt);
    }

    private void StartVerification(Map map, int attempt)
    {
        _verificationElapsed = 0;

        _verificationTimer = _plugin!.AddTimer(1.0f, () =>
        {
            _verificationElapsed++;

            if (Server.MapName.Equals(map.Name, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Map change to {Map} verified successfully", map.Name);
                CleanupVerification();
                return;
            }

            if (_verificationElapsed >= _config.RetryTimeoutSeconds)
            {
                CleanupVerification();

                if (attempt + 1 < _config.MaxRetries)
                {
                    _logger.LogWarning("Map change to {Map} failed after {Timeout}s, retrying (attempt {Next}/{Max})",
                        map.Name, _config.RetryTimeoutSeconds, attempt + 2, _config.MaxRetries);
                    ExecuteChange(map, attempt + 1);
                }
                else
                {
                    _logger.LogError("Map change to {Map} failed after {Max} attempts, giving up",
                        map.Name, _config.MaxRetries);
                    Server.PrintToChatAll($" \x02[MapChooser]\x01 Failed to change map to {map.Name} after {_config.MaxRetries} attempts!");
                    _changeInProgress = false;
                }
            }
        }, CounterStrikeSharp.API.Modules.Timers.TimerFlags.REPEAT);
    }

    private string GetChangeCommand(Map map)
    {
        if (Server.IsMapValid(map.Name))
            return $"changelevel {map.Name}";

        if (map.WorkshopId is not null)
            return $"host_workshop_map {map.WorkshopId}";

        return $"ds_workshop_changelevel {map.Name}";
    }

    private void CleanupVerification()
    {
        _verificationTimer?.Kill();
        _verificationTimer = null;
    }

    public void CancelPendingChange()
    {
        CleanupVerification();
        _changeInProgress = false;
    }

    public void Reset()
    {
        CancelPendingChange();
        _pendingMap = null;
    }
}

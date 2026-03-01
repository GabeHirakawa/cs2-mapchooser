using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using Microsoft.Extensions.Logging;

namespace MapChooser.Services;

public class VoteWeightService
{
    private readonly ILogger<VoteWeightService> _logger;
    private Dictionary<string, float> _weights = new();
    private float _defaultWeight = 1.0f;

    public VoteWeightService(ILogger<VoteWeightService> logger)
    {
        _logger = logger;
    }

    public void Configure(Dictionary<string, float> weights, float defaultWeight)
    {
        _weights = new Dictionary<string, float>(weights);
        _defaultWeight = defaultWeight;
        _logger.LogInformation("Configured {Count} vote weight groups", _weights.Count);
    }

    public float GetVoteWeight(CCSPlayerController player)
    {
        if (!player.IsValid || player.IsBot || player.IsHLTV)
            return _defaultWeight;

        float highestWeight = _defaultWeight;

        foreach (var (groupName, weight) in _weights)
        {
            if (AdminManager.PlayerInGroup(player, groupName))
            {
                if (weight > highestWeight)
                    highestWeight = weight;
            }
        }

        return highestWeight;
    }
}

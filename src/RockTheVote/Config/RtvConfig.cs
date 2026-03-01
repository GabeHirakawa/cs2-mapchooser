using System.Text.Json.Serialization;
using CounterStrikeSharp.API.Core;

namespace RockTheVote.Config;

public class RtvConfig : BasePluginConfig
{
    [JsonPropertyName("VotePercentage")]
    public float VotePercentage { get; set; } = 0.6f;

    [JsonPropertyName("MinPlayers")]
    public int MinPlayers { get; set; } = 0;

    [JsonPropertyName("MinRounds")]
    public int MinRounds { get; set; } = 0;

    [JsonPropertyName("EnabledInWarmup")]
    public bool EnabledInWarmup { get; set; } = false;

    [JsonPropertyName("ChangeMapImmediately")]
    public bool ChangeMapImmediately { get; set; } = true;

    [JsonPropertyName("MapsToShow")]
    public int MapsToShow { get; set; } = 6;

    [JsonPropertyName("VoteDurationSeconds")]
    public int VoteDurationSeconds { get; set; } = 30;
}

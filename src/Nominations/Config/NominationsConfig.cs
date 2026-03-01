using System.Text.Json.Serialization;
using CounterStrikeSharp.API.Core;

namespace Nominations.Config;

public class NominationsConfig : BasePluginConfig
{
    [JsonPropertyName("MaxNominationsPerPlayer")]
    public int MaxNominationsPerPlayer { get; set; } = 1;

    [JsonPropertyName("EnabledInWarmup")]
    public bool EnabledInWarmup { get; set; } = false;

    [JsonPropertyName("MinPlayers")]
    public int MinPlayers { get; set; } = 0;
}

using System.Text.Json.Serialization;
using CounterStrikeSharp.API.Core;

namespace Nominations.Config;

public class NominationsConfig : BasePluginConfig
{
    [JsonPropertyName("EnabledInWarmup")]
    public bool EnabledInWarmup { get; set; } = false;

    [JsonPropertyName("MinPlayers")]
    public int MinPlayers { get; set; } = 0;
}

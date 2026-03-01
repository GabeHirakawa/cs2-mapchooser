using System.Text.Json.Serialization;
using CounterStrikeSharp.API.Core;

namespace MapChooser.Config;

public class MapPoolConfig
{
    [JsonPropertyName("MapListFile")]
    public string MapListFile { get; set; } = "maplist.txt";

    [JsonPropertyName("MapsInCoolDown")]
    public int MapsInCoolDown { get; set; } = 3;

    [JsonPropertyName("MaxHistoryEntries")]
    public int MaxHistoryEntries { get; set; } = 20;
}

public class VoteSettings
{
    [JsonPropertyName("MapsToShow")]
    public int MapsToShow { get; set; } = 6;

    [JsonPropertyName("VoteDurationSeconds")]
    public int VoteDurationSeconds { get; set; } = 30;

    [JsonPropertyName("HideMenuAfterVote")]
    public bool HideMenuAfterVote { get; set; } = true;

    [JsonPropertyName("ShowVoteCounts")]
    public bool ShowVoteCounts { get; set; } = true;
}

public class EndOfMapVoteConfig
{
    [JsonPropertyName("Enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("TriggerSecondsBeforeEnd")]
    public int TriggerSecondsBeforeEnd { get; set; } = 120;

    [JsonPropertyName("TriggerRoundsBeforeEnd")]
    public int TriggerRoundsBeforeEnd { get; set; } = 2;

    [JsonPropertyName("AllowExtend")]
    public bool AllowExtend { get; set; } = true;

    [JsonPropertyName("ExtendMinutes")]
    public int ExtendMinutes { get; set; } = 30;

    [JsonPropertyName("MaxExtends")]
    public int MaxExtends { get; set; } = 3;
}

public class MapChangeConfig
{
    [JsonPropertyName("DelaySeconds")]
    public float DelaySeconds { get; set; } = 3.0f;

    [JsonPropertyName("RetryTimeoutSeconds")]
    public int RetryTimeoutSeconds { get; set; } = 10;

    [JsonPropertyName("MaxRetries")]
    public int MaxRetries { get; set; } = 3;

    [JsonPropertyName("DelayToChangeInTheEnd")]
    public float DelayToChangeInTheEnd { get; set; } = 7.0f;
}

public class CommandSettings
{
    [JsonPropertyName("NextMapShowToAll")]
    public bool NextMapShowToAll { get; set; } = false;

    [JsonPropertyName("TimeLeftShowToAll")]
    public bool TimeLeftShowToAll { get; set; } = false;
}

public class MapChooserConfig : BasePluginConfig
{
    [JsonPropertyName("MapPool")]
    public MapPoolConfig MapPool { get; set; } = new();

    [JsonPropertyName("Vote")]
    public VoteSettings Vote { get; set; } = new();

    [JsonPropertyName("EndOfMapVote")]
    public EndOfMapVoteConfig EndOfMapVote { get; set; } = new();

    [JsonPropertyName("MapChange")]
    public MapChangeConfig MapChange { get; set; } = new();

    [JsonPropertyName("VoteWeights")]
    public Dictionary<string, float> VoteWeights { get; set; } = new()
    {
        ["#silver"] = 1.25f,
        ["#gold"] = 1.5f,
        ["#platinum"] = 2.0f,
        ["#royal"] = 2.5f
    };

    [JsonPropertyName("DefaultVoteWeight")]
    public float DefaultVoteWeight { get; set; } = 1.0f;

    [JsonPropertyName("Commands")]
    public CommandSettings Commands { get; set; } = new();
}

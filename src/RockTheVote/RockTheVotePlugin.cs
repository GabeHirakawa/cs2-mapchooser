using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Commands;
using MapChooser.Contracts;
using MapChooser.Contracts.Models;
using Microsoft.Extensions.Logging;
using RockTheVote.Config;
using static CounterStrikeSharp.API.Core.Listeners;

namespace RockTheVote;

public class RockTheVotePlugin : BasePlugin, IPluginConfig<RtvConfig>
{
    public override string ModuleName => "RockTheVote";
    public override string ModuleVersion => "1.0.0";
    public override string ModuleAuthor => "cs2-mapchooser";
    public override string ModuleDescription => "Rock the Vote - trigger map votes";

    public RtvConfig Config { get; set; } = new();

    private static readonly PluginCapability<IMapChooserApi> MapChooserCapability = new("mapchooser:api");

    private readonly RtvService _rtvService = new();

    public void OnConfigParsed(RtvConfig config)
    {
        Config = config;
    }

    public override void Load(bool hotReload)
    {
        RegisterListener<OnMapStart>(mapName =>
        {
            var gameRules = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules")
                .FirstOrDefault()?.GameRules;
            bool isWarmup = gameRules?.WarmupPeriod ?? false;
            _rtvService.OnMapStart(isWarmup);
        });

        RegisterEventHandler<EventRoundEnd>((@event, info) =>
        {
            _rtvService.OnRoundEnd();
            return HookResult.Continue;
        });

        RegisterEventHandler<EventWarmupEnd>((@event, info) =>
        {
            _rtvService.OnWarmupEnd();
            return HookResult.Continue;
        });

        RegisterEventHandler<EventPlayerDisconnect>((@event, info) =>
        {
            var player = @event.Userid;
            if (player is not null && player.IsValid && !player.IsBot)
                _rtvService.OnPlayerDisconnect(player.SteamID);
            return HookResult.Continue;
        });

        AddCommand("css_rtv", "Rock the vote", OnRtvCommand);

        Logger.LogInformation("RockTheVote loaded");
    }

    private void OnRtvCommand(CCSPlayerController? player, CommandInfo info)
    {
        if (player is null || !player.IsValid || player.IsBot)
            return;

        var api = MapChooserCapability.Get();
        var result = _rtvService.TryRtv(player, Config, api);

        switch (result)
        {
            case RtvResult.Added:
                var needed = _rtvService.GetRequiredVotes(Config.VotePercentage);
                var current = _rtvService.GetTotalWeightedVotes();
                Server.PrintToChatAll(
                    $" \x02[RTV]\x01 {Localizer["rtv.rocked-the-vote", player.PlayerName]} " +
                    $"{Localizer["general.votes-needed", $"{current:F0}", needed]}");
                break;

            case RtvResult.VotesReached:
                var voteConfig = new VoteConfig(
                    MapsToShow: Config.MapsToShow,
                    VoteDurationSeconds: Config.VoteDurationSeconds,
                    AllowExtend: false,
                    ChangeMapImmediately: Config.ChangeMapImmediately,
                    TriggerSource: "rtv"
                );
                var started = api?.StartVote(voteConfig) ?? false;
                if (started)
                {
                    Server.PrintToChatAll($" \x02[RTV]\x01 {Localizer["rtv.votes-reached"]}");
                }
                else
                {
                    _rtvService.ResetVotes();
                    Logger.LogWarning("RTV vote threshold reached but StartVote failed. Votes have been reset.");
                    player.PrintToChat($" \x02[RTV]\x01 {Localizer["rtv.disabled"]}");
                }
                break;

            case RtvResult.AlreadyVoted:
                player.PrintToChat($" \x02[RTV]\x01 {Localizer["rtv.already-rocked-the-vote"]}");
                break;

            case RtvResult.InWarmup:
                player.PrintToChat($" \x02[RTV]\x01 {Localizer["general.validation.warmup"]}");
                break;

            case RtvResult.MinPlayersNotMet:
                player.PrintToChat($" \x02[RTV]\x01 {Localizer["general.validation.minimum-players", Config.MinPlayers]}");
                break;

            case RtvResult.MinRoundsNotMet:
                player.PrintToChat($" \x02[RTV]\x01 {Localizer["general.validation.minimum-rounds", Config.MinRounds]}");
                break;

            case RtvResult.VoteInProgress:
            case RtvResult.MapChangeScheduled:
            case RtvResult.Disabled:
                player.PrintToChat($" \x02[RTV]\x01 {Localizer["rtv.disabled"]}");
                break;

            case RtvResult.MapChooserUnavailable:
                player.PrintToChat(" \x02[RTV]\x01 MapChooser plugin is not loaded.");
                Logger.LogWarning("MapChooser capability not available");
                break;
        }
    }
}

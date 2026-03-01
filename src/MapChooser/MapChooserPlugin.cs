using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using MapChooser.Commands;
using MapChooser.Config;
using MapChooser.Contracts;
using MapChooser.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using static CounterStrikeSharp.API.Core.Listeners;

namespace MapChooser;

public class MapChooserServiceCollection : IPluginServiceCollection<MapChooserPlugin>
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<GameRulesService>();
        services.AddSingleton<MapPoolService>();
        services.AddSingleton<CooldownService>();
        services.AddSingleton<NominationService>();
        services.AddSingleton<VoteWeightService>();
        services.AddSingleton<MapChangeService>();
        services.AddSingleton<VoteService>();
        services.AddSingleton<TimeTracker>();
        services.AddSingleton<RoundTracker>();
        services.AddSingleton<EndOfMapService>();
        services.AddSingleton<MapChooserApi>();
        services.AddSingleton<NextMapCommand>();
        services.AddSingleton<TimeLeftCommand>();
        services.AddSingleton<AdminCommands>();
    }
}

public class MapChooserPlugin : BasePlugin, IPluginConfig<MapChooserConfig>
{
    public override string ModuleName => "MapChooser";
    public override string ModuleVersion => "1.0.0";
    public override string ModuleAuthor => "cs2-mapchooser";
    public override string ModuleDescription => "Core map voting and selection engine";

    public MapChooserConfig Config { get; set; } = new();

    public static PluginCapability<IMapChooserApi> Capability { get; } = new("mapchooser:api");

    private readonly MapChooserApi _api;
    private readonly MapPoolService _mapPool;
    private readonly CooldownService _cooldown;
    private readonly NominationService _nominations;
    private readonly VoteWeightService _voteWeight;
    private readonly MapChangeService _mapChange;
    private readonly VoteService _voteService;
    private readonly GameRulesService _gameRules;
    private readonly TimeTracker _timeTracker;
    private readonly RoundTracker _roundTracker;
    private readonly EndOfMapService _endOfMap;
    private readonly NextMapCommand _nextMapCommand;
    private readonly TimeLeftCommand _timeLeftCommand;
    private readonly AdminCommands _adminCommands;

    public MapChooserPlugin(
        MapChooserApi api,
        MapPoolService mapPool,
        CooldownService cooldown,
        NominationService nominations,
        VoteWeightService voteWeight,
        MapChangeService mapChange,
        VoteService voteService,
        GameRulesService gameRules,
        TimeTracker timeTracker,
        RoundTracker roundTracker,
        EndOfMapService endOfMap,
        NextMapCommand nextMapCommand,
        TimeLeftCommand timeLeftCommand,
        AdminCommands adminCommands)
    {
        _api = api;
        _mapPool = mapPool;
        _cooldown = cooldown;
        _nominations = nominations;
        _voteWeight = voteWeight;
        _mapChange = mapChange;
        _voteService = voteService;
        _gameRules = gameRules;
        _timeTracker = timeTracker;
        _roundTracker = roundTracker;
        _endOfMap = endOfMap;
        _nextMapCommand = nextMapCommand;
        _timeLeftCommand = timeLeftCommand;
        _adminCommands = adminCommands;
    }

    public void OnConfigParsed(MapChooserConfig config)
    {
        Config = config;

        _mapPool.LoadMaps(ModuleDirectory, config.MapPool);
        _cooldown.Initialize(ModuleDirectory, config.MapPool);
        _nominations.Configure(config.MaxNominationsPerPlayer);
        _voteWeight.Configure(config.VoteWeights, config.DefaultVoteWeight);
        _mapChange.Initialize(this, config.MapChange);
        _voteService.Initialize(this, config);
        _endOfMap.Initialize(this, config);
    }

    public override void Load(bool hotReload)
    {
        Capabilities.RegisterPluginCapability(Capability, () => _api);

        RegisterListener<OnMapStart>(OnMapStart);
        RegisterListener<OnMapEnd>(OnMapEnd);
        RegisterListener<OnTick>(OnTick);

        RegisterEventHandler<EventPlayerDisconnect>(OnPlayerDisconnect);
        RegisterEventHandler<EventRoundEnd>(OnRoundEnd);
        RegisterEventHandler<EventCsWinPanelMatch>(OnMatchEnd);

        // Register commands
        AddCommand("css_nextmap", "Show next map", (player, info) =>
        {
            _nextMapCommand.Handle(player, Config.Commands, this);
        });

        AddCommand("css_timeleft", "Show time remaining", (player, info) =>
        {
            _timeLeftCommand.Handle(player, Config.Commands, this);
        });

        AddCommand("css_forcertv", "Force a map vote", (player, info) =>
        {
            if (player is not null && !CounterStrikeSharp.API.Modules.Admin.AdminManager.PlayerHasPermissions(player, "@css/changemap"))
            {
                player.PrintToChat(" \x02[MapChooser]\x01 You don't have permission to use this command.");
                return;
            }
            _adminCommands.ForceRtv(player, Config.Vote.MapsToShow, Config.Vote.VoteDurationSeconds);
        });

        AddCommand("css_setnextmap", "Set the next map", (player, info) =>
        {
            if (player is not null && !CounterStrikeSharp.API.Modules.Admin.AdminManager.PlayerHasPermissions(player, "@css/changemap"))
            {
                player.PrintToChat(" \x02[MapChooser]\x01 You don't have permission to use this command.");
                return;
            }
            if (info.ArgCount < 2)
            {
                info.ReplyToCommand("Usage: css_setnextmap <mapname>");
                return;
            }
            _adminCommands.SetNextMap(player, info.GetArg(1));
        });

        AddCommand("css_changemap", "Immediately change map", (player, info) =>
        {
            if (player is not null && !CounterStrikeSharp.API.Modules.Admin.AdminManager.PlayerHasPermissions(player, "@css/changemap"))
            {
                player.PrintToChat(" \x02[MapChooser]\x01 You don't have permission to use this command.");
                return;
            }
            if (info.ArgCount < 2)
            {
                info.ReplyToCommand("Usage: css_changemap <mapname>");
                return;
            }
            _adminCommands.ChangeMap(player, info.GetArg(1));
        });

        Logger.LogInformation("MapChooser loaded");

        if (hotReload)
        {
            _gameRules.RefreshGameRules();
        }
    }

    private void OnMapStart(string mapName)
    {
        _voteService.Reset();
        _mapChange.Reset();
        _nominations.Reset();
        _roundTracker.Reset();
        _endOfMap.Reset();
        _gameRules.RefreshGameRules();
        _timeTracker.Initialize();
        _roundTracker.Initialize();
        _cooldown.RecordMapPlayed(mapName);
        _mapPool.LoadMaps(ModuleDirectory, Config.MapPool);

        Logger.LogInformation("Map started: {Map}", mapName);
    }

    private void OnMapEnd()
    {
        _voteService.Reset();
        _mapChange.Reset();
    }

    private void OnTick()
    {
        _endOfMap.CheckTimeBasedTrigger();
    }

    private HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player is null || !player.IsValid || player.IsBot || player.IsHLTV)
            return HookResult.Continue;

        var steamId = player.SteamID;
        _nominations.RemoveNomination(steamId);
        _voteService.RemovePlayerVotes(steamId);

        return HookResult.Continue;
    }

    private HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info)
    {
        _roundTracker.OnRoundEnd(@event.Winner);
        _gameRules.RefreshGameRules();
        _endOfMap.CheckRoundBasedTrigger();

        return HookResult.Continue;
    }

    private HookResult OnMatchEnd(EventCsWinPanelMatch @event, GameEventInfo info)
    {
        _mapChange.TriggerEndOfMapChange();
        return HookResult.Continue;
    }
}

using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Menu;
using MapChooser.Contracts;
using MapChooser.Contracts.Models;
using Microsoft.Extensions.Logging;
using Nominations.Config;

namespace Nominations;

public class NominationsPlugin : BasePlugin, IPluginConfig<NominationsConfig>
{
    public override string ModuleName => "Nominations";
    public override string ModuleVersion => "1.0.0";
    public override string ModuleAuthor => "cs2-mapchooser";
    public override string ModuleDescription => "Map nomination system";

    public NominationsConfig Config { get; set; } = new();

    private static readonly PluginCapability<IMapChooserApi> MapChooserCapability = new("mapchooser:api");

    public void OnConfigParsed(NominationsConfig config)
    {
        Config = config;
    }

    public override void Load(bool hotReload)
    {
        AddCommand("css_nominate", "Nominate a map", OnNominateCommand);

        Logger.LogInformation("Nominations loaded");
    }

    private void OnNominateCommand(CCSPlayerController? player, CommandInfo info)
    {
        if (player is null || !player.IsValid || player.IsBot)
            return;

        var api = MapChooserCapability.Get();
        if (api is null)
        {
            player.PrintToChat(" \x02[Nominate]\x01 MapChooser plugin is not loaded.");
            return;
        }

        if (info.ArgCount >= 2)
        {
            var mapName = info.GetArg(1);
            NominateDirect(player, api, mapName);
            return;
        }

        OpenNominationMenu(player, api);
    }

    private void NominateDirect(CCSPlayerController player, IMapChooserApi api, string mapName)
    {
        var available = api.GetAvailableMaps();
        var map = available.FirstOrDefault(m =>
            m.Name.Contains(mapName, StringComparison.OrdinalIgnoreCase));

        if (map is null)
        {
            var allMap = api.GetAllMaps().FirstOrDefault(m =>
                m.Name.Contains(mapName, StringComparison.OrdinalIgnoreCase));

            if (allMap is not null && api.IsMapOnCooldown(allMap.Name))
            {
                player.PrintToChat($" \x02[Nominate]\x01 {Localizer["general.validation.map-played-recently"]}");
                return;
            }

            player.PrintToChat($" \x02[Nominate]\x01 {Localizer["general.invalid-map"]}");
            return;
        }

        SubmitNomination(player, api, map);
    }

    private void OpenNominationMenu(CCSPlayerController player, IMapChooserApi api)
    {
        var available = api.GetAvailableMaps();
        if (available.Count == 0)
        {
            player.PrintToChat(" \x02[Nominate]\x01 No maps available for nomination.");
            return;
        }

        var menu = new CenterHtmlMenu(Localizer["nominate.menu-title"], this);
        menu.PostSelectAction = PostSelectAction.Close;

        foreach (var map in available.OrderBy(m => m.Name))
        {
            var nominationCount = api.GetNominations()
                .Where(kvp => kvp.Key.Name.Equals(map.Name, StringComparison.OrdinalIgnoreCase))
                .Select(kvp => kvp.Value.Count)
                .FirstOrDefault();

            var displayText = nominationCount > 0
                ? $"{map.GetDisplayName()} ({nominationCount})"
                : map.GetDisplayName();

            menu.AddMenuOption(displayText, (p, option) =>
            {
                SubmitNomination(p, api, map);
            });
        }

        MenuManager.OpenCenterHtmlMenu(this, player, menu);
    }

    private void SubmitNomination(CCSPlayerController player, IMapChooserApi api, Map map)
    {
        var result = api.Nominate(player, map);

        switch (result)
        {
            case NominationResult.Success:
                var count = api.GetNominations()
                    .Where(kvp => kvp.Key.Name.Equals(map.Name, StringComparison.OrdinalIgnoreCase))
                    .Select(kvp => kvp.Value.Count)
                    .FirstOrDefault();
                Server.PrintToChatAll(
                    $" \x02[Nominate]\x01 {Localizer["nominate.nominated", player.PlayerName, map.GetDisplayName(), count]}");
                break;

            case NominationResult.AlreadyNominated:
                var existingCount = api.GetNominations()
                    .Where(kvp => kvp.Key.Name.Equals(map.Name, StringComparison.OrdinalIgnoreCase))
                    .Select(kvp => kvp.Value.Count)
                    .FirstOrDefault();
                player.PrintToChat(
                    $" \x02[Nominate]\x01 {Localizer["nominate.already-nominated", map.GetDisplayName(), existingCount]}");
                break;

            case NominationResult.MapOnCooldown:
                player.PrintToChat($" \x02[Nominate]\x01 {Localizer["general.validation.map-played-recently"]}");
                break;

            case NominationResult.MapNotInPool:
                player.PrintToChat($" \x02[Nominate]\x01 {Localizer["general.invalid-map"]}");
                break;

            case NominationResult.IsCurrentMap:
                player.PrintToChat($" \x02[Nominate]\x01 {Localizer["general.validation.current-map"]}");
                break;

            case NominationResult.VoteInProgress:
                player.PrintToChat($" \x02[Nominate]\x01 {Localizer["general.validation.disabled"]}");
                break;

            case NominationResult.MaxNominationsReached:
                player.PrintToChat(" \x02[Nominate]\x01 Maximum nominations reached.");
                break;
        }
    }
}

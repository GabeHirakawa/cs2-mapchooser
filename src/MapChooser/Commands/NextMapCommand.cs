using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using MapChooser.Config;
using MapChooser.Services;

namespace MapChooser.Commands;

public class NextMapCommand
{
    private readonly VoteService _voteService;

    public NextMapCommand(VoteService voteService)
    {
        _voteService = voteService;
    }

    public void Handle(CCSPlayerController? player, CommandSettings settings, BasePlugin plugin)
    {
        var nextMap = _voteService.NextMap;
        string message;

        if (nextMap is not null)
        {
            message = $" \x02[MapChooser]\x01 {plugin.Localizer["nextmap", nextMap.GetDisplayName()]}";
        }
        else
        {
            message = $" \x02[MapChooser]\x01 {plugin.Localizer["nextmap.decided-by-vote"]}";
        }

        if (settings.NextMapShowToAll || player is null)
        {
            Server.PrintToChatAll(message);
        }
        else
        {
            player.PrintToChat(message);
        }
    }
}

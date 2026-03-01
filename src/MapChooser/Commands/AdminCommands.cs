using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using MapChooser.Contracts.Models;
using MapChooser.Services;

namespace MapChooser.Commands;

public class AdminCommands
{
    private readonly VoteService _voteService;
    private readonly MapPoolService _mapPool;
    private readonly MapChangeService _mapChange;

    public AdminCommands(VoteService voteService, MapPoolService mapPool, MapChangeService mapChange)
    {
        _voteService = voteService;
        _mapPool = mapPool;
        _mapChange = mapChange;
    }

    public void ForceRtv(CCSPlayerController? player, int mapsToShow, int voteDuration)
    {
        if (_voteService.IsVoteInProgress)
        {
            player?.PrintToChat(" \x02[MapChooser]\x01 A vote is already in progress.");
            return;
        }

        Server.PrintToChatAll(" \x02[MapChooser]\x01 Admin forced a map vote!");

        var config = new VoteConfig(
            MapsToShow: mapsToShow,
            VoteDurationSeconds: voteDuration,
            AllowExtend: false,
            ChangeMapImmediately: true,
            TriggerSource: "admin"
        );

        _voteService.StartVote(config);
    }

    public void SetNextMap(CCSPlayerController? player, string mapName)
    {
        var map = _mapPool.FindMapPartial(mapName);
        if (map is null)
        {
            player?.PrintToChat($" \x02[MapChooser]\x01 Map '{mapName}' not found in map pool.");
            return;
        }

        _voteService.SetNextMap(map);
        Server.PrintToChatAll($" \x02[MapChooser]\x01 Admin set next map to \x04{map.GetDisplayName()}\x01.");
    }

    public void ChangeMap(CCSPlayerController? player, string mapName)
    {
        var map = _mapPool.FindMapPartial(mapName);
        if (map is null)
        {
            map = new Map(mapName);
        }

        Server.PrintToChatAll($" \x02[MapChooser]\x01 Admin is changing map to \x04{map.GetDisplayName()}\x01...");
        _mapChange.ScheduleMapChange(map);
    }
}

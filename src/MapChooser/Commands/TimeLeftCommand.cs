using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using MapChooser.Config;
using MapChooser.Services;

namespace MapChooser.Commands;

public class TimeLeftCommand
{
    private readonly TimeTracker _timeTracker;
    private readonly RoundTracker _roundTracker;

    public TimeLeftCommand(TimeTracker timeTracker, RoundTracker roundTracker)
    {
        _timeTracker = timeTracker;
        _roundTracker = roundTracker;
    }

    public void Handle(CCSPlayerController? player, CommandSettings settings, BasePlugin plugin)
    {
        string message;

        var roundsRemaining = _roundTracker.GetRoundsRemaining();
        if (roundsRemaining is not null)
        {
            if (roundsRemaining.Value <= 0)
            {
                message = $" \x02[Timeleft]\x01 {plugin.Localizer["timeleft.last-round"]}";
            }
            else
            {
                message = $" \x02[Timeleft]\x01 {plugin.Localizer["timeleft.remaining-rounds", roundsRemaining.Value]}";
            }
        }
        else
        {
            var remaining = _timeTracker.GetTimeRemaining();
            if (remaining is null)
            {
                message = $" \x02[Timeleft]\x01 {plugin.Localizer["timeleft.no-time-limit"]}";
            }
            else if (remaining.Value <= 0)
            {
                message = $" \x02[Timeleft]\x01 {plugin.Localizer["timeleft.time-over"]}";
            }
            else
            {
                var ts = TimeSpan.FromSeconds(remaining.Value);
                if (ts.Hours > 0)
                {
                    message = $" \x02[Timeleft]\x01 {plugin.Localizer["timeleft.remaining-time-hour", ts.Hours, ts.Minutes, ts.Seconds]}";
                }
                else if (ts.Minutes > 0)
                {
                    message = $" \x02[Timeleft]\x01 {plugin.Localizer["timeleft.remaining-time-minute", ts.Minutes, ts.Seconds]}";
                }
                else
                {
                    message = $" \x02[Timeleft]\x01 {plugin.Localizer["timeleft.remaining-time-second", ts.Seconds]}";
                }
            }
        }

        if (settings.TimeLeftShowToAll || player is null)
        {
            Server.PrintToChatAll(message);
        }
        else
        {
            player.PrintToChat(message);
        }
    }
}

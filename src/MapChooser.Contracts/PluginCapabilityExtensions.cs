using CounterStrikeSharp.API.Core.Capabilities;

namespace MapChooser.Contracts;

public static class PluginCapabilityExtensions
{
    public static T? TryGet<T>(this PluginCapability<T> capability) where T : class
    {
        try
        {
            return capability.Get();
        }
        catch (KeyNotFoundException)
        {
            return null;
        }
    }
}

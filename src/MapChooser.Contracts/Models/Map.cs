namespace MapChooser.Contracts.Models;

public record Map(string Name, string? WorkshopId = null, string? DisplayName = null)
{
    public string GetDisplayName() => DisplayName ?? Name;
}

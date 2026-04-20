namespace VortexModLists.Models;

public sealed class ModEntry
{
    public string Game { get; init; } = string.Empty;
    public string ModName { get; init; } = string.Empty;
    public string Id { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public string Homepage { get; init; } = string.Empty;
    public bool IsActive { get; init; }
}

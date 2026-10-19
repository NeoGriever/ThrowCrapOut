using Dalamud.Configuration;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace ThrowCrapOut;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 3;
    public int DelayMilliseconds { get; set; } = 500;
    public Language Language { get; set; } = Language.English;
    public bool ChatInfos { get; set; } = true;
    public HashSet<InventoryType> DisabledCategories { get; set; } = [];

    public bool IsEnabled(InventoryType type) => !DisabledCategories.Contains(type);
    public int EffectiveDelay => (Math.Clamp(DelayMilliseconds, 500, 5000) + 2) / 5 * 5;
}

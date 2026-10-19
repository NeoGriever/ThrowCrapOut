using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using Lumina.Excel.Sheets;

namespace ThrowCrapOut;

internal sealed record ItemSnapshot(int Slot, uint ItemId, string Name, bool? InGearset, string Fingerprint = "", string ClassTags = "", int? ItemLevel = null);

internal sealed record ContainerSnapshot(InventoryType Type, string Name, int Capacity, string Status, ItemSnapshot[] Items, int[]? DisplaySlots = null)
{
    public ItemSnapshot?[] Slots { get; } = BuildSlots(Capacity, Items);

    private static ItemSnapshot?[] BuildSlots(int capacity, ItemSnapshot[] items)
    {
        var slots = new ItemSnapshot?[capacity];
        foreach (var item in items) slots[item.Slot] = item;
        return slots;
    }
}

internal sealed record ArmourySnapshot(DateTimeOffset? CapturedAt, string Status, ContainerSnapshot[] Containers)
{
    public static ArmourySnapshot Empty(string status) => new(null, status, []);
}

internal sealed partial class ArmouryScanner(IClientState clientState, IPlayerState playerState, IDataManager data, IPluginLog log, Configuration config, IChatGui chat)
{
    private static readonly (InventoryType Type, string Name)[] Containers =
    [
        (InventoryType.ArmoryMainHand, "Haupthand"),
        (InventoryType.ArmoryOffHand, "Nebenhand"),
        (InventoryType.ArmoryHead, "Kopf"),
        (InventoryType.ArmoryBody, "Rumpf"),
        (InventoryType.ArmoryHands, "Hände"),
        (InventoryType.ArmoryLegs, "Beine"),
        (InventoryType.ArmoryFeets, "Füße"),
        (InventoryType.ArmoryEar, "Ohrringe"),
        (InventoryType.ArmoryNeck, "Hals"),
        (InventoryType.ArmoryWrist, "Handgelenke"),
        (InventoryType.ArmoryRings, "Ringe"),
        (InventoryType.ArmorySoulCrystal, "Jobkristalle"),
    ];

    private bool refreshRequested;
    private long nextRefresh;
    private ulong characterId;
    private bool scanFailed;

    public ArmourySnapshot Snapshot { get; private set; } = ArmourySnapshot.Empty("");

    private string T(string key, params object[] args) => Text.Get(config.Language, key, args);

    public void RequestRefresh() => refreshRequested = true;

    public void Update(bool visible)
    {
        if (!clientState.IsLoggedIn || !playerState.IsLoaded || playerState.ContentId == 0)
        {
            Snapshot = ArmourySnapshot.Empty(T("LoggedOut"));
            characterId = 0;
            CancelMove();
            CancelActionForLogout();
            return;
        }

        if (characterId != playerState.ContentId)
        {
            characterId = playerState.ContentId;
            Snapshot = ArmourySnapshot.Empty(T("CharacterChanged"));
            refreshRequested = true;
            CancelMove();
            if (actionCharacterId != 0) CancelActionForLogout();
        }

        UpdateMove();
        UpdateAction();

        if (!visible || (!refreshRequested && Environment.TickCount64 < nextRefresh)) return;

        refreshRequested = false;
        nextRefresh = Environment.TickCount64 + 1000;
        try
        {
            Snapshot = Scan();
            scanFailed = false;
        }
        catch (Exception ex)
        {
            Snapshot = ArmourySnapshot.Empty(T("ScanUnavailable"));
            if (!scanFailed) log.Error(ex, "Arsenal-Analyse fehlgeschlagen");
            scanFailed = true;
        }
    }

    private unsafe ArmourySnapshot Scan()
    {
        var inventory = InventoryManager.Instance();
        if (inventory == null) return ArmourySnapshot.Empty(T("InventoryUnavailable"));

        var gearsets = RaptureGearsetModule.Instance();
        var result = new List<ContainerSnapshot>();
        var sheet = data.GetExcelSheet<Item>(DataLanguage);
        foreach (var (type, _) in Containers)
        {
            var name = T(type.ToString());
            var container = inventory->GetInventoryContainer(type);
            if (container == null || !container->IsLoaded)
            {
                result.Add(new(type, name, 0, "NotLoaded", []));
                continue;
            }

            if (container->Size < 0 || container->Size > 200 || (container->Size > 0 && container->Items == null))
            {
                result.Add(new(type, name, 0, "InvalidData", []));
                continue;
            }

            var items = new List<ItemSnapshot>();
            for (var slot = 0; slot < container->Size; slot++)
            {
                var item = container->GetInventorySlot(slot);
                if (item == null)
                {
                    items.Add(new(slot, 0, T("UnreadableSlot"), null));
                    continue;
                }

                if (item->IsSymbolic)
                {
                    items.Add(new(slot, 0, T("LinkedItem"), null));
                    continue;
                }

                if (item->ItemId == 0) continue;

                var row = sheet.GetRowOrDefault(item->ItemId);
                var itemName = row?.Name.ToString() ?? T("UnknownItem", item->ItemId);
                bool? registered = gearsets == null || row == null ? null : gearsets->IsItemRegisteredToGearset(item);
                items.Add(new(slot, item->ItemId, itemName, registered, Fingerprint(item), row is { } gear ? GetClassTags(gear.ClassJobCategory.Value) : T("Unknown"), row is { } levelRow ? checked((int)levelRow.LevelItem.RowId) : null));
            }

            result.Add(new(type, name, container->Size, "Loaded", items.ToArray(), ReadArmouryOrder(type, container->Size)));
        }

        return new(DateTimeOffset.Now, gearsets == null ? T("GearsetUnavailable") : "", result.ToArray());
    }
}

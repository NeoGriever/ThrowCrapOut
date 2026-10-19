using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace ThrowCrapOut;

internal sealed partial class ArmouryScanner
{
    private static readonly InventoryType[] DestinationBags =
    [
        InventoryType.Inventory1, InventoryType.Inventory2,
        InventoryType.Inventory3, InventoryType.Inventory4,
    ];

    private sealed record MoveRequest(ulong CharacterId, InventoryType Source, ItemSnapshot Item);
    private sealed record MoveInFlight(MoveRequest Request, InventoryType Destination, int Slot,
        int VisibleIndex, int ItemsPerPage, long Deadline, int NativeResult)
    {
        public MoveConfirmation Confirmation { get; } = new(Deadline);
    }

    private MoveRequest? requestedMove;
    private MoveInFlight? inFlightMove;
    private bool lastMoveSucceeded;
    private long nextOperationAt;
    private long moveHighlightAt;
    private long moveExecuteAt;
    public bool MoveBusy => requestedMove != null || inFlightMove != null;
    public string MoveStatus { get; private set; } = "";

    public void RequestMove(InventoryType source, ItemSnapshot item)
    {
        if (Busy || !config.IsEnabled(source) || item.InGearset != false || string.IsNullOrEmpty(item.Fingerprint))
            return;
        QueueMove(source, item);
    }

    private void QueueMove(InventoryType source, ItemSnapshot item, bool prepared = false)
    {
        if (!ActionBusy)
        {
            ActionStatus = "";
            Status = OperationStatus.Working;
            completedSteps = 0;
            totalSteps = 1;
        }
        lastMoveSucceeded = false;
        var now = Environment.TickCount64;
        var delay = config.EffectiveDelay;
        moveHighlightAt = prepared ? now : now + delay * 30 / 100;
        moveExecuteAt = Math.Max(nextOperationAt, prepared ? now + delay * 70 / 100 : now + delay);
        requestedMove = new(characterId, source, item);
        MoveStatus = T("Working", T("Clear"), 1, 1);
    }

    private void CancelMove()
    {
        if (MoveBusy)
        {
            MoveStatus = T("LoggedOut");
            if (!ActionBusy) Status = OperationStatus.Failed;
        }
        requestedMove = null;
        inFlightMove = null;
    }

    private void FinishMove(string message, bool success = false)
    {
        nextOperationAt = Environment.TickCount64 + config.EffectiveDelay;
        lastMoveSucceeded = success;
        MoveStatus = success ? "1/1" : message;
        if (!ActionBusy)
        {
            Status = success ? OperationStatus.Complete : OperationStatus.Failed;
            completedSteps = success ? 1 : 0;
        }
        requestedMove = null;
        inFlightMove = null;
        RequestRefresh();
    }

    private unsafe void UpdateMove()
    {
        if (!MoveBusy)
            return;
        try
        {
            var inventory = InventoryManager.Instance();
            if (inventory == null)
            {
                FinishMove(T("InventoryUnavailable"));
                return;
            }

            if (inFlightMove is { } pending)
            {
                var source = ReadSlot(inventory, pending.Request.Source, pending.Request.Item.Slot);
                var destination = ReadSlot(inventory, pending.Destination, pending.Slot);
                var matches = source != null && !source->IsSymbolic && source->ItemId == 0 &&
                    destination != null && !destination->IsSymbolic && Fingerprint(destination) == pending.Request.Item.Fingerprint &&
                    FindVisibleIndex(pending.Destination, pending.Slot) == pending.VisibleIndex;
                if (ValidateMove(inventory, pending.Confirmation, pending.Request.Source, pending.Request.Item.Slot,
                    pending.Destination, pending.Slot, matches))
                {
                    if (config.ChatInfos) chat.Print("[TCO] " + FormatMoved(pending.Request.Item));
                    FinishMove("", true);
                }
                else if (pending.Confirmation.IsExpired(Environment.TickCount64))
                    FinishMove(T("MoveFailed"));
                return;
            }

            if (Environment.TickCount64 < moveExecuteAt) return;
            var request = requestedMove!;
            if (!config.IsEnabled(request.Source))
            {
                FinishMove(T("Disabled"));
                return;
            }
            if (request.CharacterId != playerState.ContentId || !Containers.Any(c => c.Type == request.Source))
            {
                FinishMove(T("Stale"));
                return;
            }

            foreach (var operation in inventory->PendingOperations)
            {
                if (!operation.IsEmpty)
                {
                    FinishMove(T("InventoryBusy"));
                    return;
                }
            }

            var item = ReadSlot(inventory, request.Source, request.Item.Slot);
            if (item == null || item->IsSymbolic || item->ItemId == 0 || Fingerprint(item) != request.Item.Fingerprint)
            {
                FinishMove(T("Stale"));
                return;
            }

            var gearsets = RaptureGearsetModule.Instance();
            if (gearsets == null || gearsets->IsItemRegisteredToGearset(item))
            {
                FinishMove(T("GearsetProtected"));
                return;
            }

            var order = ItemOrderModule.Instance();
            var sorter = order == null ? null : order->InventorySorter;
            if (sorter == null || sorter->InventoryType != InventoryType.Inventory1 || sorter->ItemsPerPage <= 0 || sorter->ItemsPerPage > 200 || sorter->SortFunctionIndex != -1)
            {
                FinishMove(T("OrderUnavailable"));
                return;
            }

            var capacities = new int[DestinationBags.Length];
            for (var page = 0; page < DestinationBags.Length; page++)
            {
                var bag = inventory->GetInventoryContainer(DestinationBags[page]);
                if (!IsReadable(bag))
                {
                    FinishMove(T("InventoryUnavailable"));
                    return;
                }
                capacities[page] = bag->Size;
            }
            var count = capacities.Sum();
            if (sorter->Items.LongCount != count || count != sorter->ItemsPerPage * DestinationBags.Length)
            {
                FinishMove(T("OrderUnavailable"));
                return;
            }

            var positions = new (InventoryType Type, int Slot)[count];
            var seen = new HashSet<(ushort Page, ushort Slot)>();
            for (var index = 0; index < count; index++)
            {
                var entry = sorter->Items[index].Value;
                if (entry == null || entry->Page >= DestinationBags.Length || entry->Slot >= capacities[entry->Page] || !seen.Add((entry->Page, entry->Slot)))
                {
                    FinishMove(T("InvalidData"));
                    return;
                }
                positions[index] = (DestinationBags[entry->Page], entry->Slot);
            }

            for (var index = positions.Length - 1; index >= 0; index--)
            {
                var (type, slot) = positions[index];
                var destination = ReadSlot(inventory, type, slot);
                if (destination == null || destination->IsSymbolic)
                {
                    FinishMove(T("UnreadableSlot"));
                    return;
                }
                if (destination->ItemId != 0)
                    continue;

                var deadline = Environment.TickCount64 + MoveConfirmation.TimeoutMilliseconds;
                var result = inventory->MoveItemSlot(request.Source, (ushort)request.Item.Slot, type, (ushort)slot, true);
                requestedMove = null;
                inFlightMove = new(request, type, slot, index, sorter->ItemsPerPage, deadline, result);
                ValidateMove(inventory, inFlightMove.Confirmation, request.Source, request.Item.Slot, type, slot, false);
                MoveStatus = T("Working", T("Clear"), 1, 1);
                return;
            }
            FinishMove(T("InventoryFull"));
        }
        catch (Exception ex)
        {
            log.Error(ex, "Movement error");
            FinishMove(T("MoveFailed"));
        }
    }

    private static unsafe bool ValidateMove(InventoryManager* inventory, MoveConfirmation confirmation,
        InventoryType source, int sourceSlot, InventoryType destination, int destinationSlot, bool slotsMatch)
    {
        var observed = false;
        var pending = false;
        foreach (var operation in inventory->PendingOperations)
        {
            if (operation.IsEmpty) continue;
            pending = true;
            if (operation.SourceInventoryType == source && operation.SourceInventorySlot == sourceSlot &&
                operation.DestinationInventoryType == destination && operation.DestinationInventorySlot == destinationSlot)
                observed = true;
        }
        return confirmation.Update(Environment.TickCount64, observed, pending, slotsMatch);
    }

    private string FormatMoved(ItemSnapshot item)
    {
        var row = data.GetExcelSheet<Lumina.Excel.Sheets.Item>(DataLanguage).GetRowOrDefault(item.ItemId);
        var name = row?.Name.ToString() ?? item.Name;
        var tags = row is { } gear ? GetClassTags(gear.ClassJobCategory.Value) : item.ClassTags;
        int? itemLevel = row is { } levelRow
            ? checked((int)levelRow.LevelItem.RowId)
            : item.ItemLevel;

        return T(
            "Moved",
            name,
            tags,
            itemLevel?.ToString() ?? "?"
        );
    }

    private static unsafe int FindVisibleIndex(InventoryType type, int slot)
    {
        var order = ItemOrderModule.Instance();
        var sorter = order == null ? null : order->InventorySorter;
        if (sorter == null || sorter->Items.LongCount > 800 || sorter->SortFunctionIndex != -1)
            return -1;
        for (var index = 0; index < sorter->Items.LongCount; index++)
        {
            var entry = sorter->Items[index].Value;
            if (entry != null && entry->Page < DestinationBags.Length &&
                DestinationBags[entry->Page] == type && entry->Slot == slot)
                return index;
        }
        return -1;
    }

    private static unsafe bool IsReadable(InventoryContainer* container) =>
        container != null && container->IsLoaded && container->Items != null &&
        container->Size > 0 && container->Size <= 200;

    private static unsafe InventoryItem* ReadSlot(InventoryManager* inventory, InventoryType type, int slot)
    {
        var container = inventory->GetInventoryContainer(type);
        return IsReadable(container) && slot >= 0 && slot < container->Size
            ? container->GetInventorySlot(slot)
            : null;
    }

    private static unsafe string Fingerprint(InventoryItem* item) =>
        $"{item->ItemId}:{item->Quantity}:{(byte)item->Flags}:{item->Condition}:" +
        $"{item->SpiritbondOrCollectability}:{item->CrafterContentId}:{item->GlamourId}:" +
        $"{string.Join(',', item->Stains.ToArray())}:" +
        $"{string.Join(',', item->Materia.ToArray())}:{string.Join(',', item->MateriaGrades.ToArray())}";
}

using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace ThrowCrapOut;

internal enum OperationStatus { Idle, Working, Failed, Complete }

internal sealed partial class ArmouryScanner
{
    private sealed record ActionStep(InventoryType Type, int Capacity, SlotSwap? Swap, ItemSnapshot? ClearItem);
    private sealed record SwapInFlight(ActionStep Step, int LeftSlot, int RightSlot, long Deadline)
    {
        public MoveConfirmation Confirmation { get; } = new(Deadline);
    }
    private readonly Queue<ActionStep> actionSteps = new();
    private readonly HashSet<(InventoryType Type, int Slot)> pendingSlots = new();
    private ArsenalAction? activeAction;
    private bool actionNeedsScan;
    private bool waitingForClear;
    private bool stopping;
    private SwapInFlight? swapInFlight;
    private SwapInFlight? preparedSwap;
    private long nextActionAt;
    private long inventoryBusySince;
    private int completedSteps;
    private int totalSteps;
    private ulong actionCharacterId;
    public bool ActionBusy => activeAction != null;
    public bool Busy => MoveBusy || ActionBusy;
    public OperationStatus Status { get; private set; }
    public float Progress => totalSteps > 0 ? Math.Clamp((float)completedSteps / totalSteps, 0, 1)
        : Status == OperationStatus.Complete ? 1 : 0;
    public string ActionStatus { get; private set; } = "";

    public bool IsActiveSlot(InventoryType type, int slot) =>
        requestedMove is { } request && Environment.TickCount64 >= moveHighlightAt && request.Source == type && request.Item.Slot == slot ||
        inFlightMove is { } move && move.Request.Source == type && move.Request.Item.Slot == slot ||
        preparedSwap is { } preview && preview.Step.Type == type && (preview.LeftSlot == slot || preview.RightSlot == slot) ||
        swapInFlight is { } swap && swap.Step.Type == type && (swap.LeftSlot == slot || swap.RightSlot == slot);

    public bool IsPendingSlot(InventoryType type, int slot) => config.IsEnabled(type) &&
        (pendingSlots.Contains((type, slot)) ||
         requestedMove is { } request && request.Source == type && request.Item.Slot == slot);

    private void RefreshPendingSlots()
    {
        pendingSlots.Clear();
        foreach (var step in actionSteps)
        {
            if (!config.IsEnabled(step.Type)) continue;
            if (step.ClearItem is { } item)
                pendingSlots.Add((step.Type, item.Slot));
            else if (step.Swap is { } swap &&
                Snapshot.Containers.FirstOrDefault(c => c.Type == step.Type)?.DisplaySlots is { } order)
            {
                pendingSlots.Add((step.Type, order[swap.Left]));
                pendingSlots.Add((step.Type, order[swap.Right]));
            }
        }
    }

    public void RequestAction(ArsenalAction action)
    {
        if (Busy) return;
        Status = OperationStatus.Working;
        activeAction = action;
        actionNeedsScan = true;
        actionCharacterId = 0;
        completedSteps = totalSteps = 0;
        nextActionAt = inventoryBusySince = 0;
        stopping = false;
        MoveStatus = "";
        ActionStatus = T("Scanning", T(action.ToString()));
    }

    public void StopAction()
    {
        if (!ActionBusy) return;
        stopping = true;
        preparedSwap = null;
        if (waitingForClear && requestedMove != null)
        {
            requestedMove = null;
            waitingForClear = false;
        }
        actionSteps.Clear();
        pendingSlots.Clear();
        ActionStatus = T("Stopping");
    }

    private void CancelActionForLogout()
    {
        if (ActionBusy) FinishAction(T("LoggedOut"));
    }

    private void FinishAction(string reason, bool success = false)
    {
        Status = success ? OperationStatus.Complete : OperationStatus.Failed;
        ActionStatus = success ? $"{completedSteps}/{totalSteps}"
            : T("ActionResult", T(activeAction?.ToString() ?? "Sort"), reason, completedSteps, totalSteps);

        activeAction = null;
        actionCharacterId = 0;
        actionSteps.Clear();
        pendingSlots.Clear();
        swapInFlight = preparedSwap = null;
        waitingForClear = actionNeedsScan = false;
        RequestRefresh();
    }

    private void StepCompleted()
    {
        completedSteps++;
        RefreshPendingSlots();
        ScheduleActionStep();
        ActionStatus = T("Progress", T(activeAction?.ToString() ?? "Sort"), completedSteps, totalSteps);
        RequestRefresh();
    }

    private void ScheduleActionStep()
    {
        var start = Environment.TickCount64;
        var delay = config.EffectiveDelay;
        nextActionAt = start + delay * 30 / 100;
        nextOperationAt = Math.Max(nextOperationAt, start + delay);
    }

    private unsafe void UpdateAction()
    {
        if (!ActionBusy) return;
        try
        {
            if (actionNeedsScan)
            {
                Snapshot = Scan();
                if (Snapshot.CapturedAt == null) throw new InvalidOperationException(Snapshot.Status);
                actionCharacterId = playerState.ContentId;
                actionNeedsScan = false;
                foreach (var container in Snapshot.Containers.Where(c => config.IsEnabled(c.Type)))
                {
                    if (container.Status != "Loaded" || container.Items.Any(i => i.ItemId == 0 || i.ItemLevel == null || i.InGearset == null))
                        throw new InvalidOperationException(T("Incomplete", container.Name));
                    if (activeAction == ArsenalAction.Clear)
                    {
                        foreach (var item in container.Items.Where(i => i.InGearset == false))
                            actionSteps.Enqueue(new(container.Type, container.Capacity, null, item));
                    }
                    else
                    {
                        var order = container.DisplaySlots ?? throw new InvalidOperationException(T("OrderUnavailable"));
                        var items = container.Items.ToDictionary(i => i.Slot);
                        var slots = order.Select(slot => items.TryGetValue(slot, out var item)
                            ? new SortEntry(item.Fingerprint, item.ItemLevel!.Value, item.InGearset!.Value) : null).ToArray();
                        var plan = activeAction == ArsenalAction.Random
                            ? InventorySortPlan.Randomize(slots, Random.Shared)
                            : InventorySortPlan.Create(slots, activeAction == ArsenalAction.Split);
                        foreach (var swap in plan)
                            actionSteps.Enqueue(new(container.Type, container.Capacity, swap, null));
                    }
                }
                totalSteps = actionSteps.Count;
                RefreshPendingSlots();
                ScheduleActionStep();
                ActionStatus = T("Progress", T(activeAction!.Value.ToString()), 0, totalSteps);
            }

            if (actionCharacterId != playerState.ContentId)
                throw new InvalidOperationException(T("CharacterChanged"));

            if (waitingForClear)
            {
                if (MoveBusy) return;
                waitingForClear = false;
                if (!lastMoveSucceeded) throw new InvalidOperationException(MoveStatus);
                StepCompleted();
            }

            var inventory = InventoryManager.Instance();
            if (inventory == null) throw new InvalidOperationException(T("InventoryUnavailable"));
            if (swapInFlight is { } pending)
            {
                var order = ReadArmouryOrder(pending.Step.Type, pending.Step.Capacity);
                var swap = pending.Step.Swap!;
                var matches = order != null && order[swap.Left] == pending.LeftSlot && order[swap.Right] == pending.RightSlot &&
                    Matches(inventory, pending.Step.Type, pending.LeftSlot, swap.BeforeRight) &&
                    Matches(inventory, pending.Step.Type, pending.RightSlot, swap.BeforeLeft);
                var fromSlot = swap.BeforeLeft == null ? pending.RightSlot : pending.LeftSlot;
                var toSlot = swap.BeforeLeft == null ? pending.LeftSlot : pending.RightSlot;
                if (ValidateMove(inventory, pending.Confirmation, pending.Step.Type, fromSlot, pending.Step.Type, toSlot, matches))
                {
                    swapInFlight = null;
                    StepCompleted();
                }
                else if (pending.Confirmation.IsExpired(Environment.TickCount64))
                    throw new InvalidOperationException(T("SwapFailed"));
                else return;
            }

            if (stopping) { FinishAction(T("Canceled")); return; }
            if (actionSteps.Count == 0 && preparedSwap == null) { FinishAction("", true); return; }
            if (Environment.TickCount64 < nextActionAt) return;
            if (preparedSwap is { } prepared && Environment.TickCount64 < prepared.Deadline) return;

            foreach (var operation in inventory->PendingOperations)
            {
                if (operation.IsEmpty) continue;
                if (inventoryBusySince == 0) inventoryBusySince = Environment.TickCount64;
                if (Environment.TickCount64 - inventoryBusySince >= 5000)
                    throw new InvalidOperationException(T("InventoryBusy"));
                return;
            }
            inventoryBusySince = 0;

            var step = preparedSwap?.Step ?? actionSteps.Dequeue();
            if (!config.IsEnabled(step.Type))
            {
                preparedSwap = null;
                totalSteps--;
                RefreshPendingSlots();
                return;
            }
            if (step.ClearItem is { } clearItem)
            {
                QueueMove(step.Type, clearItem, true);
                waitingForClear = true;
                ActionStatus = T("Working", T(activeAction!.Value.ToString()), completedSteps + 1, totalSteps);
                return;
            }

            var positions = ReadArmouryOrder(step.Type, step.Capacity)
                ?? throw new InvalidOperationException(T("OrderUnavailable"));
            var pair = step.Swap!;
            var left = positions[pair.Left];
            var right = positions[pair.Right];
            if (!Matches(inventory, step.Type, left, pair.BeforeLeft) || !Matches(inventory, step.Type, right, pair.BeforeRight))
                throw new InvalidOperationException(T("Stale"));

            if (activeAction == ArsenalAction.Split)
            {
                var gearsets = RaptureGearsetModule.Instance();
                if (gearsets == null) throw new InvalidOperationException(T("GearsetUnavailable"));
                foreach (var (slot, expected) in new[] { (left, pair.BeforeLeft), (right, pair.BeforeRight) })
                    if (expected != null && gearsets->IsItemRegisteredToGearset(ReadSlot(inventory, step.Type, slot)) != expected.InGearset)
                        throw new InvalidOperationException(T("GearsetChanged"));
            }

            if (preparedSwap == null || preparedSwap.LeftSlot != left || preparedSwap.RightSlot != right)
            {
                preparedSwap = new(step, left, right,
                    Math.Max(nextOperationAt, Environment.TickCount64 + config.EffectiveDelay * 70 / 100));
                ActionStatus = T("Working", T(activeAction!.Value.ToString()), completedSteps + 1, totalSteps);
                return;
            }
            preparedSwap = null;

            var from = pair.BeforeLeft == null ? right : left;
            var to = pair.BeforeLeft == null ? left : right;
            var deadline = Environment.TickCount64 + MoveConfirmation.TimeoutMilliseconds;
            var result = inventory->MoveItemSlot(step.Type, (ushort)from, step.Type, (ushort)to, true);
            swapInFlight = new(step, left, right, deadline);
            ValidateMove(inventory, swapInFlight.Confirmation, step.Type, from, step.Type, to, false);
            ActionStatus = T("Working", T(activeAction!.Value.ToString()), completedSteps + 1, totalSteps);
            log.Debug("{Action}: {Type} display positions {Left}/{Right}, physical {From}->{To}, result {Result}",
                activeAction?.ToString() ?? "Sort", step.Type, pair.Left, pair.Right, from, to, result);
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Arsenal-Aktion gestoppt");
            FinishAction(ex.Message);
        }
    }

    private static unsafe bool Matches(InventoryManager* inventory, InventoryType type, int slot, SortEntry? expected)
    {
        var item = ReadSlot(inventory, type, slot);
        return item != null && !item->IsSymbolic && (expected == null
            ? item->ItemId == 0 : item->ItemId != 0 && Fingerprint(item) == expected.Fingerprint);
    }

    private static unsafe int[]? ReadArmouryOrder(InventoryType type, int capacity)
    {
        var module = ItemOrderModule.Instance();
        if (module == null) return null;
        var sorter = type switch
        {
            InventoryType.ArmoryMainHand => module->ArmouryMainHandSorter,
            InventoryType.ArmoryOffHand => module->ArmouryOffHandSorter,
            InventoryType.ArmoryHead => module->ArmouryHeadSorter,
            InventoryType.ArmoryBody => module->ArmouryBodySorter,
            InventoryType.ArmoryHands => module->ArmouryHandsSorter,
            InventoryType.ArmoryLegs => module->ArmouryLegsSorter,
            InventoryType.ArmoryFeets => module->ArmouryFeetSorter,
            InventoryType.ArmoryEar => module->ArmouryEarsSorter,
            InventoryType.ArmoryNeck => module->ArmouryNeckSorter,
            InventoryType.ArmoryWrist => module->ArmouryWristsSorter,
            InventoryType.ArmoryRings => module->ArmouryRingsSorter,
            InventoryType.ArmorySoulCrystal => module->ArmourySoulCrystalSorter,
            _ => null,
        };
        if (sorter == null || sorter->InventoryType != type || sorter->SortFunctionIndex != -1 ||
            sorter->Items.LongCount != capacity || capacity < 1 || capacity > 200) return null;
        var result = new int[capacity];
        var seen = new HashSet<int>();
        for (var i = 0; i < capacity; i++)
        {
            var entry = sorter->Items[i].Value;
            if (entry == null || entry->Page != 0 || entry->Slot >= capacity || !seen.Add(entry->Slot)) return null;
            result[i] = entry->Slot;
        }
        return result;
    }
}

namespace ThrowCrapOut;

internal enum ArsenalAction { Sort, Split, Clear, Random }
internal sealed record SortEntry(string Fingerprint, int Level, bool InGearset);
internal sealed record SlotSwap(int Left, int Right, SortEntry? BeforeLeft, SortEntry? BeforeRight);

internal static class InventorySortPlan
{
    public static List<SlotSwap> Create(SortEntry?[] slots, bool split)
    {
        var target = slots.OrderBy(item => item == null ? 1 : split && !item.InGearset ? 2 : 0)
            .ThenByDescending(item => item?.Level ?? 0).ToArray();
        return PlanPermutation(slots, target);
    }

    private readonly record struct SlotKey(SortEntry? Item);

    public static List<SlotSwap> Randomize(SortEntry?[] slots, Random random)
    {
        var target = slots.ToArray();
        for (var i = target.Length - 1; i > 0; i--)
        {
            var other = random.Next(i + 1);
            (target[i], target[other]) = (target[other], target[i]);
        }
        if (target.Length > 1 && target.SequenceEqual(slots))
        {
            var other = Array.FindIndex(target, entry => !Equals(entry, target[0]));
            if (other >= 0) (target[0], target[other]) = (target[other], target[0]);
        }
        return PlanPermutation(slots, target);
    }

    private static List<SlotSwap> PlanPermutation(SortEntry?[] slots, SortEntry?[] target)
    {
        var targetIds = new int[slots.Length];
        var remaining = new Dictionary<SlotKey, Queue<int>>();
        for (var i = 0; i < slots.Length; i++)
        {
            targetIds[i] = Equals(slots[i], target[i]) ? i : -1;
            if (targetIds[i] >= 0) continue;
            var key = new SlotKey(slots[i]);
            if (!remaining.TryGetValue(key, out var indices)) remaining[key] = indices = new();
            indices.Enqueue(i);
        }
        for (var i = 0; i < target.Length; i++)
            if (targetIds[i] < 0) targetIds[i] = remaining[new(target[i])].Dequeue();

        var working = slots.ToArray();
        var ids = Enumerable.Range(0, slots.Length).ToArray();
        var positions = ids.ToArray();
        var result = new List<SlotSwap>();
        for (var left = 0; left < slots.Length; left++)
        {
            var right = positions[targetIds[left]];
            if (left == right) continue;
            if (!Equals(working[left], working[right]))
                result.Add(new(left, right, working[left], working[right]));
            (working[left], working[right]) = (working[right], working[left]);
            (ids[left], ids[right]) = (ids[right], ids[left]);
            positions[ids[left]] = left;
            positions[ids[right]] = right;
        }
        return result;
    }
}

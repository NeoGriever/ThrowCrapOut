namespace ThrowCrapOut;

internal static class ClearPlanning
{
    public static bool HasCapacity(int freeSlots, int itemsToMove, int reservedSlots) =>
        freeSlots >= 0 && itemsToMove >= 0 && freeSlots - Math.Clamp(reservedSlots, 0, 140) >= itemsToMove;

    public static IEnumerable<T> Order<T>(IEnumerable<T> items, Func<T, int> itemLevel, bool highestFirst) =>
        highestFirst ? items.OrderByDescending(itemLevel) : items;
}

namespace ThrowCrapOut;

internal sealed class MoveConfirmation(long deadline)
{
    public const int TimeoutMilliseconds = 5000;
    private bool observed;
    private long? stableSince;

    public bool IsExpired(long now) => now >= deadline;

    public bool Update(long now, bool operationObserved, bool operationsPending, bool slotsMatch)
    {
        if (IsExpired(now)) return false;
        observed |= operationObserved;
        if (!observed || operationsPending || !slotsMatch)
        {
            stableSince = null;
            return false;
        }
        stableSince ??= now;
        return now - stableSince.Value >= 250;
    }
}

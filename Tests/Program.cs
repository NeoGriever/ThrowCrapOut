using ThrowCrapOut;

var random = new Random(419);
var cases = 0;
foreach (var size in new[] { 0, 1, 2, 26, 35, 50 })
foreach (var split in new[] { false, true })
for (var example = 0; example < 100; example++)
{
    var input = Enumerable.Range(0, size).Select(i => random.Next(4) == 0
        ? null : new SortEntry($"item-{i}", random.Next(1, 6), random.Next(2) == 0)).ToArray();
    var original = input.ToArray();
    var plan = InventorySortPlan.Create(input, split);
    Assert(input.SequenceEqual(original), "Planning changed its input snapshot");
    var actual = input.ToArray();
    foreach (var swap in plan)
    {
        Assert(swap.Left >= 0 && swap.Right >= 0 && swap.Left < size && swap.Right < size && swap.Left != swap.Right, "Swap escaped category bounds");
        Assert(actual[swap.Left] == swap.BeforeLeft && actual[swap.Right] == swap.BeforeRight,
            "Swap precondition did not match the evolving inventory");
        Assert(swap.BeforeLeft != null || swap.BeforeRight != null, "Empty-to-empty operation");
        (actual[swap.Left], actual[swap.Right]) = (actual[swap.Right], actual[swap.Left]);
    }
    var occupied = original.OfType<SortEntry>().ToArray();
    var empty = new SortEntry?[size - occupied.Length];
    SortEntry?[] expected = split
        ? occupied.Where(i => i.InGearset).OrderByDescending(i => i.Level).Cast<SortEntry?>()
            .Concat(empty).Concat(occupied.Where(i => !i.InGearset).OrderByDescending(i => i.Level)).ToArray()
        : occupied.OrderByDescending(i => i.Level).Cast<SortEntry?>().Concat(empty).ToArray();
    Assert(actual.SequenceEqual(expected), "Incorrect order, lost item, or unstable equal-level ordering");
    Assert(InventorySortPlan.Create(actual, split).Count == 0, "Already sorted inventory requires more steps");
    Assert(plan.Count <= Math.Max(0, size - 1), "Direct-swap bound exceeded");
    cases++;
}
for (var example = 0; example < 200; example++)
{
    var slots = Enumerable.Range(0, 50).Select(_ => random.Next(4) == 0 ? null
        : new SortEntry("duplicate", random.Next(1, 4), random.Next(2) == 0)).ToArray();
    foreach (var split in new[] { false, true })
    {
        var actual = Replay(slots, InventorySortPlan.Create(slots, split));
        var expected = slots.OrderBy(i => i == null ? 1 : split && !i.InGearset ? 2 : 0)
            .ThenByDescending(i => i?.Level ?? 0);
        Assert(actual.SequenceEqual(expected), "Duplicate-item ordering failed");
        cases++;
    }
}

for (var example = 0; example < 100; example++)
{
    var source = Enumerable.Range(0, 50).Select(i => i % 4 == 0 ? null : new SortEntry(i.ToString(), random.Next(1, 101), i % 2 == 0)).ToArray();
    var mixed = Replay(source, InventorySortPlan.Randomize(source, random));
    Assert(!mixed.SequenceEqual(source), "Randomization did not change the category");
    Assert(mixed.OfType<SortEntry>().OrderBy(i => i.Fingerprint).SequenceEqual(source.OfType<SortEntry>().OrderBy(i => i.Fingerprint)), "Randomization lost items");
    Assert(mixed.Count(i => i == null) == source.Count(i => i == null), "Randomization lost empty slots");
    cases++;
}

var reversed = Enumerable.Range(1, 50).Select(i => (SortEntry?)new SortEntry($"item-{i}", i, true)).ToArray();
var optimizedCount = InventorySortPlan.Create(reversed, false).Count;
Assert(optimizedCount == 25, "Reverse-order benchmark should need 25 direct swaps");
Console.WriteLine($"Passed {cases} Sort/Split regression cases. Reverse-ordered 50 items: {optimizedCount} swaps instead of 1225 adjacent bubble swaps.");

foreach (var action in Enum.GetValues<ArsenalAction>())
    Assert(Text.Strings.ContainsKey(action.ToString()), $"Missing action translation: {action}");

foreach (var (key, translations) in Text.Strings)
{
    Assert(translations.Length == 4 && translations.All(t => !string.IsNullOrWhiteSpace(t)), $"Missing translation: {key}");
    var placeholders = System.Text.RegularExpressions.Regex.Matches(translations[0], @"\{\d+\}")
        .Select(m => m.Value).Order().ToArray();
    foreach (var language in Enum.GetValues<Language>())
    {
        var translated = translations[(int)language];
        var actual = System.Text.RegularExpressions.Regex.Matches(translated, @"\{\d+\}").Select(m => m.Value).Order();
        Assert(placeholders.SequenceEqual(actual), $"Translation placeholders differ: {key}/{language}");
        _ = Text.Get(language, key, "Item", "AST WHM", 4, 10);
    }
    Assert(Text.Get((Language)999, key) == translations[0], "Invalid language must fall back to English");
}
Assert(Text.Get(Language.English, "Moved", "Copper Gorget", "AST WHM", 4) ==
    "Moved Copper Gorget (AST WHM / IL: 4) to Inventory", "Incorrect English transfer message");
Console.WriteLine($"Passed localization checks for {Text.Strings.Count} messages in all four languages.");

var confirmation = new MoveConfirmation(5000);
Assert(!confirmation.Update(0, false, false, true), "Local changes alone must not confirm a move");
Assert(!confirmation.Update(1000, false, false, true), "Unobserved requests must not confirm after waiting");
Assert(!confirmation.Update(1010, true, true, true), "Pending request must block confirmation");
Assert(!confirmation.Update(1100, false, false, true), "Queue drainage must start stability observation");
Assert(!confirmation.Update(1349, false, false, true), "Stable interval too short");
Assert(!confirmation.Update(1350, false, false, false), "Rollback must reset stability observation");
Assert(!confirmation.Update(1400, false, false, true), "Recovered state must restart stability observation");
Assert(!confirmation.Update(1650, false, true, true), "New pending work must reset stability observation");
Assert(!confirmation.Update(1700, false, false, true), "Queue must remain drained during stability observation");
Assert(confirmation.Update(1950, false, false, true), "Drained request and stable matching slots must confirm");
Assert(!new MoveConfirmation(5000).Update(2000, false, false, true), "Confirmation must not leak between moves");
var timeout = new MoveConfirmation(MoveConfirmation.TimeoutMilliseconds);
Assert(!timeout.Update(0, true, true, false), "Request starts unconfirmed");
Assert(!timeout.Update(4750, false, false, true), "Late matching state starts stability observation");
Assert(!timeout.IsExpired(4999), "Confirmation deadline expired too early");
Assert(timeout.IsExpired(5000), "Confirmation must expire at exactly five seconds");
Assert(!timeout.Update(5000, false, false, true), "Confirmation at the deadline must not succeed");
Assert(!timeout.Update(6000, false, false, true), "Late confirmation must not override timeout");
var justInTime = new MoveConfirmation(5000);
Assert(!justInTime.Update(4749, true, false, true), "Stable interval must be observed");
Assert(justInTime.Update(4999, false, false, true), "Confirmation before the deadline must succeed immediately");
Console.WriteLine("Passed move confirmation checks, including rollback, pending operations and the five-second deadline.");

var candidates = new[]
{
    (Category: "Main", Level: 40, Slot: 0),
    (Category: "Main", Level: 70, Slot: 1),
    (Category: "Ear", Level: 90, Slot: 0),
    (Category: "Body", Level: 70, Slot: 2),
};
Assert(ClearPlanning.Order(candidates, item => item.Level, false).SequenceEqual(candidates),
    "Default clear order must stay unchanged");
Assert(ClearPlanning.Order(candidates, item => item.Level, true).Select(item => item.Level)
    .SequenceEqual(new[] { 90, 70, 70, 40 }), "Clear order must span all categories");
Assert(ClearPlanning.Order(candidates, item => item.Level, true).Where(item => item.Level == 70)
    .Select(item => item.Category).SequenceEqual(new[] { "Main", "Body" }),
    "Equal item levels must retain their original order");
Assert(ClearPlanning.HasCapacity(12, 5, 7), "Exact capacity must be accepted");
Assert(!ClearPlanning.HasCapacity(11, 5, 7), "Insufficient capacity must be rejected before clear");
Assert(ClearPlanning.HasCapacity(1, 1, 0), "Zero reserve permits the last free slot");
Assert(!ClearPlanning.HasCapacity(1, 1, 1), "Reserve must remain free after a move");
Assert(!ClearPlanning.HasCapacity(140, 1, 140), "Full reserve blocks all moves");
Assert(ClearPlanning.HasCapacity(140, 0, 140), "Empty clear plan should be permitted");
Console.WriteLine("Passed global clear ordering and inventory reserve checks.");

static SortEntry?[] Replay(SortEntry?[] source, List<SlotSwap> plan)
{
    Assert(plan.Count <= Math.Max(0, source.Length - 1), "More than n-1 swaps");
    var actual = source.ToArray();
    foreach (var swap in plan)
    {
        Assert(swap.Left >= 0 && swap.Right >= 0 && swap.Left < actual.Length && swap.Right < actual.Length && swap.Left != swap.Right,
            "Swap escaped category bounds");
        Assert(actual[swap.Left] == swap.BeforeLeft && actual[swap.Right] == swap.BeforeRight, "Stale swap preconditions");
        Assert(!Equals(swap.BeforeLeft, swap.BeforeRight), "Redundant identical-item/empty swap");
        (actual[swap.Left], actual[swap.Right]) = (actual[swap.Right], actual[swap.Left]);
    }
    return actual;
}

static void Assert(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

using System.Reflection;
using Dalamud.Game;
using Lumina.Excel.Sheets;

namespace ThrowCrapOut;

internal sealed partial class ArmouryScanner
{
    private readonly Dictionary<uint, string> classTagCache = new();
    private (PropertyInfo Membership, string Tag)[]? classDefinitions;
    private Language? classLanguage;
    public void LanguageChanged()
    {
        if (!Busy)
        {
            ActionStatus = "";
            MoveStatus = "";
            Status = OperationStatus.Idle;
            completedSteps = totalSteps = 0;
        }
        RequestRefresh();
    }
    private ClientLanguage DataLanguage => config.Language switch
    {
        Language.German => ClientLanguage.German,
        Language.French => ClientLanguage.French,
        Language.Japanese => ClientLanguage.Japanese,
        _ => ClientLanguage.English,
    };

    private string GetClassTags(ClassJobCategory category)
    {
        if (classLanguage != config.Language)
        {
            classTagCache.Clear();
            classDefinitions = null;
            classLanguage = config.Language;
        }
        if (classTagCache.TryGetValue(category.RowId, out var cached))
            return cached;

        classDefinitions ??= data.GetExcelSheet<ClassJob>(ClientLanguage.English)
            .Where(job => job.RowId != 0)
            .OrderBy(job => job.RowId)
            .Select(job => (
                Membership: typeof(ClassJobCategory).GetProperty(job.Abbreviation.ToString()),
                Tag: data.GetExcelSheet<ClassJob>(DataLanguage).GetRow(job.RowId).Abbreviation.ToString()))
            .Where(entry => entry.Membership?.PropertyType == typeof(bool) && entry.Tag.Length > 0)
            .Select(entry => (entry.Membership!, entry.Tag))
            .ToArray();

        var tags = classDefinitions
            .Where(entry => entry.Membership.GetValue(category) is true)
            .Select(entry => entry.Tag).ToArray();
        var label = tags.Length == 0
            ? T("Unknown")
            : tags.Length == classDefinitions.Length
                ? T("All")
                : tags.Length > 5
                    ? string.Join(' ', tags.Take(4)) + $" {tags.Length - 4}+"
                    : string.Join(' ', tags);
        classTagCache[category.RowId] = label;
        return label;
    }
}

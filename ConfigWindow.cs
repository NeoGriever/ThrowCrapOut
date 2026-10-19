using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace ThrowCrapOut;

internal sealed class ConfigWindow(Configuration config, Action save) : Window("ThrowCrapOut - Einstellungen###TcoConfig")
{
    private string T(string key) => Text.Get(config.Language, key);

    public override void PreDraw()
    {
        WindowName = $"ThrowCrapOut - {T("Settings")}###TcoConfig";
        Size = new Vector2(349, 365);
        SizeCondition = ImGuiCond.Always;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = Size.Value, MaximumSize = Size.Value };
        Flags = ImGuiWindowFlags.NoResize;
    }

    public override void Draw()
    {
        ImGui.TextUnformatted(T("Language"));
        ImGui.SameLine();
        var language = Enum.IsDefined(config.Language) ? (int)config.Language : 0;
        ImGui.SetNextItemWidth(-1);
        if (ImGui.Combo("##Language", ref language, "English\0Deutsch\0Français\0日本語\0"))
        {
            config.Language = (Language)language;
            save();
        }
        var chatInfos = config.ChatInfos;
        if (ImGui.Checkbox(T("ChatInfos"), ref chatInfos))
        {
            config.ChatInfos = chatInfos;
            save();
        }
        ImGui.TextUnformatted(T("Delay"));
        var ticks = config.EffectiveDelay / 5;
        ImGui.SetNextItemWidth(-1);
        var delayLabel = (ticks * 0.005).ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) + " s";
        if (ImGui.SliderInt("##Delay", ref ticks, 100, 1000, delayLabel, ImGuiSliderFlags.NoInput))
        {
            config.DelayMilliseconds = Math.Clamp(ticks, 100, 1000) * 5;
            save();
        }
        ImGui.TextUnformatted(T("FreeInventorySlots"));
        var freeSlots = config.EffectiveFreeInventorySlots;
        ImGui.SetNextItemWidth(-1);
        if (ImGui.SliderInt("##FreeInventorySlots", ref freeSlots, 0, 140, "%d", ImGuiSliderFlags.NoInput))
        {
            config.FreeInventorySlots = freeSlots;
            save();
        }
        var firstHighLevelGear = config.FirstHighLevelGear;
        if (ImGui.Checkbox(T("FirstHighLevelGear"), ref firstHighLevelGear))
        {
            config.FirstHighLevelGear = firstHighLevelGear;
            save();
        }
        var labels = MainWindow.CardOrder.Select(type => T(type.ToString())).ToArray();
        if (ImGui.BeginTable("CategoryOptions", 4, ImGuiTableFlags.SizingFixedFit))
        {
            for (var row = 0; row < 6; row++)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                var width = labels.Where((_, i) => i % 2 == 0).Max(label => ImGui.CalcTextSize(label).X);
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + Math.Max(0, width - ImGui.CalcTextSize(labels[row * 2]).X));
                ImGui.TextUnformatted(labels[row * 2]);
                ImGui.TableNextColumn();
                Checkbox(row * 2);
                ImGui.TableNextColumn();
                Checkbox(row * 2 + 1);
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(labels[row * 2 + 1]);
            }
            ImGui.EndTable();
        }
    }

    private void Checkbox(int index)
    {
        InventoryType type = MainWindow.CardOrder[index];
        var enabled = config.IsEnabled(type);
        if (!ImGui.Checkbox($"##Category{index}", ref enabled)) return;
        if (enabled) config.DisabledCategories.Remove(type);
        else config.DisabledCategories.Add(type);
        save();
    }
}

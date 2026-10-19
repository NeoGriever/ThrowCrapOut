using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace ThrowCrapOut;

internal sealed class MainWindow(ArmouryScanner scanner, Configuration config, Action openConfig) : Window("ThrowCrapOut - Arsenal###TcoArsenal")
{
    internal static readonly InventoryType[] CardOrder =
    [
      InventoryType.ArmoryMainHand, InventoryType.ArmoryOffHand,
    InventoryType.ArmoryHead, InventoryType.ArmoryEar,
    InventoryType.ArmoryBody, InventoryType.ArmoryNeck,
    InventoryType.ArmoryHands, InventoryType.ArmoryWrist,
    InventoryType.ArmoryLegs, InventoryType.ArmoryRings,
    InventoryType.ArmoryFeets, InventoryType.ArmorySoulCrystal,
  ];

    private static readonly Vector4 Registered = new(0.40f, 0.91f, 0.43f, 1);
    private static readonly Vector4 Unregistered = new(1, 0.40f, 0.40f, 1);
    private static readonly Vector4 Empty = new(0.32f, 0.35f, 0.39f, 1);
    private static readonly Vector4 Unknown = new(0.98f, 0.74f, 0.30f, 1);
    private static readonly Vector4 LevelColor = new(0f, 0.63f, 0f, 1);
    private static readonly ArsenalAction[] VisibleActions = [ArsenalAction.Sort, ArsenalAction.Split, ArsenalAction.Clear];
    private static readonly Vector4 Active = new(0.40f, 0.82f, 1, 1);
    private bool clearHovered;
    private string T(string key, params object[] args) => Text.Get(config.Language, key, args);

    public override void OnOpen() => scanner.RequestRefresh();

    public override void PreDraw()
    {
        WindowName = $"ThrowCrapOut - {T("Armoury")}###TcoArsenal";
        Size = new Vector2(523, 834);
        SizeCondition = ImGuiCond.Always;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = Size.Value, MaximumSize = Size.Value };
        Flags = ImGuiWindowFlags.NoResize;
    }

    public override void Draw()
    {
        var snapshot = scanner.Snapshot;
        var style = ImGui.GetStyle();
        var rowStart = ImGui.GetCursorPosX();
        var rowWidth = ImGui.GetContentRegionAvail().X;
        var buttonWidth = VisibleActions.Max(action => ImGui.CalcTextSize(T(action.ToString())).X) + style.FramePadding.X * 2;
        var settingsLabel = T("Settings");
        var settingsWidth = ImGui.CalcTextSize(settingsLabel).X + style.FramePadding.X * 2;
        clearHovered = false;
        ImGui.BeginDisabled(scanner.Busy);
        foreach (var action in VisibleActions)
        {
            if (ImGui.Button(T(action.ToString()) + "###" + action, new Vector2(buttonWidth, 0))) scanner.RequestAction(action);
            if (action == ArsenalAction.Clear) clearHovered = ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled);
            if (ImGui.IsItemHovered() && T(action.ToString() + "Tooltip") != "")
            {
                ImGui.BeginTooltip();
                {
                    ImGui.TextUnformatted(T(action.ToString() + "Tooltip"));
                }
                ImGui.EndTooltip();
            }
            ImGui.SameLine();
        }
        ImGui.EndDisabled();
        ImGui.SameLine();

        ImGui.BeginDisabled(!scanner.Busy && !scanner.ActionBusy);
        if (ImGui.Button(T("Stop"))) scanner.StopAction();
        ImGui.EndDisabled();
        ImGui.SameLine();

        ImGui.SetCursorPosX(Math.Max(ImGui.GetCursorPosX(), rowStart + rowWidth - settingsWidth));
        if (ImGui.Button(settingsLabel, new Vector2(settingsWidth, 0))) openConfig();

        var status = scanner.ActionBusy || !string.IsNullOrEmpty(scanner.ActionStatus)
          ? scanner.ActionStatus : scanner.MoveStatus;
        var statusColor = scanner.Status switch
        {
            OperationStatus.Working => new Vector4(1, 1, 0, 1),
            OperationStatus.Failed => new Vector4(1, 0, 0, 1),
            OperationStatus.Complete => new Vector4(0, 1, 0, 1),
            _ => Vector4.One,
        };
        ImGui.PushStyleColor(ImGuiCol.Text, statusColor);
        ImGui.TextWrapped(string.IsNullOrEmpty(status) ? T("Idle") : status);
        ImGui.PopStyleColor();

        var barStart = ImGui.GetCursorScreenPos();
        var barSize = new Vector2(Math.Max(0, ImGui.GetContentRegionAvail().X), 4);
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddRectFilled(barStart, barStart + barSize, ImGui.GetColorU32(new Vector4(0, 0, 0, 1)));
        if (scanner.Progress > 0)
            drawList.AddRectFilled(barStart, barStart + new Vector2(barSize.X * scanner.Progress, 4),
                ImGui.GetColorU32(new Vector4(0, 1, 0, 1)));
        ImGui.Dummy(barSize);

        Legend(Registered, "");
        ImGui.SameLine();
        Legend(Unregistered, "");
        ImGui.SameLine();
        Legend(Empty, "");
        ImGui.SameLine();
        Legend(Unknown, "");

        if (snapshot.CapturedAt == null)
        {
            ImGui.TextWrapped(snapshot.Status);
            return;
        }

        var scale = ImGui.GetFontSize() / 17f;
        var gap = 6f * scale;
        if (ImGui.BeginChild("ArsenalCards"))
        {
            ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new Vector2(gap / 2, gap / 2));
            if (ImGui.BeginTable("Categories", 2, ImGuiTableFlags.SizingStretchSame))
            {
                for (var row = 0; row < CardOrder.Length; row += 2)
                {
                    var left = snapshot.Containers.FirstOrDefault(c => c.Type == CardOrder[row]);
                    var right = snapshot.Containers.FirstOrDefault(c => c.Type == CardOrder[row + 1]);
                    var capacity = Math.Max(left?.Capacity ?? 0, right?.Capacity ?? 0);
                    var height = Math.Max(72f, ((capacity + 4) / 5) * 12f + 16f) * scale;
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    if (left != null)
                        DrawCard(left, height, scale);
                    ImGui.TableNextColumn();
                    if (right != null)
                        DrawCard(right, height, scale);
                }
                ImGui.EndTable();
            }
            ImGui.PopStyleVar();
        }
        ImGui.EndChild();
    }

    private static void Legend(Vector4 color, string text)
    {
        var origin = ImGui.GetCursorScreenPos();
        var size = ImGui.GetFontSize();
        ImGui.GetWindowDrawList().AddCircleFilled(origin + new Vector2(size * 0.30f, size * 0.5f),
          size * 0.22f, ImGui.ColorConvertFloat4ToU32(color));
        ImGui.Dummy(new Vector2(size * 0.6f, size));
        ImGui.SameLine(0, 3);
        ImGui.TextUnformatted(text);
    }

    private void DrawCard(ContainerSnapshot container, float height, float scale)
    {
        ImGui.PushID((int)container.Type);
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var size = new Vector2(width, height);
        var draw = ImGui.GetWindowDrawList();
        var padding = 10f * scale;
        var pitch = 12f * scale;
        var radius = 4.3f * scale;
        var textColor = ImGui.GetColorU32(ImGuiCol.Text);
        var muted = ImGui.GetColorU32(ImGuiCol.TextDisabled);
        var enabled = config.IsEnabled(container.Type);
        var background = enabled ? new Vector4(0.055f, 0.065f, 0.075f, 0.90f) : new Vector4(0.035f, 0.035f, 0.035f, 0.95f);
        var border = enabled ? new Vector4(0.25f, 0.28f, 0.31f, 0.8f) : new Vector4(0.12f, 0.12f, 0.12f, 0.8f);
        draw.AddRectFilled(origin, origin + size, ImGui.ColorConvertFloat4ToU32(background), 5 * scale);
        draw.AddRect(origin, origin + size, ImGui.ColorConvertFloat4ToU32(border), 5 * scale);

        var textOrigin = origin + new Vector2(padding, (height - ImGui.GetFontSize() * 2.5f) / 2);
        var label = T(container.Type.ToString());
        var labelWidth = width - padding * 3 - pitch * 5;
        while (label.Length > 1 && ImGui.CalcTextSize(label).X > labelWidth)
            label = label.TrimEnd('…')[..^1] + "…";
        draw.AddText(textOrigin, enabled ? textColor : muted, label);
        var count = container.Items.Count(i => i.ItemId != 0);
        draw.AddText(textOrigin + new Vector2(0, ImGui.GetFontSize() * 1.45f), muted,
          container.Status == "Loaded" ? $"{count} / {container.Capacity}" : T(container.Status));
        var rows = (container.Capacity + 4) / 5;
        var gridOrigin = origin + new Vector2(width - padding - pitch * 5, (height - rows * pitch) / 2);
        for (var slot = 0; slot < container.Capacity; slot++)
        {
            var physicalSlot = container.DisplaySlots?[slot] ?? slot;
            var item = container.Slots[physicalSlot];
            var color = item == null ? Empty : item.InGearset switch
            {
                true => Registered,
                false => Unregistered,
                null => Unknown,
            };
            if (item != null && scanner.IsPendingSlot(container.Type, physicalSlot))
                color = new Vector4(color.X * 0.4f, color.Y * 0.4f, color.Z * 0.4f, color.W);
            if (!enabled) color = Vector4.Lerp(color, new Vector4(0.3f, 0.3f, 0.3f, 1), 0.6f);
            if (scanner.IsActiveSlot(container.Type, physicalSlot)) color = Active;
            var cell = gridOrigin + new Vector2(slot % 5 * pitch, slot / 5 * pitch);
            var center = cell + new Vector2(pitch / 2);
            if (item == null)
            {
                draw.AddCircleFilled(center, radius * 0.8f, ImGui.ColorConvertFloat4ToU32(color), 16);
                continue;
            }
            ImGui.SetCursorScreenPos(cell);
            ImGui.PushID(slot);
            var clicked = ImGui.InvisibleButton("Slot", new Vector2(pitch));
            var hovered = ImGui.IsItemHovered();
            draw.AddCircleFilled(center, radius, ImGui.ColorConvertFloat4ToU32(color), 16);
            if (clearHovered && enabled && item.InGearset == false)
                draw.AddCircle(center, radius + 1.5f * scale, ImGui.ColorConvertFloat4ToU32(new Vector4(1, 0.55f, 0.12f, 1)), 16);
            if (hovered)
            {
                draw.AddCircle(center, radius + 1.5f * scale, textColor, 16);
                ImGui.BeginTooltip();
                {
                    ImGui.TextUnformatted(item.Name);
                    ImGui.TextUnformatted(string.IsNullOrEmpty(item.ClassTags) ? T("Unknown") : item.ClassTags);
                    ImGui.SameLine(0, ImGui.CalcTextSize(" ").X);
                    ImGui.TextColored(LevelColor, T("Level", item.ItemLevel?.ToString() ?? "?"));
                }
                ImGui.EndTooltip();
            }
            if (clicked && enabled && item.InGearset == false && !scanner.Busy)
                scanner.RequestMove(container.Type, item);
            ImGui.PopID();
        }
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(size);
        ImGui.PopID();
    }
}

using Dalamud.Interface.Colors;
using Dalamud.Interface.Windowing;
using Lumina.Excel.Sheets;

namespace StatusTextEditor;

public static class UI
{
    private static string Filter = "";
    internal static uint Selected;

    private static StatusOverride? SelectedOverride =>
        P.Config.Overrides.FirstOrDefault(x => x.StatusId == Selected);

    public static void Draw()
    {
        if (P.Config == null || P.Patcher == null) return;

        ImGui.Checkbox("启用文本覆盖", ref P.Config.Enabled);
        if (ImGui.IsItemEdited()) P.Save();
        ImGui.SameLine();
        if (P.Patcher.Active) ImGuiEx.Text(ImGuiColors.ParsedGreen, "状态行接口已挂钩");
        else ImGuiEx.Text(ImGuiColors.DalamudRed, "未找到状态行接口签名，此客户端不可用");
        ImGui.SameLine();
        ImGuiEx.Text(ImGuiColors.DalamudGrey, "修改即时生效，仅本机显示");
        ImGui.Separator();

        if (ImGui.BeginTable("##main", 2, ImGuiTableFlags.Resizable))
        {
            ImGui.TableSetupColumn("##list", ImGuiTableColumnFlags.WidthFixed, 290f);
            ImGui.TableNextColumn();
            DrawListPanel();
            ImGui.TableNextColumn();
            DrawEditorPanel();
            ImGui.EndTable();
        }
    }

    private static void DrawListPanel()
    {
        ImGui.SetNextItemWidth(-1f);
        ImGui.InputTextWithHint("##filter", "筛选（名称 / ID）", ref Filter, 64);

        var overrides = P.Config.Overrides
            .Where(x => Filter.Length == 0
                     || x.StatusId.ToString().Contains(Filter)
                     || GetStatusName(x.StatusId).Contains(Filter, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (ImGui.BeginChild("##overrides", new(0, -ImGui.GetFrameHeightWithSpacing())))
        {
            if (overrides.Count == 0)
            {
                ImGuiEx.Text(ImGuiColors.DalamudGrey, "暂无条目。点击下方“添加状态”从状态列表中选择。");
            }
            foreach (var ov in overrides)
            {
                var selected = ov.StatusId == Selected;
                if (ThreadLoadImageHandler.TryGetIconTextureWrap(GetStatusIcon(ov.StatusId), false, out var tex))
                {
                    ImGui.Image(tex.Handle, new(20, 20));
                    ImGui.SameLine();
                }
                var name = GetStatusName(ov.StatusId);
                if (ImGui.Selectable($"{ov.StatusId}  {name}##ov{ov.StatusId}", selected))
                    Selected = ov.StatusId;
            }
        }
        ImGui.EndChild();

        if (ImGui.Button("+ 添加状态"))
            StatusPicker.Open();
        ImGui.SameLine();
        ImGui.BeginDisabled(Selected == 0);
        if (ImGui.Button("删除所选"))
        {
            var ov = SelectedOverride;
            if (ov != null)
            {
                P.Config.Overrides.Remove(ov);
                P.Save();
                Selected = 0;
            }
        }
        ImGui.EndDisabled();
    }

    private static void DrawEditorPanel()
    {
        var ov = SelectedOverride;
        if (ov == null)
        {
            ImGuiEx.Text(ImGuiColors.DalamudGrey, "从左侧选择一个条目进行编辑。");
            return;
        }

        if (ImGui.BeginTable("##editor", 2, ImGuiTableFlags.PadOuterX))
        {
            ImGui.TableSetupColumn("##label", ImGuiTableColumnFlags.WidthFixed, 110f);

            ImGui.TableNextColumn();
            ImGuiEx.TextV("状态 ID");
            ImGui.TableNextColumn();
            var id = (int)ov.StatusId;
            ImGui.SetNextItemWidth(120f);
            if (ImGui.InputInt("##id", ref id))
            {
                var newId = (uint)Math.Max(0, id);
                if (newId != ov.StatusId && P.Config.Overrides.Any(x => x.StatusId == newId))
                {
                    Notify.Error($"状态 {newId} 已存在条目。");
                }
                else
                {
                    ov.StatusId = newId;
                    Selected = newId;
                    P.Save();
                }
            }
            ImGui.SameLine();
            ImGuiEx.Text($"{GetStatusName(ov.StatusId)}");

            ImGui.TableNextColumn();
            ImGuiEx.TextV("名称");
            ImGui.TableNextColumn();
            var name = ov.Name;
            ImGui.SetNextItemWidth(-40f);
            if (ImGui.InputTextWithHint("##name", "留空保持原版", ref name, 200))
            {
                ov.Name = name;
                P.Save();
            }
            if (ThreadLoadImageHandler.TryGetIconTextureWrap(GetStatusIcon(ov.StatusId), false, out var icon))
            {
                ImGui.SameLine();
                ImGui.Image(icon.Handle, new(28, 28));
            }

            ImGui.TableNextColumn();
            ImGuiEx.TextV("描述");
            ImGui.TableNextColumn();
            var desc = ov.Description;
            ImGui.SetNextItemWidth(-40f);
            if (ImGuiEx.InputTextMultilineExpanding("##desc", ref desc, 4000, minLines: 5, maxLines: 20))
            {
                ov.Description = desc;
                P.Save();
            }
            var err = BBCode.Parse(desc, out var parseError);
            if (parseError.Length > 0)
                ImGuiEx.Text(ImGuiColors.DalamudRed, parseError);

            ImGui.EndTable();
        }

        if (ImGui.CollapsingHeader("格式化标签说明", ImGuiTreeNodeFlags.DefaultOpen))
        {
            ImGuiEx.TextWrapped("""
                此字段支持格式化标签（原生 tooltip 同样生效）：
                [color=Red]…[/color]、[color=31]…[/color] - 彩色文字
                [glow=LightBlue]…[/glow]、[glow=数值]…[/glow] - 发光文字轮廓
                可用颜色名称：
                WhiteNormal, White, Grey1, Grey2, Grey3, Grey4, Yellow, Black, LightYellow, Red, DarkRed,
                Green, DarkGreen, WarmSeaBlue, Orange, LightBlue, Gold, DarkBlue, LightGreen, Pink
                更多颜色可用 “/xldata uicolor” 指令查询数值后以 [color=数值] 使用
                [i]…[/i] - 斜体文字
                """);
        }
    }

    private static string GetStatusName(uint statusId)
    {
        try
        {
            return Svc.Data.GetExcelSheet<Status>().TryGetFirst(x => x.RowId == statusId, out var row)
                ? row.Name.ExtractText() : "?";
        }
        catch
        {
            return "?";
        }
    }

    private static uint GetStatusIcon(uint statusId)
    {
        try
        {
            return Svc.Data.GetExcelSheet<Status>().TryGetFirst(x => x.RowId == statusId, out var row)
                ? row.Icon : 0u;
        }
        catch
        {
            return 0;
        }
    }
}

public class StatusPicker : Window
{
    private string Filter = "";
    private List<(uint Id, string Name, uint Icon)> Cache = [];

    public StatusPicker() : base("选择状态##ste-picker")
    {
        this.SetMinSize(420, 380);
        EzConfigGui.WindowSystem.AddWindow(this);
    }

    public static void Open()
    {
        if (P.Picker == null) P.Picker = new();
        P.Picker.IsOpen = true;
    }

    public override void Draw()
    {
        ImGui.SetNextItemWidth(-1f);
        ImGui.InputTextWithHint("##picker-filter", "筛选（名称 / ID）", ref Filter, 64);

        if (Cache.Count == 0)
        {
            foreach (var x in Svc.Data.GetExcelSheet<Status>())
            {
                var name = x.Name.ExtractText();
                if (name.Length == 0) continue;
                Cache.Add((x.RowId, name, x.Icon));
            }
        }

        var matches = Cache
            .Where(x => Filter.Length == 0
                     || x.Name.Contains(Filter, StringComparison.OrdinalIgnoreCase)
                     || x.Id.ToString().Contains(Filter))
            .ToList();

        ImGuiEx.Text($"共 {matches.Count} 个状态（左键选择并添加覆盖）");
        if (ImGui.BeginChild("##picker-list"))
        {
            var clipper = new ImGuiListClipper();
            clipper.Begin(matches.Count);
            while (clipper.Step())
            {
                for (var i = (int)clipper.DisplayStart; i < clipper.DisplayEnd; i++)
                {
                    var (id, name, icon) = matches[i];
                    if (ThreadLoadImageHandler.TryGetIconTextureWrap(icon, false, out var tex))
                    {
                        ImGui.Image(tex.Handle, new(22, 22));
                        ImGui.SameLine();
                    }
                    var hasOverride = P.Config.Overrides.Any(x => x.StatusId == id);
                    var label = $"{id}  {name}{(hasOverride ? "  ✔" : "")}##st{id}";
                    if (ImGui.Selectable(label, UI.Selected == id))
                    {
                        P.AddOrSelect(id);
                    }
                    if (hasOverride && ImGui.IsItemHovered()) ImGui.SetTooltip("该状态已存在覆盖条目，点击直接选中。");
                }
            }
            clipper.End();
        }
        ImGui.EndChild();
    }
}

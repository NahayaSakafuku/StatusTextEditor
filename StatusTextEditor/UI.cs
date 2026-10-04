using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Windowing;
using Lumina.Excel.Sheets;

namespace StatusTextEditor;

public static class UI
{
    internal static string Filter = "";
    internal static uint Selected;

    private static string? RenamingCategory;   // 正在重命名的分类（null = 无）
    private static string RenameBuffer = "";

    private static StatusOverride? SelectedOverride =>
        P.Config.Overrides.FirstOrDefault(x => x.StatusId == Selected);

    private static IEnumerable<IGrouping<string, StatusOverride>> Grouped =>
        P.Config.Overrides
            .Where(MatchesFilter)
            .GroupBy(x => x.Category);

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
            ImGui.TableSetupColumn("##list", ImGuiTableColumnFlags.WidthFixed, 300f);
            ImGui.TableNextColumn();
            DrawListPanel();
            ImGui.TableNextColumn();
            DrawEditorPanel();
            ImGui.EndTable();
        }
    }

    private static bool MatchesFilter(StatusOverride x) =>
        Filter.Length == 0
     || x.StatusId.ToString().Contains(Filter)
     || GetStatusName(x.StatusId).Contains(Filter, StringComparison.OrdinalIgnoreCase);

    private static void DrawListPanel()
    {
        ImGui.SetNextItemWidth(-1f);
        ImGui.InputTextWithHint("##filter", "筛选（名称 / ID）", ref Filter, 64);

        if (ImGui.BeginChild("##overrides", new(0, -ImGui.GetFrameHeight() * 2 - 4f)))
        {
            if (P.Config.Overrides.Count == 0)
            {
                ImGuiEx.Text(ImGuiColors.DalamudGrey, "暂无条目。点击下方“添加状态”从状态列表中选择。");
            }
            else if (Filter.Length > 0)
            {
                // 筛选时平铺显示，不做分组折叠。
                foreach (var ov in P.Config.Overrides.Where(MatchesFilter).ToList())
                    DrawEntryRow(ov);
            }
            else
            {
                var index = 0;
                foreach (var group in Grouped.ToList())
                {
                    DrawCategoryHeader(group.Key, group.Count(), index++);
                    if (!P.Config.CollapsedCategories.Contains(group.Key))
                    {
                        ImGui.Indent(10f);
                        foreach (var ov in group) DrawEntryRow(ov);
                        ImGui.Unindent(10f);
                    }
                }
                if (index == 0)
                    ImGuiEx.Text(ImGuiColors.DalamudGrey, "没有符合筛选条件的条目。");
            }
        }
        ImGui.EndChild();

        if (ImGui.Button("+ 添加状态"))
            StatusPicker.Open();
        ImGui.SameLine();
        if (ImGui.Button("导入/导出"))
            TransferWindow.Open();
        ImGui.SameLine();
        ImGui.BeginDisabled(Selected == 0);
        if (ImGuiEx.IconButton(FontAwesomeIcon.Trash, "delSel"))
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

        if (ImGui.SmallButton("全部展开"))
            P.Config.CollapsedCategories.Clear();
        ImGui.SameLine();
        if (ImGui.SmallButton("全部折叠"))
        {
            P.Config.CollapsedCategories.Clear();
            foreach (var cat in Grouped.Select(g => g.Key))
                P.Config.CollapsedCategories.Add(cat);
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("拖动条目可排序；拖到分组标题上可移动分类；右键分组标题可重命名分类。");
    }

    private static void DrawCategoryHeader(string category, int count, int index)
    {
        var display = category.Length > 0 ? category : "默认";
        var collapsed = P.Config.CollapsedCategories.Contains(category);
        ImGui.SetNextItemOpen(!collapsed);

        if (RenamingCategory == category)
        {
            ImGui.SetNextItemWidth(-1f);
            if (ImGui.InputText($"##rename{index}", ref RenameBuffer, 64, ImGuiInputTextFlags.EnterReturnsTrue))
            {
                var newName = RenameBuffer.Trim();
                if (newName.Length > 0 && newName != category)
                    P.RenameCategory(category, newName);
                RenamingCategory = null;
            }
            if (ImGui.IsItemClicked(ImGuiMouseButton.Right)) RenamingCategory = null;
            return;
        }

        var open = ImGui.CollapsingHeader($"{display} ({count})##cat{index}");
        if (open == collapsed)
        {
            if (open) P.Config.CollapsedCategories.Remove(category);
            else P.Config.CollapsedCategories.Add(category);
            P.Save();
        }

        // 右键重命名
        if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
            ImGui.OpenPopup($"catmenu{index}");
        if (ImGui.BeginPopup($"catmenu{index}"))
        {
            if (ImGui.MenuItem("重命名分类"))
            {
                RenamingCategory = category;
                RenameBuffer = display;
            }
            ImGui.EndPopup();
        }

        // 拖拽条目到分组标题上 = 移动到该分类
        if (ImGui.BeginDragDropTarget())
        {
            if (ImGuiDragDrop.AcceptDragDropPayload<uint>("STE_OV", out var dragged))
            {
                if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
                    P.MoveToCategory(dragged, category);
            }
            ImGui.EndDragDropTarget();
        }
    }

    private static void DrawEntryRow(StatusOverride ov)
    {
        var selected = ov.StatusId == Selected;
        if (ThreadLoadImageHandler.TryGetIconTextureWrap(GetStatusIcon(ov.StatusId), false, out var tex))
        {
            ImGui.Image(tex.Handle, new(20, 20));
            ImGui.SameLine();
        }
        var name = GetStatusName(ov.StatusId);
        var label = $"{ov.StatusId}  {name}##ov{ov.StatusId}";
        if (ImGui.Selectable(label, selected))
            Selected = ov.StatusId;

        // 拖拽源
        if (ImGui.BeginDragDropSource())
        {
            ImGuiDragDrop.SetDragDropPayload("STE_OV", ov.StatusId);
            ImGui.TextUnformatted($"{ov.StatusId}  {name}");
            ImGui.EndDragDropSource();
        }
        // 拖拽目标 = 移动到该条目位置
        if (ImGui.BeginDragDropTarget())
        {
            if (ImGuiDragDrop.AcceptDragDropPayload<uint>("STE_OV", out var dragged))
            {
                if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
                    P.MoveEntry(dragged, ov.StatusId);
            }
            ImGui.EndDragDropTarget();
        }
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
            ImGuiEx.TextV("分类");
            ImGui.TableNextColumn();
            var cat = ov.Category;
            ImGui.SetNextItemWidth(-40f);
            if (ImGui.InputTextWithHint("##cat", "留空 = 默认分类", ref cat, 64))
            {
                ov.Category = cat.Trim();
                P.Save();
            }

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
            BBCode.Parse(desc, out var parseError);
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

    internal static string GetStatusName(uint statusId)
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

    internal static uint GetStatusIcon(uint statusId)
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
                        P.AddOrSelect(id);
                    if (hasOverride && ImGui.IsItemHovered()) ImGui.SetTooltip("该状态已存在覆盖条目，点击直接选中。");
                }
            }
            clipper.End();
        }
        ImGui.EndChild();
    }
}

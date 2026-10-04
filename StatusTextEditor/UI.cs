using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
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
        var s = Loc.S;

        ImGui.Checkbox(s.EnableOverrides, ref P.Config.Enabled);
        if (ImGui.IsItemEdited()) P.Save();
        ImGui.SameLine();
        if (P.Patcher.Active) ImGuiEx.Text(ImGuiColors.ParsedGreen, s.HookActive);
        else ImGuiEx.Text(ImGuiColors.DalamudRed, s.HookMissing);
        ImGui.SameLine();
        ImGuiEx.Text(ImGuiColors.DalamudGrey, s.LocalOnly);

        ImGui.SameLine();
        DrawLanguageCombo();

        ImGui.Separator();

        if (ImGui.BeginTable("##main", 2, ImGuiTableFlags.Resizable))
        {
            ImGui.TableSetupColumn("##list", ImGuiTableColumnFlags.WidthFixed, 300f);
            ImGui.TableNextColumn();
            DrawListPanel(s);
            ImGui.TableNextColumn();
            DrawEditorPanel(s);
            ImGui.EndTable();
        }
    }

    private static void DrawLanguageCombo()
    {
        ImGui.SetNextItemWidth(130f);
        using var combo = ImRaii.Combo("Language / 语言", Loc.CurrentLanguageLabel());
        if (!combo) return;
        if (ImGui.Selectable(Loc.S.LangAuto, P.Config.Language == 0))
        {
            P.Config.Language = 0;
            P.Save();
            Loc.Invalidate();
        }
        if (ImGui.Selectable(Loc.S.LangChinese, P.Config.Language == 1))
        {
            P.Config.Language = 1;
            P.Save();
            Loc.Invalidate();
        }
        if (ImGui.Selectable(Loc.S.LangEnglish, P.Config.Language == 2))
        {
            P.Config.Language = 2;
            P.Save();
            Loc.Invalidate();
        }
    }

    private static bool MatchesFilter(StatusOverride x) =>
        Filter.Length == 0
     || x.StatusId.ToString().Contains(Filter)
     || UI.GetStatusName(x.StatusId).Contains(Filter, StringComparison.OrdinalIgnoreCase);

    private static void DrawListPanel(Strings s)
    {
        ImGui.SetNextItemWidth(-1f);
        ImGui.InputTextWithHint("##filter", s.FilterHint, ref Filter, 64);

        if (ImGui.BeginChild("##overrides", new(0, -ImGui.GetFrameHeight() * 2 - 4f)))
        {
            if (P.Config.Overrides.Count == 0)
            {
                ImGuiEx.Text(ImGuiColors.DalamudGrey, s.NoEntries);
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
                    DrawCategoryHeader(s, group.Key, group.Count(), index++);
                    if (!P.Config.CollapsedCategories.Contains(group.Key))
                    {
                        ImGui.Indent(10f);
                        foreach (var ov in group) DrawEntryRow(ov);
                        ImGui.Unindent(10f);
                    }
                }
                if (index == 0)
                    ImGuiEx.Text(ImGuiColors.DalamudGrey, s.NoMatches);
            }
        }
        ImGui.EndChild();

        if (ImGui.Button(s.AddStatus))
            StatusPicker.Open();
        ImGui.SameLine();
        if (ImGui.Button(s.ImportExport))
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

        if (ImGui.SmallButton(s.ExpandAll))
            P.Config.CollapsedCategories.Clear();
        ImGui.SameLine();
        if (ImGui.SmallButton(s.CollapseAll))
        {
            P.Config.CollapsedCategories.Clear();
            foreach (var cat in Grouped.Select(g => g.Key))
                P.Config.CollapsedCategories.Add(cat);
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(s.ListHint);
    }

    private static void DrawCategoryHeader(Strings s, string category, int count, int index)
    {
        var display = category.Length > 0 ? category : s.DefaultCategory;
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
            if (ImGui.MenuItem(s.RenameCategory))
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
        if (ThreadLoadImageHandler.TryGetIconTextureWrap(UI.GetStatusIcon(ov.StatusId), false, out var tex))
        {
            ImGui.Image(tex.Handle, new(20, 20));
            ImGui.SameLine();
        }
        var name = UI.GetStatusName(ov.StatusId);
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

    private static void DrawEditorPanel(Strings s)
    {
        var ov = SelectedOverride;
        if (ov == null)
        {
            ImGuiEx.Text(ImGuiColors.DalamudGrey, s.SelectToEdit);
            return;
        }

        if (ImGui.BeginTable("##editor", 2, ImGuiTableFlags.PadOuterX))
        {
            ImGui.TableSetupColumn("##label", ImGuiTableColumnFlags.WidthFixed, 110f);

            ImGui.TableNextColumn();
            ImGuiEx.TextV(s.StatusId);
            ImGui.TableNextColumn();
            var id = (int)ov.StatusId;
            ImGui.SetNextItemWidth(120f);
            if (ImGui.InputInt("##id", ref id))
            {
                var newId = (uint)Math.Max(0, id);
                if (newId != ov.StatusId && P.Config.Overrides.Any(x => x.StatusId == newId))
                {
                    Notify.Error(string.Format(s.DuplicateEntry, newId));
                }
                else
                {
                    ov.StatusId = newId;
                    Selected = newId;
                    P.Save();
                }
            }
            ImGui.SameLine();
            ImGuiEx.Text($"{UI.GetStatusName(ov.StatusId)}");

            ImGui.TableNextColumn();
            ImGuiEx.TextV(s.Category);
            ImGui.TableNextColumn();
            var cat = ov.Category;
            ImGui.SetNextItemWidth(-40f);
            if (ImGui.InputTextWithHint("##cat", s.CategoryHint, ref cat, 64))
            {
                ov.Category = cat.Trim();
                P.Save();
            }

            ImGui.TableNextColumn();
            ImGuiEx.TextV(s.Name);
            ImGui.TableNextColumn();
            var name = ov.Name;
            ImGui.SetNextItemWidth(-40f);
            if (ImGui.InputTextWithHint("##name", s.NameHint, ref name, 200))
            {
                ov.Name = name;
                P.Save();
            }
            BBCode.Parse(name, out var nameError);
            if (nameError.Length > 0)
                ImGuiEx.Text(ImGuiColors.DalamudRed, nameError);
            if (ThreadLoadImageHandler.TryGetIconTextureWrap(UI.GetStatusIcon(ov.StatusId), false, out var icon))
            {
                ImGui.SameLine();
                ImGui.Image(icon.Handle, new(28, 28));
            }

            ImGui.TableNextColumn();
            ImGuiEx.TextV(s.Description);
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

        if (ImGui.CollapsingHeader(s.FormattingHelpTitle, ImGuiTreeNodeFlags.DefaultOpen))
        {
            ImGuiEx.TextWrapped(s.FormattingHelp);
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

    public StatusPicker() : base($"{Loc.S.PickerTitle}##ste-picker")
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
        var s = Loc.S;
        ImGui.SetNextItemWidth(-1f);
        ImGui.InputTextWithHint("##picker-filter", s.PickerFilterHint, ref Filter, 64);

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

        ImGuiEx.Text(string.Format(s.PickerCount, matches.Count));
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
                    if (hasOverride && ImGui.IsItemHovered()) ImGui.SetTooltip(s.PickerHasOverride);
                }
            }
            clipper.End();
        }
        ImGui.EndChild();
    }
}

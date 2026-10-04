using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Windowing;
using ECommons.Interop;

namespace StatusTextEditor;

/// <summary> 导出 / 导入窗口：选择性地将覆盖条目以 JSON 分发。 </summary>
public class TransferWindow : Window
{
    // 导出
    private readonly HashSet<uint> ExportSelected = [];
    private readonly HashSet<string> ExportCollapsed = [];
    private bool ExportSelectionDirty = true;   // 打开窗口时全量填充选中集合

    // 导入
    private string ImportText = "";
    private ExportFormat? ParsedImport;
    private string ImportError = "";
    private bool ImportOverwrite;

    public TransferWindow() : base($"{Loc.S.TransferTitle}##ste-transfer")
    {
        this.SetMinSize(520, 420);
        EzConfigGui.WindowSystem.AddWindow(this);
    }

    public static void Open()
    {
        if (P.Transfer == null) P.Transfer = new();
        P.Transfer.IsOpen = true;
    }

    public override void OnOpen()
    {
        ExportSelectionDirty = true;
        ImportError = "";
        ParsedImport = null;
    }

    public override void Draw()
    {
        var s = Loc.S;
        if (ImGui.BeginTabBar("##transfer-tabs"))
        {
            if (ImGui.BeginTabItem(s.TabExport))
            {
                DrawExport(s);
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem(s.TabImport))
            {
                DrawImport(s);
                ImGui.EndTabItem();
            }
            ImGui.EndTabBar();
        }
    }

    #region 导出

    private void DrawExport(Strings s)
    {
        ImGuiEx.TextWrapped(s.ExportIntro);
        ImGui.Separator();

        // 选中集合与当前条目对齐：清理已删除的，打开窗口时默认全选。
        ExportSelected.RemoveWhere(id => !P.Config.Overrides.Any(x => x.StatusId == id));
        if (ExportSelectionDirty)
        {
            ExportSelected.Clear();
            foreach (var ov in P.Config.Overrides)
                ExportSelected.Add(ov.StatusId);
            ExportSelectionDirty = false;
        }

        if (ImGui.Button(s.SelectAll))
            foreach (var ov in P.Config.Overrides)
                ExportSelected.Add(ov.StatusId);
        ImGui.SameLine();
        if (ImGui.Button(s.SelectNone))
            ExportSelected.Clear();
        ImGui.SameLine();
        ImGuiEx.Text(ImGuiColors.DalamudGrey, string.Format(s.SelectedCount, ExportSelected.Count, P.Config.Overrides.Count));

        if (ImGui.BeginChild("##export-list", new(0, -ImGui.GetFrameHeightWithSpacing() * 2 - 4f)))
        {
            if (P.Config.Overrides.Count == 0)
            {
                ImGuiEx.Text(ImGuiColors.DalamudGrey, s.NothingToExport);
            }
            var gi = 0;
            foreach (var group in P.Config.Overrides.GroupBy(x => x.Category).ToList())
            {
                var display = group.Key.Length > 0 ? group.Key : s.DefaultCategory;
                var all = group.All(x => ExportSelected.Contains(x.StatusId));
                var any = group.Any(x => ExportSelected.Contains(x.StatusId));
                var cb = all;
                if (ImGui.Checkbox($"##excat{gi}", ref cb))
                {
                    if (cb) foreach (var x in group) ExportSelected.Add(x.StatusId);
                    else foreach (var x in group) ExportSelected.Remove(x.StatusId);
                }
                if (any && !all && ImGui.IsItemHovered())
                    ImGui.SetTooltip(s.PartialCategory);
                ImGui.SameLine();

                var collapsed = ExportCollapsed.Contains(group.Key);
                ImGui.SetNextItemOpen(!collapsed);
                var open = ImGui.CollapsingHeader($"{display} ({group.Count()})##excat-h{gi}");
                if (open == collapsed)
                {
                    if (open) ExportCollapsed.Remove(group.Key);
                    else ExportCollapsed.Add(group.Key);
                }

                if (open)
                {
                    ImGui.Indent(10f);
                    foreach (var ov in group)
                    {
                        var has = ExportSelected.Contains(ov.StatusId);
                        if (ImGui.Checkbox($"##ex{ov.StatusId}", ref has))
                        {
                            if (has) ExportSelected.Add(ov.StatusId);
                            else ExportSelected.Remove(ov.StatusId);
                        }
                        ImGui.SameLine();
                        ImGuiEx.Text($"{ov.StatusId}  {UI.GetStatusName(ov.StatusId)}");
                    }
                    ImGui.Unindent(10f);
                }
                gi++;
            }
        }
        ImGui.EndChild();

        if (ImGui.Button(s.ExportToFile))
        {
            OpenFileDialog.SelectFile(
                ofn => new TickScheduler(() => WriteExportFile(ofn.file, s)),
                null,
                null,
                s.ChooseExportLocation,
                [(s.JsonFile, new[] { "json" })],
                save: true);
        }
        ImGui.SameLine();
        if (ImGui.Button(s.CopyClipboard))
        {
            ImGui.SetClipboardText(BuildJson());
            Notify.Success(string.Format(s.CopiedClipboard, ExportSelected.Count));
        }
    }

    private List<StatusOverride> SelectedEntries =>
        P.Config.Overrides.Where(x => ExportSelected.Contains(x.StatusId)).ToList();

    private string BuildJson()
    {
        var export = new ExportFormat { Entries = SelectedEntries };
        return JsonSerializer.Serialize(export, new JsonSerializerOptions { WriteIndented = true, IncludeFields = true });
    }

    private void WriteExportFile(string path, Strings s)
    {
        try
        {
            if (path.IsNullOrEmpty()) return;
            if (!path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) path += ".json";
            File.WriteAllText(path, BuildJson());
            Notify.Success(string.Format(s.ExportedFile, ExportSelected.Count, path));
        }
        catch (Exception e)
        {
            Notify.Error(string.Format(s.ExportFailed, e.Message));
        }
    }

    #endregion

    #region 导入

    private void DrawImport(Strings s)
    {
        ImGuiEx.TextWrapped(s.ImportIntro);
        ImGui.Separator();

        if (ImGui.Button(s.LoadFromFile))
        {
            OpenFileDialog.SelectFile(
                ofn => new TickScheduler(() =>
                {
                    try
                    {
                        ImportText = File.ReadAllText(ofn.file);
                        ParseImport(s);
                    }
                    catch (Exception e)
                    {
                        ImportError = string.Format(s.ReadFileFailed, e.Message);
                    }
                }),
                null,
                null,
                s.ChooseJsonFile,
                [(s.JsonFile, new[] { "json" })]);
        }
        ImGui.SameLine();
        if (ImGui.Button(s.ParseClipboard))
        {
            ImportText = ImGui.GetClipboardText();
            ParseImport(s);
        }

        var text = ImportText;
        ImGui.SetNextItemWidth(-1f);
        if (ImGui.InputTextMultiline("##import-text", ref text, 100_000, new(0, 120), ImGuiInputTextFlags.AllowTabInput))
        {
            ImportText = text;
            ParsedImport = null;
            ImportError = "";
        }

        if (ParsedImport != null)
        {
            var entries = ParsedImport.Entries;
            var conflicts = entries.Count(x => P.Config.Overrides.Any(y => y.StatusId == x.StatusId));
            ImGuiEx.Text(ImGuiColors.ParsedGreen, string.Format(s.ImportParsed, entries.Count, conflicts));
            ImGui.Checkbox(s.ImportOverwriteHint, ref ImportOverwrite);

            if (ImGui.Button(s.ExecuteImport))
            {
                var added = 0;
                var overwritten = 0;
                foreach (var e in entries)
                {
                    var existing = P.Config.Overrides.FirstOrDefault(y => y.StatusId == e.StatusId);
                    if (existing != null)
                    {
                        if (!ImportOverwrite) continue;
                        existing.Name = e.Name;
                        existing.Description = e.Description;
                        existing.Category = e.Category;
                        overwritten++;
                    }
                    else
                    {
                        P.Config.Overrides.Add(new StatusOverride
                        {
                            StatusId = e.StatusId,
                            Name = e.Name,
                            Description = e.Description,
                            Category = e.Category,
                        });
                        added++;
                    }
                }
                P.Save();
                Notify.Success(string.Format(s.ImportDone, added, overwritten));
                ParsedImport = null;
                ImportText = "";
            }
        }
        else if (ImportError.Length > 0)
        {
            ImGuiEx.Text(ImGuiColors.DalamudRed, ImportError);
        }
    }

    private void ParseImport(Strings s)
    {
        ImportError = "";
        ParsedImport = null;
        if (ImportText.Trim().Length == 0)
        {
            ImportError = s.ImportEmpty;
            return;
        }
        try
        {
            var parsed = JsonSerializer.Deserialize<ExportFormat>(ImportText, new JsonSerializerOptions { IncludeFields = true });
            if (parsed?.Entries is not { Count: > 0 })
            {
                ImportError = s.ImportNoEntries;
                return;
            }
            ParsedImport = parsed;
        }
        catch (Exception e)
        {
            ImportError = string.Format(s.ImportParseFailed, e.Message);
        }
    }

    #endregion
}

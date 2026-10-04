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

    public TransferWindow() : base("导入 / 导出##ste-transfer")
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
        if (ImGui.BeginTabBar("##transfer-tabs"))
        {
            if (ImGui.BeginTabItem("导出"))
            {
                DrawExport();
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("导入"))
            {
                DrawImport();
                ImGui.EndTabItem();
            }
            ImGui.EndTabBar();
        }
    }

    #region 导出

    private void DrawExport()
    {
        ImGuiEx.TextWrapped("勾选需要分发的条目，导出为 JSON 文件（或复制到剪贴板直接发给对方，对方在“导入”页粘贴即可）。分类标题上的勾选框可整类批量勾选。");
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

        if (ImGui.Button("全选"))
            foreach (var ov in P.Config.Overrides)
                ExportSelected.Add(ov.StatusId);
        ImGui.SameLine();
        if (ImGui.Button("全不选"))
            ExportSelected.Clear();
        ImGui.SameLine();
        ImGuiEx.Text(ImGuiColors.DalamudGrey, $"已选 {ExportSelected.Count} / {P.Config.Overrides.Count}");

        if (ImGui.BeginChild("##export-list", new(0, -ImGui.GetFrameHeightWithSpacing() * 2 - 4f)))
        {
            if (P.Config.Overrides.Count == 0)
            {
                ImGuiEx.Text(ImGuiColors.DalamudGrey, "暂无条目可导出。");
            }
            var gi = 0;
            foreach (var group in P.Config.Overrides.GroupBy(x => x.Category).ToList())
            {
                var display = group.Key.Length > 0 ? group.Key : "默认";
                var all = group.All(x => ExportSelected.Contains(x.StatusId));
                var any = group.Any(x => ExportSelected.Contains(x.StatusId));
                var cb = all;
                if (ImGui.Checkbox($"##excat{gi}", ref cb))
                {
                    if (cb) foreach (var x in group) ExportSelected.Add(x.StatusId);
                    else foreach (var x in group) ExportSelected.Remove(x.StatusId);
                }
                if (any && !all && ImGui.IsItemHovered())
                    ImGui.SetTooltip("该分类已部分勾选，点击勾选框 = 取消整类；再点 = 全选整类。");
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

        if (ImGui.Button("导出到文件…"))
        {
            OpenFileDialog.SelectFile(
                ofn => new TickScheduler(() => WriteExportFile(ofn.file)),
                null,
                null,
                "选择导出位置",
                [("JSON 文件", new[] { "json" })],
                save: true);
        }
        ImGui.SameLine();
        if (ImGui.Button("复制到剪贴板"))
        {
            ImGui.SetClipboardText(BuildJson());
            Notify.Success($"已复制 {ExportSelected.Count} 条覆盖到剪贴板。");
        }
    }

    private List<StatusOverride> SelectedEntries =>
        P.Config.Overrides.Where(x => ExportSelected.Contains(x.StatusId)).ToList();

    private string BuildJson()
    {
        var export = new ExportFormat { Entries = SelectedEntries };
        return JsonSerializer.Serialize(export, new JsonSerializerOptions { WriteIndented = true, IncludeFields = true });
    }

    private void WriteExportFile(string path)
    {
        try
        {
            if (path.IsNullOrEmpty()) return;
            if (!path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) path += ".json";
            File.WriteAllText(path, BuildJson());
            Notify.Success($"已导出 {ExportSelected.Count} 条覆盖到 {path}");
        }
        catch (Exception e)
        {
            Notify.Error($"导出失败: {e.Message}");
        }
    }

    #endregion

    #region 导入

    private void DrawImport()
    {
        ImGuiEx.TextWrapped("粘贴他人分享的 JSON 文本，或从文件加载；选择冲突处理方式后执行导入。");
        ImGui.Separator();

        if (ImGui.Button("从文件加载…"))
        {
            OpenFileDialog.SelectFile(
                ofn => new TickScheduler(() =>
                {
                    try
                    {
                        ImportText = File.ReadAllText(ofn.file);
                        ParseImport();
                    }
                    catch (Exception e)
                    {
                        ImportError = $"读取文件失败: {e.Message}";
                    }
                }),
                null,
                null,
                "选择 JSON 文件",
                [("JSON 文件", new[] { "json" })]);
        }
        ImGui.SameLine();
        if (ImGui.Button("解析剪贴板"))
        {
            ImportText = ImGui.GetClipboardText();
            ParseImport();
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
            ImGuiEx.Text(ImGuiColors.ParsedGreen, $"解析成功：{entries.Count} 条覆盖，其中 {conflicts} 条与现有条目冲突。");
            ImGui.Checkbox("覆盖已存在的条目（不勾选则跳过冲突项）", ref ImportOverwrite);

            if (ImGui.Button("执行导入"))
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
                Notify.Success($"导入完成：新增 {added} 条，覆盖 {overwritten} 条。");
                ParsedImport = null;
                ImportText = "";
            }
        }
        else if (ImportError.Length > 0)
        {
            ImGuiEx.Text(ImGuiColors.DalamudRed, ImportError);
        }
    }

    private void ParseImport()
    {
        ImportError = "";
        ParsedImport = null;
        if (ImportText.Trim().Length == 0)
        {
            ImportError = "内容为空。";
            return;
        }
        try
        {
            var parsed = JsonSerializer.Deserialize<ExportFormat>(ImportText, new JsonSerializerOptions { IncludeFields = true });
            if (parsed?.Entries is not { Count: > 0 })
            {
                ImportError = "未找到任何覆盖条目（Entries 为空）。";
                return;
            }
            ParsedImport = parsed;
        }
        catch (Exception e)
        {
            ImportError = $"JSON 解析失败: {e.Message}";
        }
    }

    #endregion
}

using Lumina.Data;
using Lumina.Data.Files.Excel;

namespace StatusTextEditor;

public enum PluginLanguage
{
    Auto = 0,
    Chinese = 1,
    English = 2,
}

/// <summary>
///     双语 UI 文案。Auto 规则：游戏 Status 表声明为简体中文（国服客户端）→ 中文，
///     其余（国际服日/英/德/法）→ English；可在设置里手动固定。
/// </summary>
public static class Loc
{
    private static bool? _chineseClient;
    private static Strings? _current;

    public static Strings S => _current ??= Strings.Make(IsChinese);

    /// <summary> 当前解析出的语言（供标题等一次性创建的 UI 使用）。 </summary>
    public static bool IsChinese
    {
        get
        {
            if (_chineseClient == null)
            {
                try
                {
                    _chineseClient = Setting switch
                    {
                        1 => true,
                        2 => false,
                        _ => DetectChineseClient(),
                    };
                }
                catch
                {
                    _chineseClient = true;
                }
            }
            return _chineseClient.Value;
        }
    }

    private static int Setting => P.Config?.Language ?? 0;

    /// <summary> 语言设置或客户端变化后调用，使文案表重新解析。 </summary>
    public static void Invalidate()
    {
        _chineseClient = null;
        _current = null;
    }

    /// <summary> 国服客户端的 Status.exh 声明为 ChineseSimplified；国际服没有该语言变体。 </summary>
    private static bool DetectChineseClient()
    {
        var exh = Svc.Data.GetFile<ExcelHeaderFile>("exd/status.exh");
        return exh?.Languages.Contains(Language.ChineseSimplified) ?? false;
    }

    /// <summary> 语言下拉框的当前显示文本。 </summary>
    public static string CurrentLanguageLabel()
    {
        var s = S;
        return Setting switch
        {
            1 => s.LangChinese,
            2 => s.LangEnglish,
            _ => IsChinese ? $"{s.LangAuto}（{s.LangChinese}）" : $"{s.LangAuto} (English)",
        };
    }
}

public class Strings
{
    // 主窗口
    public string EnableOverrides = "";
    public string HookActive = "";
    public string HookMissing = "";
    public string LocalOnly = "";
    public string FilterHint = "";
    public string NoEntries = "";
    public string NoMatches = "";
    public string AddStatus = "";
    public string ImportExport = "";
    public string DeleteSelected = "";
    public string ExpandAll = "";
    public string CollapseAll = "";
    public string ListHint = "";
    public string DefaultCategory = "";
    public string RenameCategory = "";
    public string SelectToEdit = "";
    public string StatusId = "";
    public string DuplicateEntry = "";
    public string Category = "";
    public string CategoryHint = "";
    public string Name = "";
    public string NameHint = "";
    public string Description = "";
    public string FormattingHelpTitle = "";
    public string FormattingHelp = "";

    // 状态选择器
    public string PickerTitle = "";
    public string PickerFilterHint = "";
    public string PickerCount = "";
    public string PickerHasOverride = "";

    // 导入 / 导出
    public string TransferTitle = "";
    public string TabExport = "";
    public string TabImport = "";
    public string ExportIntro = "";
    public string SelectAll = "";
    public string SelectNone = "";
    public string SelectedCount = "";
    public string NothingToExport = "";
    public string PartialCategory = "";
    public string ExportToFile = "";
    public string CopyClipboard = "";
    public string CopiedClipboard = "";
    public string ExportedFile = "";
    public string ExportFailed = "";
    public string ChooseExportLocation = "";
    public string JsonFile = "";
    public string ImportIntro = "";
    public string LoadFromFile = "";
    public string ParseClipboard = "";
    public string ImportEmpty = "";
    public string ImportNoEntries = "";
    public string ImportParseFailed = "";
    public string ImportParsed = "";
    public string ImportOverwriteHint = "";
    public string ExecuteImport = "";
    public string ImportDone = "";
    public string ReadFileFailed = "";
    public string ChooseJsonFile = "";

    // BBCode 错误
    public string BbMismatch = "";
    public string BbColorInvalid = "";
    public string BbSyntax = "";

    // 语言选项
    public string LangAuto = "";
    public string LangChinese = "";
    public string LangEnglish = "";

    public static Strings Make(bool zh) => zh ? Chinese() : English();

    private static Strings Chinese() => new()
    {
        EnableOverrides = "启用文本覆盖",
        HookActive = "状态行接口已挂钩",
        HookMissing = "未找到状态行接口签名，此客户端不可用",
        LocalOnly = "修改即时生效，仅本机显示",
        FilterHint = "筛选（名称 / ID）",
        NoEntries = "暂无条目。点击下方“添加状态”从状态列表中选择。",
        NoMatches = "没有符合筛选条件的条目。",
        AddStatus = "+ 添加状态",
        ImportExport = "导入/导出",
        DeleteSelected = "删除所选条目",
        ExpandAll = "全部展开",
        CollapseAll = "全部折叠",
        ListHint = "拖动条目可排序；拖到分组标题上可移动分类；右键分组标题可重命名分类。",
        DefaultCategory = "默认",
        RenameCategory = "重命名分类",
        SelectToEdit = "从左侧选择一个条目进行编辑。",
        StatusId = "状态 ID",
        DuplicateEntry = "状态 {0} 已存在条目。",
        Category = "分类",
        CategoryHint = "留空 = 默认分类",
        Name = "名称",
        NameHint = "留空保持原版",
        Description = "描述",
        FormattingHelpTitle = "格式化标签说明",
        FormattingHelp = """
            名称与描述均支持格式化标签（原生 tooltip 同样生效）：
            [color=Red]…[/color]、[color=31]…[/color] - 彩色文字
            [glow=LightBlue]…[/glow]、[glow=数值]…[/glow] - 发光文字轮廓
            可用颜色名称：
            WhiteNormal, White, Grey1, Grey2, Grey3, Grey4, Yellow, Black, LightYellow, Red, DarkRed,
            Green, DarkGreen, WarmSeaBlue, Orange, LightBlue, Gold, DarkBlue, LightGreen, Pink
            更多颜色可用 “/xldata uicolor” 指令查询数值后以 [color=数值] 使用
            [i]…[/i] - 斜体文字
            """,
        PickerTitle = "选择状态",
        PickerFilterHint = "筛选（名称 / ID）",
        PickerCount = "共 {0} 个状态（左键选择并添加覆盖）",
        PickerHasOverride = "该状态已存在覆盖条目，点击直接选中。",
        TransferTitle = "导入 / 导出",
        TabExport = "导出",
        TabImport = "导入",
        ExportIntro = "勾选需要分发的条目，导出为 JSON 文件（或复制到剪贴板直接发给对方，对方在“导入”页粘贴即可）。分类标题上的勾选框可整类批量勾选。",
        SelectAll = "全选",
        SelectNone = "全不选",
        SelectedCount = "已选 {0} / {1}",
        NothingToExport = "暂无条目可导出。",
        PartialCategory = "该分类已部分勾选，点击勾选框 = 取消整类；再点 = 全选整类。",
        ExportToFile = "导出到文件…",
        CopyClipboard = "复制到剪贴板",
        CopiedClipboard = "已复制 {0} 条覆盖到剪贴板。",
        ExportedFile = "已导出 {0} 条覆盖到 {1}",
        ExportFailed = "导出失败: {0}",
        ChooseExportLocation = "选择导出位置",
        JsonFile = "JSON 文件",
        ImportIntro = "粘贴他人分享的 JSON 文本，或从文件加载；选择冲突处理方式后执行导入。",
        LoadFromFile = "从文件加载…",
        ParseClipboard = "解析剪贴板",
        ImportEmpty = "内容为空。",
        ImportNoEntries = "未找到任何覆盖条目（Entries 为空）。",
        ImportParseFailed = "JSON 解析失败: {0}",
        ImportParsed = "解析成功：{0} 条覆盖，其中 {1} 条与现有条目冲突。",
        ImportOverwriteHint = "覆盖已存在的条目（不勾选则跳过冲突项）",
        ExecuteImport = "执行导入",
        ImportDone = "导入完成：新增 {0} 条，覆盖 {1} 条。",
        ReadFileFailed = "读取文件失败: {0}",
        ChooseJsonFile = "选择 JSON 文件",
        BbMismatch = "错误：开始与结束标签不匹配。",
        BbColorInvalid = "错误：颜色名称或数值无效。",
        BbSyntax = "错误：请检查语法。",
        LangAuto = "自动",
        LangChinese = "中文",
        LangEnglish = "English",
    };

    private static Strings English() => new()
    {
        EnableOverrides = "Enable text overrides",
        HookActive = "Status row hook active",
        HookMissing = "Signature not found - text patching unavailable on this client",
        LocalOnly = "Applies instantly; local display only",
        FilterHint = "Filter (name / ID)",
        NoEntries = "No entries yet. Click \"+ Add status\" below to pick one from the status list.",
        NoMatches = "No entries match the filter.",
        AddStatus = "+ Add status",
        ImportExport = "Import/Export",
        DeleteSelected = "Delete selected entry",
        ExpandAll = "Expand all",
        CollapseAll = "Collapse all",
        ListHint = "Drag entries to reorder; drop onto a group header to recategorize; right-click a header to rename it.",
        DefaultCategory = "Default",
        RenameCategory = "Rename category",
        SelectToEdit = "Select an entry on the left to edit.",
        StatusId = "Status ID",
        DuplicateEntry = "Status {0} already has an entry.",
        Category = "Category",
        CategoryHint = "Empty = default category",
        Name = "Name",
        NameHint = "Empty = keep original",
        Description = "Description",
        FormattingHelpTitle = "Formatting tags",
        FormattingHelp = """
            Both name and description support formatting tags (rendered in the native tooltip):
            [color=Red]...[/color], [color=31]...[/color] - colored text
            [glow=LightBlue]...[/glow], [glow=value]...[/glow] - glowing outline
            Available color names:
            WhiteNormal, White, Grey1, Grey2, Grey3, Grey4, Yellow, Black, LightYellow, Red, DarkRed,
            Green, DarkGreen, WarmSeaBlue, Orange, LightBlue, Gold, DarkBlue, LightGreen, Pink
            For more colors run "/xldata uicolor" and use [color=value]
            [i]...[/i] - italic text
            """,
        PickerTitle = "Pick a Status",
        PickerFilterHint = "Filter (name / ID)",
        PickerCount = "{0} statuses (click to add or select an override)",
        PickerHasOverride = "An override already exists; clicking selects it.",
        TransferTitle = "Import / Export",
        TabExport = "Export",
        TabImport = "Import",
        ExportIntro = "Tick the entries to share, then export as a JSON file (or copy to clipboard and let the other person paste it on the Import tab). Category header checkboxes select whole categories at once.",
        SelectAll = "Select all",
        SelectNone = "Select none",
        SelectedCount = "Selected {0} / {1}",
        NothingToExport = "Nothing to export.",
        PartialCategory = "Partially selected: clicking the checkbox clears the whole category, clicking again selects it.",
        ExportToFile = "Export to file...",
        CopyClipboard = "Copy to clipboard",
        CopiedClipboard = "Copied {0} overrides to the clipboard.",
        ExportedFile = "Exported {0} overrides to {1}",
        ExportFailed = "Export failed: {0}",
        ChooseExportLocation = "Choose export location",
        JsonFile = "JSON file",
        ImportIntro = "Paste shared JSON text or load it from a file, choose how to handle conflicts, then import.",
        LoadFromFile = "Load from file...",
        ParseClipboard = "Parse clipboard",
        ImportEmpty = "Content is empty.",
        ImportNoEntries = "No override entries found (Entries is empty).",
        ImportParseFailed = "JSON parse failed: {0}",
        ImportParsed = "Parsed {0} overrides; {1} conflict with existing entries.",
        ImportOverwriteHint = "Overwrite existing entries (uncheck to skip conflicts)",
        ExecuteImport = "Import",
        ImportDone = "Import done: {0} added, {1} overwritten.",
        ReadFileFailed = "Failed to read file: {0}",
        ChooseJsonFile = "Choose a JSON file",
        BbMismatch = "Error: mismatched open/close tags.",
        BbColorInvalid = "Error: invalid color name or value.",
        BbSyntax = "Error: please check the syntax.",
        LangAuto = "Auto",
        LangChinese = "中文",
        LangEnglish = "English",
    };
}

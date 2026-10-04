using Dalamud.Game.Text.SeStringHandling;

namespace StatusTextEditor;

/// <summary> 单个原生状态的文本覆盖条目。 </summary>
public class StatusOverride
{
    public uint StatusId;

    /// <summary> 替换名称；留空保持原版。 </summary>
    public string Name = "";

    /// <summary> 替换描述；留空保持原版。支持格式化标签（[color]/[glow]/[i]）。 </summary>
    public string Description = "";

    /// <summary> 分类名；空 = 默认分类。 </summary>
    public string Category = "";

    public bool IsEmpty => Name.Length == 0 && Description.Length == 0;
}

/// <summary> 导出/导入用的条目集合格式。 </summary>
public class ExportFormat
{
    public int Format = 1;
    public string Type = "StatusTextEditor.Export";
    public List<StatusOverride> Entries = [];
}

public class Config
{
    public bool Enabled = true;
    public List<StatusOverride> Overrides = [];

    /// <summary> 界面语言：0 = 自动（按游戏表语言），1 = 中文，2 = English。 </summary>
    public int Language = 0;

    /// <summary> 记忆列表里处于折叠状态的分类。 </summary>
    public HashSet<string> CollapsedCategories = [];
}

public class StatusTextEditorPlugin : IDalamudPlugin
{
    public static StatusTextEditorPlugin P;
    public Config Config;
    public StatusTextPatcher Patcher;
    public StatusPicker? Picker;
    public TransferWindow? Transfer;

    /// <summary> 将拖拽条目移动到目标条目所在位置（重排序）。 </summary>
    public void MoveEntry(uint draggedId, uint targetId)
    {
        if (draggedId == targetId) return;
        var item = Config.Overrides.FirstOrDefault(x => x.StatusId == draggedId);
        if (item == null) return;
        Config.Overrides.Remove(item);
        var ti = Config.Overrides.FindIndex(x => x.StatusId == targetId);
        Config.Overrides.Insert(ti >= 0 ? ti : Config.Overrides.Count, item);
        Save();
    }

    /// <summary> 将拖拽条目移动到目标分类的末尾。 </summary>
    public void MoveToCategory(uint draggedId, string category)
    {
        var item = Config.Overrides.FirstOrDefault(x => x.StatusId == draggedId);
        if (item == null || item.Category == category) return;
        item.Category = category;
        // 移到该分类现有条目之后，视觉上落在分组末尾。
        Config.Overrides.Remove(item);
        var insertAt = Config.Overrides.Count;
        for (var i = Config.Overrides.Count - 1; i >= 0; i--)
        {
            if (Config.Overrides[i].Category == category) { insertAt = i + 1; break; }
        }
        Config.Overrides.Insert(insertAt, item);
        Save();
    }

    /// <summary> 批量重命名分类。 </summary>
    public void RenameCategory(string oldName, string newName)
    {
        foreach (var ov in Config.Overrides)
            if (ov.Category == oldName)
                ov.Category = newName;
        Config.CollapsedCategories.Remove(oldName);
        Save();
    }

    /// <summary> 选中指定状态的覆盖条目，不存在则创建空条目。 </summary>
    public void AddOrSelect(uint statusId, string category = "")
    {
        var ov = Config.Overrides.FirstOrDefault(x => x.StatusId == statusId);
        if (ov == null)
        {
            ov = new StatusOverride { StatusId = statusId, Category = category };
            Config.Overrides.Add(ov);
            Save();
        }
        UI.Selected = statusId;
    }

    private string ConfigPath => Path.Combine(Svc.PluginInterface.GetPluginConfigDirectory(), "StatusTextEditor.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        IncludeFields = true,
    };

    public StatusTextEditorPlugin(IDalamudPluginInterface pi)
    {
        P = this;
        ECommonsMain.Init(pi, this);
        new TickScheduler(() =>
        {
            Config = Load();
            Patcher = new(Config);
            EzConfigGui.Init(UI.Draw);
            EzConfigGui.Window.SetMinSize(720, 480);
            EzCmd.Add("/statustext", ToggleUi, "打开状态文本编辑器");
            EzCmd.Add("/ste", ToggleUi, "打开状态文本编辑器");
            PluginLog.Information($"[StatusTextEditor] 初始化完成，已加载 {Config.Overrides.Count} 条覆盖。");
        });
    }

    private void ToggleUi(string _, string __)
    {
        if (EzConfigGui.Window is { } window)
            window.IsOpen = !EzConfigGui.Window.IsOpen;
    }

    public void Save()
    {
        try
        {
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(Config, JsonOptions));
        }
        catch (Exception e)
        {
            PluginLog.Error($"[StatusTextEditor] 配置保存失败: {e.Message}");
        }
        // 已重建的缓冲区需要按当前配置重新生成，修改才能立即生效。
        Patcher?.UpdateConfig(Config);
    }

    private Config Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
                return JsonSerializer.Deserialize<Config>(File.ReadAllText(ConfigPath), JsonOptions) ?? new();
        }
        catch (Exception e)
        {
            PluginLog.Error($"[StatusTextEditor] 配置读取失败: {e.Message}");
        }
        return new();
    }

    public void Dispose()
    {
        Patcher?.Dispose();
        ECommonsMain.Dispose();
        P = null!;
    }
}

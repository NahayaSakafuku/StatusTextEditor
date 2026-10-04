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

    public bool IsEmpty => Name.Length == 0 && Description.Length == 0;
}

public class Config
{
    public bool Enabled = true;
    public List<StatusOverride> Overrides = [];
}

public class StatusTextEditorPlugin : IDalamudPlugin
{
    public static StatusTextEditorPlugin P;
    public Config Config;
    public StatusTextPatcher Patcher;
    public StatusPicker? Picker;

    /// <summary> 选中指定状态的覆盖条目，不存在则创建空条目。 </summary>
    public void AddOrSelect(uint statusId)
    {
        var ov = Config.Overrides.FirstOrDefault(x => x.StatusId == statusId);
        if (ov == null)
        {
            ov = new StatusOverride { StatusId = statusId };
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

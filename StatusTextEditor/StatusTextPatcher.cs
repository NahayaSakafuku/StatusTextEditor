using System.Runtime.InteropServices;
using Dalamud.Hooking;

namespace StatusTextEditor;

/// <summary>
///     通过 hook 游戏内部“按 StatusId 取状态行数据”的接口热修改原生状态文本。
///     返回的缓冲区布局（经 Namingway 1.1.22 反编译与本机客户端验证）：
///       data[0] (byte) = 名称 C 字符串的偏移
///       data[4] (byte) = 描述 C 字符串的偏移 - 4
///     重建缓冲区并替换返回指针，原版数据不动。
/// </summary>
public unsafe class StatusTextPatcher : IDisposable
{
    private const string GetStatusRowSignature = "E8 ?? ?? ?? ?? 48 85 C0 74 96";

    private delegate nint GetStatusRowDelegate(uint statusId);

    private Hook<GetStatusRowDelegate>? _hook;
    private readonly Dictionary<uint, nint> _rebuilt = [];
    private Config _config;

    /// <summary> 签名是否命中、补丁是否生效。 </summary>
    public bool Active { get; private set; }

    public StatusTextPatcher(Config config)
    {
        _config = config;
        try
        {
            if (Svc.SigScanner.TryScanText(GetStatusRowSignature, out var ptr))
            {
                _hook = Svc.Hook.HookFromAddress<GetStatusRowDelegate>(ptr, Detour);
                _hook.Enable();
                Active = true;
                PluginLog.Information($"[StatusTextEditor] 状态行接口已挂钩: {ptr:X16}");
            }
            else
            {
                PluginLog.Warning("[StatusTextEditor] 未找到状态行接口签名，本客户端无法热修改表文本。");
            }
        }
        catch (Exception e)
        {
            PluginLog.Error($"[StatusTextEditor] 初始化状态行补丁失败: {e}");
        }
    }

    public void UpdateConfig(Config config)
    {
        _config = config;
        InvalidateCache();
    }

    /// <summary> 丢弃全部已重建缓冲区，下次查询按当前配置重建。 </summary>
    public void InvalidateCache()
    {
        foreach (var ptr in _rebuilt.Values)
            Marshal.FreeHGlobal(ptr);
        _rebuilt.Clear();
    }

    private nint Detour(uint statusId)
    {
        var original = _hook!.Original(statusId);
        try
        {
            return DetourInner(statusId, original);
        }
        catch (Exception e)
        {
            PluginLog.Error($"[StatusTextEditor] 状态 {statusId} 文本补丁失败: {e}");
            return original;
        }
    }

    private nint DetourInner(uint statusId, nint original)
    {
        if (original == nint.Zero) return original;
        if (!_config.Enabled) return original;
        if (!HasOverride(statusId)) return original;
        if (_rebuilt.TryGetValue(statusId, out var cached)) return cached;

        var nameOffset = Marshal.ReadByte(original);            // data[0]
        if (nameOffset < 12) return original;                   // 与 Namingway 相同的安全下限

        var descOffset = Marshal.ReadByte(original + 4) + 4;    // data[4] + 4
        var originalName = ReadCString(original + nameOffset);
        var originalDesc = ReadCString(original + descOffset);

        var ov = GetOverride(statusId)!;
        // Name goes through the BBCode parser like the description so formatting
        // tags render in the native tooltip (the buffer is parsed as SeString).
        var newName = ov.Name.Length > 0 ? StatusTextEditor.BBCode.Parse(ov.Name).Encode() : originalName;
        var maxNameLen = 254 - nameOffset;                      // data[4] stores the desc offset in a single byte
        if (newName.Length > maxNameLen) Array.Resize(ref newName, maxNameLen);

        var newDesc = ov.Description.Length > 0 ? StatusTextEditor.BBCode.Parse(ov.Description).Encode() : originalDesc;
        if (newDesc.Length > 1024) Array.Resize(ref newDesc, 1024);

        var newDescOffset = nameOffset + newName.Length + 1;
        var total = newDescOffset + newDesc.Length + 1;
        var buffer = Marshal.AllocHGlobal(total);

        CopyBytes(original, buffer, nameOffset);                // 原始头部
        Marshal.WriteByte(buffer + 4, (byte)(newDescOffset - 4));
        WriteBytes(buffer + nameOffset, newName);
        Marshal.WriteByte(buffer + nameOffset + newName.Length, 0);
        WriteBytes(buffer + newDescOffset, newDesc);
        Marshal.WriteByte(buffer + newDescOffset + newDesc.Length, 0);

        _rebuilt[statusId] = buffer;
        return buffer;
    }

    private bool HasOverride(uint statusId)
    {
        foreach (var o in _config.Overrides)
            if (o.StatusId == statusId && !o.IsEmpty)
                return true;
        return false;
    }

    private StatusOverride? GetOverride(uint statusId)
    {
        foreach (var o in _config.Overrides)
            if (o.StatusId == statusId)
                return o;
        return null;
    }

    private static byte[] ReadCString(nint ptr)
    {
        var len = 0;
        while (Marshal.ReadByte(ptr + len) != 0) len++;
        var bytes = new byte[len];
        Marshal.Copy(ptr, bytes, 0, len);
        return bytes;
    }

    private static void CopyBytes(nint src, nint dst, int len)
    {
        var bytes = new byte[len];
        Marshal.Copy(src, bytes, 0, len);
        Marshal.Copy(bytes, 0, dst, len);
    }

    private static void WriteBytes(nint dst, byte[] bytes) => Marshal.Copy(bytes, 0, dst, bytes.Length);

    public void Dispose()
    {
        _hook?.Dispose();
        InvalidateCache();
    }
}

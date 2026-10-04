using System.Text.RegularExpressions;
using Dalamud.Game.Text.SeStringHandling;
using ECommons.ChatMethods;
using Lumina.Excel.Sheets;
using UIColor = ECommons.ChatMethods.UIColor;

namespace StatusTextEditor;

/// <summary>
///     Moodles 风格的格式化标签解析器：
///     [color=名称或数值]...[/color]、[glow=...]...[/glow]、[i]...[/i]
/// </summary>
public static partial class BBCode
{
    public static SeString Parse(string text, bool nullTerminator = false) => Parse(text, out _, nullTerminator);

    public static SeString Parse(string text, out string error, bool nullTerminator = false)
    {
        try
        {
            error = "";
            var result = SplitRegex().Split(text);
            var str = new SeStringBuilder();
            int[] valid = [0, 0, 0];
            foreach (var s in result)
            {
                if (s == string.Empty) continue;
                if (s.StartsWith("[color=", StringComparison.OrdinalIgnoreCase))
                {
                    var success = ushort.TryParse(s[7..^1], out var r);
                    if (!success)
                    {
                        r = (ushort)Enum.GetValues<UIColor>().FirstOrDefault(x => x.ToString().EqualsIgnoreCase(s[7..^1]));
                    }
                    if (r == 0 || Svc.Data.GetExcelSheet<Lumina.Excel.Sheets.UIColor>().GetRowOrDefault(r) == null) goto ColorError;
                    str.AddUiForeground(r);
                    valid[0]++;
                }
                else if (s.Equals("[/color]", StringComparison.OrdinalIgnoreCase))
                {
                    str.AddUiForegroundOff();
                    if (valid[0] <= 0) goto ParseError;
                    valid[0]--;
                }
                else if (s.StartsWith("[glow=", StringComparison.OrdinalIgnoreCase))
                {
                    var success = ushort.TryParse(s[6..^1], out var r);
                    if (!success)
                    {
                        r = (ushort)Enum.GetValues<UIColor>().FirstOrDefault(x => x.ToString().EqualsIgnoreCase(s[6..^1]));
                    }
                    if (r == 0 || Svc.Data.GetExcelSheet<Lumina.Excel.Sheets.UIColor>().GetRowOrDefault(r) == null) goto ColorError;
                    str.AddUiGlow(r);
                    valid[1]++;
                }
                else if (s.Equals("[/glow]", StringComparison.OrdinalIgnoreCase))
                {
                    str.AddUiGlowOff();
                    if (valid[1] <= 0) goto ParseError;
                    valid[1]--;
                }
                else if (s.Equals("[i]", StringComparison.OrdinalIgnoreCase))
                {
                    str.AddItalicsOn();
                    valid[2]++;
                }
                else if (s.Equals("[/i]", StringComparison.OrdinalIgnoreCase))
                {
                    str.AddItalicsOff();
                    if (valid[2] <= 0) goto ParseError;
                    valid[2]--;
                }
                else
                {
                    str.AddText(s);
                }
            }
            if (!valid.All(x => x == 0)) goto ParseError;
            if (nullTerminator) str.AddText("\0");
            return str.Build();

        ParseError:
            error = Loc.S.BbMismatch;
            return new SeStringBuilder().AddText(error).Build();

        ColorError:
            error = Loc.S.BbColorInvalid;
            return new SeStringBuilder().AddText(error).Build();
        }
        catch (Exception)
        {
            error = Loc.S.BbSyntax;
            return new SeStringBuilder().AddText(error).Build();
        }
    }

    [GeneratedRegex(@"(\[color=[0-9a-zA-Z]+\])|(\[\/color\])|(\[glow=[0-9a-zA-Z]+\])|(\[\/glow\])|(\[i\])|(\[\/i\])", RegexOptions.IgnoreCase, "en-US")]
    private static partial Regex SplitRegex();
}

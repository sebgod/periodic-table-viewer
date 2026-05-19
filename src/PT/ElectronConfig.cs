namespace PeriodicTable;

/// <summary>
/// Expands a noble-gas-shorthand electron configuration ("[Ne] 3s²") into
/// its full non-shorthand form ("1s² 2s² 2p⁶ 3s²"). Shared by panels that
/// want to show the long form alongside the compact form already on the
/// <see cref="Element"/> record.
/// </summary>
public static class ElectronConfig
{
    public static string Expand(Element e)
    {
        var s = e.ElectronConfiguration;
        if (s.Length > 0 && s[0] == '[')
        {
            int close = s.IndexOf(']');
            if (close > 0)
            {
                var token = s[..(close + 1)];
                if (NobleGasExpansion.TryGetValue(token, out var expanded))
                {
                    var rest = s[(close + 1)..].TrimStart();
                    return rest.Length > 0 ? $"{expanded} {rest}" : expanded;
                }
            }
        }
        return s; // already full (e.g. "1s¹" for hydrogen)
    }

    /// <summary>
    /// LaTeX form of the expanded configuration, suitable for embedding inside
    /// a markdown <c>\(…\)</c> inline-math span. Subshell occupancies are
    /// written as <c>^{N}</c> (digit form) and subshells are separated by
    /// <c>\,</c> thin spaces.
    /// </summary>
    public static string ExpandLatex(Element e)
        => ToLatex(Expand(e));

    /// <summary>
    /// Converts a Unicode-superscript electron-config string (e.g. <c>"1s² 2s² 2p⁶"</c>)
    /// into LaTeX inline-math source (<c>"1s^{2}\,2s^{2}\,2p^{6}"</c>). Plain
    /// spaces between subshell tokens become <c>\,</c>; runs of Unicode
    /// super-digits become <c>^{digits}</c>. Other characters pass through.
    /// </summary>
    private static string ToLatex(string s)
    {
        var sb = new System.Text.StringBuilder(s.Length + 16);
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (TryMapSuperDigit(c, out _))
            {
                sb.Append("^{");
                while (i < s.Length && TryMapSuperDigit(s[i], out var d))
                {
                    sb.Append(d);
                    i++;
                }
                sb.Append('}');
                i--; // step back so the outer for-loop increment lands on the next char
                continue;
            }
            if (c == ' ')
            {
                sb.Append("\\,");
                continue;
            }
            sb.Append(c);
        }
        return sb.ToString();
    }

    private static bool TryMapSuperDigit(char c, out char digit)
    {
        digit = c switch
        {
            '⁰' => '0',
            '¹' => '1',
            '²' => '2',
            '³' => '3',
            '⁴' => '4',
            '⁵' => '5',
            '⁶' => '6',
            '⁷' => '7',
            '⁸' => '8',
            '⁹' => '9',
            _ => '\0',
        };
        return digit != '\0';
    }

    private static readonly System.Collections.Generic.Dictionary<string, string> NobleGasExpansion = new()
    {
        ["[He]"] = "1s²",
        ["[Ne]"] = "1s² 2s² 2p⁶",
        ["[Ar]"] = "1s² 2s² 2p⁶ 3s² 3p⁶",
        ["[Kr]"] = "1s² 2s² 2p⁶ 3s² 3p⁶ 3d¹⁰ 4s² 4p⁶",
        ["[Xe]"] = "1s² 2s² 2p⁶ 3s² 3p⁶ 3d¹⁰ 4s² 4p⁶ 4d¹⁰ 5s² 5p⁶",
        // [Rn] / [Og] use the conventional ordering where the 4f/5f groups are
        // listed AFTER the higher s/p of the same n shell — matches the post-
        // core listing style used in the source data.
        ["[Rn]"] = "1s² 2s² 2p⁶ 3s² 3p⁶ 3d¹⁰ 4s² 4p⁶ 4d¹⁰ 5s² 5p⁶ 4f¹⁴ 5d¹⁰ 6s² 6p⁶",
        ["[Og]"] = "1s² 2s² 2p⁶ 3s² 3p⁶ 3d¹⁰ 4s² 4p⁶ 4d¹⁰ 5s² 5p⁶ 4f¹⁴ 5d¹⁰ 6s² 6p⁶ 5f¹⁴ 6d¹⁰ 7s² 7p⁶",
    };
}

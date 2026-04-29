namespace PeriodicTable.Tui;

/// <summary>
/// Expands a noble-gas-shorthand electron configuration ("[Ne] 3s²") into
/// its full non-shorthand form ("1s² 2s² 2p⁶ 3s²"). Shared by panels that
/// want to show the long form alongside the compact form already on the
/// <see cref="Element"/> record.
/// </summary>
internal static class ElectronConfig
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

    private static readonly System.Collections.Generic.Dictionary<string, string> NobleGasExpansion = new()
    {
        ["[He]"] = "1s\u00B2",
        ["[Ne]"] = "1s\u00B2 2s\u00B2 2p\u2076",
        ["[Ar]"] = "1s\u00B2 2s\u00B2 2p\u2076 3s\u00B2 3p\u2076",
        ["[Kr]"] = "1s\u00B2 2s\u00B2 2p\u2076 3s\u00B2 3p\u2076 3d\u00B9\u2070 4s\u00B2 4p\u2076",
        ["[Xe]"] = "1s\u00B2 2s\u00B2 2p\u2076 3s\u00B2 3p\u2076 3d\u00B9\u2070 4s\u00B2 4p\u2076 4d\u00B9\u2070 5s\u00B2 5p\u2076",
        // [Rn] / [Og] use the conventional ordering where the 4f/5f groups are
        // listed AFTER the higher s/p of the same n shell — matches the post-
        // core listing style used in the source data.
        ["[Rn]"] = "1s\u00B2 2s\u00B2 2p\u2076 3s\u00B2 3p\u2076 3d\u00B9\u2070 4s\u00B2 4p\u2076 4d\u00B9\u2070 5s\u00B2 5p\u2076 4f\u00B9\u2074 5d\u00B9\u2070 6s\u00B2 6p\u2076",
        ["[Og]"] = "1s\u00B2 2s\u00B2 2p\u2076 3s\u00B2 3p\u2076 3d\u00B9\u2070 4s\u00B2 4p\u2076 4d\u00B9\u2070 5s\u00B2 5p\u2076 4f\u00B9\u2074 5d\u00B9\u2070 6s\u00B2 6p\u2076 5f\u00B9\u2074 6d\u00B9\u2070 7s\u00B2 7p\u2076",
    };
}

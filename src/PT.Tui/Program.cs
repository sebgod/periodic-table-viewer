using DIR.Lib;
using PeriodicTable;
using PeriodicTable.Tui.Soft;
using CL = global::Console.Lib;
using SysConsole = global::System.Console;

namespace PeriodicTable.Tui;

internal static class Program
{
    private static async Task<int> Main(string[] _)
    {
        await using var term = new CL.VirtualTerminal();
        await term.InitAsync();

        if (term.IsInputRedirected || term.IsOutputRedirected)
        {
            PrintNonInteractive();
            return 0;
        }

        term.EnterAlternateScreen();
        await RunUi(term);
        return 0;
    }

    private static async Task RunUi(CL.IVirtualTerminal term)
    {
        var panel = new CL.Panel(term);

        var header = new CL.TextBar(panel.Dock(DockStyle.Top, 1))
            .Text(" Periodic Table Viewer ")
            .RightText(" ←↑→↓ navigate · click isotope · y yank · q quit  ")
            .Style(new CL.VtStyle(CL.SgrColor.Black, CL.SgrColor.White));

        var status = new CL.TextBar(panel.Dock(DockStyle.Bottom, 1))
            .Style(new CL.VtStyle(CL.SgrColor.BrightWhite, CL.SgrColor.BrightBlack));

        // Decay chain panel sits between detail panel and the periodic table.
        // Drawn even when the selected element is stable (caption explains).
        // Sixel rendering is opt-in: requires both terminal capability and a
        // resolvable system font; otherwise the panel falls back to a
        // text-only chain on a single line.
        // FontResolver returns "" when no candidate is found; the panels expect
        // null for that "Sixel disabled" branch, so map empty -> null here.
        var resolved = term.HasSixelSupport ? FontResolver.ResolveSystemFont() : "";
        string? fontPath = resolved.Length > 0 ? resolved : null;
        var chainPanel = new SixelDecayChainPanel(
            panel.Dock(DockStyle.Bottom, SixelDecayChainPanel.Rows),
            fontPath);

        var detail = new DetailPanel(panel.Dock(DockStyle.Bottom, DetailPanel.Rows));

        // Orbital panel: docks on the right when the terminal is wide enough
        // at startup. The check uses the root viewport's initial size — once
        // the dock layout is fixed we can't change it, so a too-narrow
        // terminal just gets no orbital panel until the user restarts.
        // Threshold: table width (72) + a couple of margin cells + the
        // panel's own minimum.
        OrbitalPanel? orbital = null;
        if (term.Size.Width >= PeriodicTableWidget.RenderedWidth + 2 + OrbitalPanel.MinViewportCols)
        {
            orbital = new OrbitalPanel(
                panel.Dock(DockStyle.Right, OrbitalPanel.DockedWidth),
                fontPath);
        }

        var fill = panel.Fill();
        var table = new PeriodicTableWidget(fill);

        panel.Add(header).Add(detail).Add(chainPanel).Add(table).Add(status);
        if (orbital is not null) panel.Add(orbital);

        void Refresh(Element e)
        {
            detail.SetElement(e);
            chainPanel.SetElement(e);
            orbital?.SetElement(e);
            status.Text($"  {e.Symbol} · {e.Name}  ")
                  .RightText($"  Z={e.AtomicNumber}  {e.Category}  ");
        }

        table.SelectionChanged += Refresh;
        Refresh(table.Selected);

        bool quit = false;
        bool dirty = true; // force initial paint
        while (!quit)
        {
            if (panel.Recompute()) { term.Clear(); dirty = true; }
            if (dirty)
            {
                panel.RenderAll();
                term.Flush();
                dirty = false;
            }

            while (term.HasInput())
            {
                var ev = term.TryReadInput();
                if (ev.Mouse is { } m)
                {
                    // Decay panel gets the click first — its viewport is below
                    // the table's, so a click there must not also fire on the
                    // table. On a hit, jump table selection to the isotope's
                    // element so the rest of the UI (detail, status) follows.
                    if (chainPanel.TryClick(m, out var iso))
                    {
                        if (table.SelectByZ(iso.Z)) dirty = true;
                    }
                    else if (table.HandleMouse(m)) dirty = true;
                    continue;
                }
                if (ev.Key == ConsoleKey.Q || ev.Key == ConsoleKey.Escape)
                {
                    quit = true;
                    break;
                }
                if (ev.Key == ConsoleKey.Y && ev.Modifiers == 0)
                {
                    if (chainPanel.GetChainPlainText() is { } text)
                    {
                        CL.Clipboard.SetText(term, text);
                        status.Text($"  Copied: {text[..Math.Min(60, text.Length)]}…  ");
                        dirty = true;
                    }
                    continue;
                }
                if (table.HandleKey(ev.Key, ev.Modifiers)) dirty = true;
            }
            await Task.Delay(20);
        }
    }

    private static void PrintNonInteractive()
    {
        SysConsole.WriteLine($"Periodic Table — {Elements.All.Count} elements");
        SysConsole.WriteLine();
        SysConsole.WriteLine("  Z  Sym  Name                Mass     Group  Period  Block  Category");
        SysConsole.WriteLine("  ─  ───  ──────────────────  ───────  ─────  ──────  ─────  ─────────────────");
        foreach (var e in Elements.All)
        {
            var grp = e.Group?.ToString() ?? "—";
            var mass = e.IsSynthetic ? $"({(int)Math.Round(e.AtomicWeight)})" : e.AtomicWeight.ToString("F3");
            SysConsole.WriteLine($"  {e.AtomicNumber,3}  {e.Symbol,-3}  {e.Name,-18}  {mass,7}  {grp,5}  {e.Period,6}  {e.Block,5}  {e.Category}");
        }
    }
}

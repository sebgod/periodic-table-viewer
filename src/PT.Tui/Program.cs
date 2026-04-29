using System.Text;
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
        var fontPath = term.HasSixelSupport ? SixelDecayChainPanel.FindSystemFont() : null;
        var chainPanel = new SixelDecayChainPanel(
            panel.Dock(DockStyle.Bottom, SixelDecayChainPanel.Rows),
            fontPath);

        var detail = new DetailPanel(panel.Dock(DockStyle.Bottom, DetailPanel.Rows));
        var fill = panel.Fill();
        var table = new PeriodicTableWidget(fill);

        panel.Add(header).Add(detail).Add(chainPanel).Add(table).Add(status);

        void Refresh(Element e)
        {
            detail.SetElement(e);
            chainPanel.SetElement(e);
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
                        WriteOsc52(term, text);
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

    /// <summary>
    /// Emits the OSC 52 "set selection clipboard" escape, asking the terminal
    /// to put <paramref name="text"/> on the system clipboard. Universally
    /// supported in modern terminals (Windows Terminal, iTerm2, kitty,
    /// foot, etc.) — no native clipboard P/Invoke needed. Useful here because
    /// the Sixel-rendered isotope notation is not selectable via the
    /// terminal's own drag-select.
    /// </summary>
    private static void WriteOsc52(CL.IVirtualTerminal term, string text)
    {
        var b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(text));
        term.Write($"\u001b]52;c;{b64}\u0007");
        term.Flush();
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

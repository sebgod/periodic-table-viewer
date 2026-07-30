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

#if CONSOLE_INSPECTOR
        // Buffered, DIFFING writes from here on, and enabled HERE rather than inside RunUi — the same
        // placement TianWen's TUI uses. The buffer only reaches the screen when someone calls Flush, so
        // anything written before it is enabled sits in a buffer nothing has flushed yet; turning it on
        // before a single widget exists is what keeps the first frame honest.
        if (Environment.GetEnvironmentVariable("PT_INSPECTOR") is { Length: > 0 })
        {
            term.EnableCellBuffer();
            // The counts say HOW MUCH went out; this says WHICH cells — the question the front buffer
            // cannot answer after the fact, because its final state always looks right.
            if (term.CellBuffer is { } buffer) buffer.CollectFlushDiagnostics = true;
        }
#endif

        await RunUi(term);
        return 0;
    }

    // Concrete VirtualTerminal, not IVirtualTerminal: the debug inspector needs
    // IInspectableTerminal + EnableCellBuffer, which the concrete type carries.
    private static async Task RunUi(CL.VirtualTerminal term)
    {
        // Resolve a system font for the Sixel-rendered chain image and the
        // pixel-mode markdown math blocks. FontResolver returns "" when no
        // candidate is found; the downstream panels expect null for that
        // "pixel mode disabled" branch, so map empty -> null here.
        var resolved = term.HasSixelSupport ? FontResolver.ResolveSystemFont() : "";
        string? fontPath = resolved.Length > 0 ? resolved : null;

        // One hosted viewport per frame slot, created empty and re-pointed at its slot's arranged rect by
        // FrameHost.Place. This replaces CL.Panel's dock chain: the frame is now a costed shape plus one
        // declarative tree (see ViewerFrameLayout), so a resize re-derives the whole arrangement instead
        // of being stuck with whatever the terminal size was at startup.
        var frameHost = new FrameHost(term);
        CL.ITerminalViewport Host(string key) => frameHost.Host(key);

        var header = new CL.TextBar(Host(ViewerFrameLayout.SlotHeader))
            .Text(" Periodic Table Viewer ")
            .RightText(" ←↑→↓ navigate · click isotope · y yank · q quit  ")
            .Style(new CL.VtStyle(CL.SgrColor.Black, CL.SgrColor.White));

        var status = new CL.TextBar(Host(ViewerFrameLayout.SlotStatus))
            .Style(new CL.VtStyle(CL.SgrColor.BrightWhite, CL.SgrColor.BrightBlack));

        // A capability, not a layout decision. Both panels already degrade themselves to their text path
        // when handed too few rows (SixelDecayChainPanel.MathLegendMinRows, DetailPanel.Render), so the
        // frame's job is deciding how many rows to SPEND and the panels' job is deciding what fits in
        // them — which is what lets one shape change on resize be honoured without rebuilding a widget.
        var mathMode = fontPath is not null ? CL.BoxRenderMode.Sextant : (CL.BoxRenderMode?)null;

        var chainPanel = new SixelDecayChainPanel(Host(ViewerFrameLayout.SlotChain), fontPath, mathMode);
        var detail = new DetailPanel(Host(ViewerFrameLayout.SlotDetail), mathMode, fontPath);

        // Constructed unconditionally now, where it used to be null on a narrow terminal. "No orbital
        // gutter" is expressed as a zero-width slot, which the render loop skips and the panel itself
        // guards — and because it sizes its Sixel surface lazily in Render, growing the terminal makes it
        // appear with no restart. That restart was the whole complaint against the docked budget.
        var orbital = new OrbitalPanel(Host(ViewerFrameLayout.SlotOrbital), fontPath);

        var table = new PeriodicTableWidget(Host(ViewerFrameLayout.SlotTable));

        // Paint order preserved from the dock version (header, detail, chain, table, status, orbital).
        // The regions are disjoint so it does not matter today, but the two Sixel panels write raw output
        // and the order in which raw regions are declared is not something to churn casually.
        CL.Widget[] widgets = [header, detail, chainPanel, table, status, orbital];

        ViewerFrameLayout NewFrame() => new(
            term.Size.Width, term.Size.Height, ViewerFrameMetrics.Widgets,
            mathCapable: fontPath is not null);

        var frame = NewFrame();
        frameHost.Place(frame);
        var lastSize = term.Size;

        void Refresh(Element e)
        {
            detail.SetElement(e);
            chainPanel.SetElement(e);
            orbital.SetElement(e);
            status.Text($"  {e.Symbol} · {e.Name}  ")
                  .RightText($"  Z={e.AtomicNumber}  {e.Category}  ");
        }

        table.SelectionChanged += Refresh;
        Refresh(table.Selected);

#if CONSOLE_INSPECTOR
        // Lets an agent drive this TUI instead of asking someone to click it. See
        // .claude/skills/run-tui for the protocol and the MCP server.
        //
        // The cell buffer is what gives the inspector a screen to report: buffered writes go
        // through the diff, so the FRONT buffer is the record of what was actually emitted.
        // Enabling it is safe for the Sixel panels because both blit through CL.Canvas, whose
        // Render() declares the region via BeginRawOutput/MarkRawRegion — so the diff breaks
        // its runs AROUND the picture instead of painting blanks through it.
        // The cell buffer is enabled in Main (it has to precede every widget); its presence IS the
        // signal that the inspector was asked for.
        CL.ConsoleDebugInspector? inspector = term.CellBuffer is null
            ? null
            : CL.ConsoleDebugInspector.Attach("pt-tui", term, AppState);

        // Hand-built JSON, not serialized from an anonymous type: this project is AOT-configured,
        // so reflection-based serialization is disabled and the generic overload throws at
        // runtime. DebugInspectorCore.Quote exists for exactly this.
        string AppState()
        {
            static string Q(string? v) => DIR.Lib.Diagnostics.DebugInspectorCore.Quote(v);
            var e = table.Selected;

            // The chain plain text rides along because the decay chain is SIXEL — it reads as
            // kind=Image with no glyphs, so `screen` can never assert it. This is the only way a
            // driver can check which chain is on screen. Same reason `sixel` is reported: a run
            // with no system font silently takes the text-fallback path, and a test that did not
            // know which path it was on would draw the wrong conclusion from a blank band.
            return "{" +
                $"\"z\":{e.AtomicNumber}," +
                $"\"symbol\":{Q(e.Symbol)}," +
                $"\"name\":{Q(e.Name)}," +
                $"\"category\":{Q(e.Category.ToString())}," +
                $"\"period\":{e.Period}," +
                $"\"block\":{Q(e.Block.ToString())}," +
                $"\"sixel\":{(fontPath is not null ? "true" : "false")}," +
                // The frame's decision, so a driver can resize the terminal and assert that the shape
                // followed. These are re-read from `frame` per call rather than captured once, which is
                // the whole difference from the budget this replaced.
                $"\"shape\":{Q(frame.Shape.ToString())}," +
                $"\"detailRows\":{frame.DetailRows}," +
                $"\"chainRows\":{frame.ChainRows}," +
                $"\"orbitalCols\":{frame.OrbitalColumns}," +
                $"\"orbitalPanel\":{(frame.HasOrbitalPanel ? "true" : "false")}," +
                $"\"chain\":{Q(chainPanel.GetChainPlainText())}," +
                // Paint accounting, from Console.Lib 4.8: totals rather than a per-last-flush read,
                // because a mid-paint flush hides from the latter while being the whole problem.
                $"\"flushedCells\":{term.FlushedCellsTotal}," +
                $"\"flushedOpaqueCells\":{term.FlushedOpaqueCellsTotal}," +
                $"\"lastFlushRuns\":{Q(term.CellBuffer?.LastFlushRuns is { } r && r.Length > 900 ? r[..900] : term.CellBuffer?.LastFlushRuns)}" +
                "}";
        }

        // "What it changed" is the half that makes an input log worth reading: a bare keycode
        // cannot distinguish a dropped event from one that was handled and correctly changed
        // nothing (an arrow key at the edge of the grid, say).
        void LogInput(string what)
            => inspector?.LogInput($"{what} -> Z={table.Selected.AtomicNumber} {table.Selected.Symbol}");
#endif

        bool quit = false;
        bool dirty = true; // force initial paint
        while (!quit)
        {
#if CONSOLE_INSPECTOR
            // Top of the loop, so a command that Injects a key has it drained below in this same
            // iteration; the resulting repaint lands on the next one, which is what the
            // inspector's `wait {frames}` verb is for. No-op when no inspector is attached.
            inspector?.Pump();
#endif
            // Guarded on the size actually changing: re-deriving the frame allocates (a tree plus the
            // engine's arrange scratch), and this loop pumps every 20 ms. Panel.Recompute() made the same
            // check internally; it is only visible here because the frame is ours now.
            if (lastSize != term.Size)
            {
                lastSize = term.Size;
                frame = NewFrame();
                // Clear on move, not on every re-derive: a region that shrank or vanished (the orbital
                // gutter, most of all) leaves its old cells behind, and nothing repaints what is no
                // longer anyone's viewport.
                if (frameHost.Place(frame)) { term.Clear(); dirty = true; }
            }

            if (dirty)
            {
                foreach (var widget in widgets)
                {
                    // Skip a slot the frame gave no room to. Every widget here guards this itself, but
                    // the skip is where "the shape omits this region" is actually said — now that every
                    // slot exists for the frame's whole lifetime, a zero-area viewport IS the absence.
                    var (w, h) = widget.Viewport.Size;
                    if (w > 0 && h > 0) widget.Render();
                }

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
#if CONSOLE_INSPECTOR
                        LogInput($"click ({m.X},{m.Y}) chain isotope {iso}");
#endif
                    }
                    else if (table.HandleMouse(m))
                    {
                        dirty = true;
#if CONSOLE_INSPECTOR
                        LogInput($"click ({m.X},{m.Y}) table");
#endif
                    }
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
#if CONSOLE_INSPECTOR
                LogInput($"key {ev.Key}{(ev.Modifiers == 0 ? "" : $"+{ev.Modifiers}")}");
#endif
            }
            await Task.Delay(20);
        }

#if CONSOLE_INSPECTOR
        // Releases the loopback listener and the discovery socket, so the next run of a rebuilt
        // binary does not race a lingering one for them.
        inspector?.Dispose();
#endif

        // Both panels hold an unmanaged Sixel surface. Process exit would reclaim it, but the orbital
        // panel is now built on every run rather than only on a wide terminal, so leaving it to exit
        // means the one that is always constructed is also the one never released.
        chainPanel.Dispose();
        orbital.Dispose();
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

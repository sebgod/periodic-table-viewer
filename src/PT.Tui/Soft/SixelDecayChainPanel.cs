using Console.Lib;
using DIR.Lib;
using PeriodicTable;
using CL = global::Console.Lib;

namespace PeriodicTable.Tui.Soft;

/// <summary>
/// Bottom-panel widget that renders a decay chain for the selected element.
///
/// Sixel path: rasterizes the chain as a horizontal strip of isotope tiles
/// with proper isotope notation — mass number top-left, atomic number
/// bottom-left, symbol main, all stacked in the same column to the left of
/// the symbol. Terminal text cannot stack super- and subscripts in the same
/// column; that is the single feature this widget exists to provide.
///
/// Tiles are connected by inline text arrows (e.g. "─α→") rendered into the
/// same Sixel surface — simpler and sharper than constructed rectangles.
///
/// Text fallback: when no Sixel and/or no system font, the chain is emitted
/// as one line of Unicode-superscript notation:
///   "²³⁸U →α ²³⁴Th →β⁻ ²³⁴Pa → … → ²⁰⁶Pb"
/// (atomic-number subscripts are omitted in the text path because they
/// cannot stack on the same column).
/// </summary>
public sealed class SixelDecayChainPanel : CL.Widget, IDisposable
{
    public const int Rows = 5;
    private const int ChainPixelRows = 2;

    private readonly string? _fontPath;
    private CL.SixelRgbaImageRenderer? _renderer;
    private (int Width, int Height) _surfacePx;

    private Element _element = Elements.ByAtomicNumber[1];
    private DecayChain? _chain;

    // Cell-aligned hit regions (panel-local coords) populated during Render so
    // clicks can be mapped back to the isotope under the cursor — works for
    // both the Sixel tiles and the text-fallback tokens. Cleared each frame.
    private readonly List<(int Left, int Top, int Width, int Height, Isotope Iso)> _hits = [];

    public SixelDecayChainPanel(CL.ITerminalViewport viewport, string? fontPath)
        : base(viewport)
    {
        _fontPath = fontPath;
    }

    public void SetElement(Element e)
    {
        _element = e;
        _chain = DecayChains.ForElement(e);
    }

    public override void Render()
    {
        _hits.Clear();
        var mode = Viewport.ColorMode;
        int width = Viewport.Size.Width;
        if (width <= 0) return;

        var dimStyle = new CL.VtStyle(CL.SgrColor.BrightBlack, CL.SgrColor.Black);

        // Row 0: top divider.
        if (TrySetCursorPosition(Viewport, 0, 0))
            Viewport.Write(new string('\u2500', width));

        // Row 1: chain caption (rendered through MarkdownRenderer so the chain
        // name and selected symbol get markdown bold styling and the legend
        // row below shares the same rendering path).
        var caption = _chain is null
            ? $"  **{_element.Symbol}** ({_element.Name}): no curated decay chain"
            : $"  **{_chain.Name}** — selected: **{_element.Symbol}**";
        WriteMarkdownRow(1, caption, width);

        // Rows 2..2+ChainPixelRows-1: blank first, then either Sixel or text overlay.
        for (int r = 2; r < 2 + ChainPixelRows; r++)
            WriteRow(r, "", dimStyle, width);

        if (_chain is null)
        {
            WriteRow(2 + ChainPixelRows, "  Stable — see top detail panel", dimStyle, width);
            return;
        }

        bool sixelOk = _fontPath is not null && TryRenderSixel(width);
        if (!sixelOk) RenderTextFallback(width);

        // Last row: legend showing the relevant decay step from the selected
        // isotope. Useful when there are too many steps to read all half-lives
        // off the bitmap. Isotopes are pre-baked into Unicode super-digits
        // (²³⁸U) and embedded as literal markdown text — the math grammar's
        // ^{N} path needs a base atom before it, which an isotope-prefix form
        // (^{A}Sym) doesn't have, and the empty-group {}^{A} workaround isn't
        // recognised either. Bold styling on the surrounding caption is what
        // we get from the markdown pipeline here.
        var firstStep = _chain.Steps.FirstOrDefault(s => s.Parent.Z == _element.AtomicNumber);
        var legend = firstStep is null
            ? $"  {IsotopeText(_chain.Start)} → … → {IsotopeText(_chain.End)} (stable)"
            : $"  {IsotopeText(firstStep.Parent)} →{firstStep.Mode.Symbol()} {IsotopeText(firstStep.Daughter)}   t½ = {firstStep.HalfLife}";
        WriteMarkdownRow(2 + ChainPixelRows, legend, width);
    }

    /// <summary>Mass-number superscript + symbol, ready for direct insertion into markdown source.</summary>
    private static string IsotopeText(Isotope iso)
        => CL.Subscripts.Super(iso.A.ToString()) + iso.Symbol;

    private bool TryRenderSixel(int viewportCols)
    {
        var cell = Viewport.CellSize;
        if (cell.Width == 0 || cell.Height == 0) return false;

        int pxW = viewportCols * cell.Width;
        int pxH = ChainPixelRows * cell.Height;
        // Need at least ~28px vertical for any sane glyph.
        if (pxH < 28) return false;

        if (_renderer is null || _surfacePx != (pxW, pxH))
        {
            _renderer?.Dispose();
            _renderer = new CL.SixelRgbaImageRenderer((uint)pxW, (uint)pxH);
            _surfacePx = (pxW, pxH);
        }

        var r = _renderer;
        // Opaque-black fill: every frame fully repaints the band so stale
        // pixels from a previous (longer or differently-spaced) chain cannot
        // ghost through. We previously used alpha=0 + P2=1 for byte-count
        // savings, but Windows Terminal does not clear emitted Sixel pixels
        // when the cell is overwritten with a text space — so transparent
        // skips in the new frame leak the old frame's tiles. The WriteRow
        // pre-paint above is now belt-and-suspenders.
        var bg = new RGBAColor32(0, 0, 0, 255);
        var fg = new RGBAColor32(230, 230, 230, 255);
        var dim = new RGBAColor32(160, 160, 160, 255);
        var arrowColor = new RGBAColor32(180, 180, 220, 255);
        var endColor = new RGBAColor32(160, 220, 160, 255);
        var hilite = new RGBAColor32(80, 80, 140, 255);

        r.FillRectangle(new RectInt(new PointInt(pxW, pxH), new PointInt(0, 0)), bg);

        // Slot allocation: N steps → N+1 tiles + N arrows. Tiles get ~70 % of
        // horizontal space, arrows the rest.
        int n = _chain!.Steps.Count;
        int totalTiles = n + 1;
        int totalSlots = totalTiles + n;
        // weight tiles 1.6× the size of arrow slots
        int slotUnits = totalTiles * 16 + n * 10;
        int unitPx = pxW / slotUnits;
        int tileW = unitPx * 16;
        int arrowW = unitPx * 10;

        int x = 0;
        DrawIsotopeTile(r, x, tileW, pxH, _chain.Start,
            _chain.Start.Z == _element.AtomicNumber ? hilite : (RGBAColor32?)null,
            fg, dim);
        AddSixelHit(x, tileW, cell.Width, _chain.Start);
        x += tileW;

        for (int i = 0; i < n; i++)
        {
            var step = _chain.Steps[i];
            DrawArrow(r, x, arrowW, pxH, step.Mode.Symbol(), arrowColor);
            x += arrowW;

            var isEnd = i == n - 1;
            var symFg = isEnd ? endColor : fg;
            var bgHi = step.Daughter.Z == _element.AtomicNumber ? hilite : (RGBAColor32?)null;
            DrawIsotopeTile(r, x, tileW, pxH, step.Daughter, bgHi, symFg, dim);
            AddSixelHit(x, tileW, cell.Width, step.Daughter);
            x += tileW;
        }

        // Pin the canvas at (col=0, row=2) of THIS panel's viewport — Canvas
        // sets cursor (0,0) within the sub-viewport, which delegates to
        // (0+0, 2+0) of the parent.
        var sixelVp = new CL.TerminalViewport(Viewport, 0, 2, viewportCols, ChainPixelRows);
        new CL.Canvas(sixelVp, r).Render();
        return true;
    }

    private void DrawIsotopeTile(
        CL.SixelRgbaImageRenderer r,
        int x, int w, int h,
        Isotope iso,
        RGBAColor32? hiliteBg,
        RGBAColor32 symColor,
        RGBAColor32 numColor)
    {
        if (hiliteBg is { } bg)
        {
            r.FillRectangle(
                new RectInt(new PointInt(x + w - 1, h - 1), new PointInt(x + 1, 1)),
                bg);
        }

        // Sizing: bound by BOTH tile dimensions so glyphs never overflow when
        // the window narrows. Picking by height alone made labels collide
        // with adjacent tiles when the user resized down to a smaller window.
        float symPx = MathF.Round(MathF.Min(h * 0.78f, w * 0.55f));
        float numPx = MathF.Round(MathF.Min(h * 0.40f, w * 0.22f));

        // Mass number — top-left strip.
        r.DrawText(
            iso.A.ToString().AsSpan(),
            _fontPath!, numPx, numColor,
            new RectInt(new PointInt(x + w / 2, (int)(numPx * 1.2f)), new PointInt(x + 2, 0)),
            TextAlign.Near, TextAlign.Near);

        // Atomic number — bottom-left strip (dimmer).
        int subTop = h - (int)(numPx * 1.2f);
        r.DrawText(
            iso.Z.ToString().AsSpan(),
            _fontPath!, numPx, numColor,
            new RectInt(new PointInt(x + w / 2, h), new PointInt(x + 2, subTop)),
            TextAlign.Near, TextAlign.Far);

        // Symbol — fills the right ~⅔ of the tile, vertically centered.
        int symLeft = x + (int)(w * 0.35f);
        r.DrawText(
            iso.Symbol.AsSpan(),
            _fontPath!, symPx, symColor,
            new RectInt(new PointInt(x + w, h), new PointInt(symLeft, 0)),
            TextAlign.Center, TextAlign.Center);
    }

    private void DrawArrow(
        CL.SixelRgbaImageRenderer r,
        int x, int w, int h,
        string modeGlyph,
        RGBAColor32 color)
    {
        // Render as inline text so the font does the antialiasing. The dash +
        // mode glyph + arrow are drawn together as one centered string. Box
        // drawing characters render cleanly in monospaced fonts.
        var label = "\u2500" + modeGlyph + "\u2192"; // ─α→ / ─β⁻→ / ─EC→
        // Same width-clamp story as the tile: keeps glyphs inside their slot
        // when the window is narrow.
        float px = MathF.Round(MathF.Min(h * 0.55f, w * 0.30f));
        r.DrawText(
            label.AsSpan(),
            _fontPath!, px, color,
            new RectInt(new PointInt(x + w, h), new PointInt(x, 0)),
            TextAlign.Center, TextAlign.Center);
    }

    private void RenderTextFallback(int width)
    {
        if (_chain is null) return;
        var labelStyle = new CL.VtStyle(CL.SgrColor.BrightWhite, CL.SgrColor.Black);
        var modeStyle = new CL.VtStyle(CL.SgrColor.BrightCyan, CL.SgrColor.Black);
        var endStyle = new CL.VtStyle(CL.SgrColor.BrightGreen, CL.SgrColor.Black);
        var hilite = new CL.VtStyle(CL.SgrColor.Black, CL.SgrColor.BrightYellow);
        var mode = Viewport.ColorMode;

        // Track visible-cell column as we build the output. _hits gets one
        // entry per isotope token spanning the supers + symbol cells, so a
        // click on those cells maps back to the isotope.
        const int LeadIndent = 2;
        int col = LeadIndent;
        var sb = new System.Text.StringBuilder(new string(' ', LeadIndent));

        col += AppendIsotope(sb, _chain.Start, mode,
            _chain.Start.Z == _element.AtomicNumber ? hilite : labelStyle, col);
        for (int i = 0; i < _chain.Steps.Count; i++)
        {
            var step = _chain.Steps[i];
            sb.Append($" {modeStyle.Apply(mode)}\u2192{step.Mode.Symbol()}{CL.VtStyle.Reset} ");
            // " →α " visible width: 1 + 1 + visible(modeSym) + 1
            col += 3 + step.Mode.Symbol().Length;
            var isEnd = step.Daughter == _chain.End;
            var style = step.Daughter.Z == _element.AtomicNumber ? hilite
                      : isEnd ? endStyle
                      : labelStyle;
            col += AppendIsotope(sb, step.Daughter, mode, style, col);
        }

        if (TrySetCursorPosition(Viewport, 0, 2))
        {
            Viewport.Write(sb.ToString());
            // Pad to clear leftovers from a longer prior chain.
            int approxVisible = ApproxVisibleLength(sb.ToString());
            if (approxVisible < width)
                Viewport.Write(new string(' ', width - approxVisible));
        }
        // Row 3 (second of ChainPixelRows) stays blank in text mode.
    }

    /// <summary>Appends the isotope token and records its hit region. Returns visible cells written.</summary>
    private int AppendIsotope(
        System.Text.StringBuilder sb, Isotope iso, CL.ColorMode mode, CL.VtStyle style, int startCol)
    {
        sb.Append(style.Apply(mode));
        var supers = Subscripts.Super(iso.A.ToString());
        sb.Append(supers);
        sb.Append(iso.Symbol);
        sb.Append(CL.VtStyle.Reset);
        int visible = supers.Length + iso.Symbol.Length;
        // Text fallback paints into row 2 only.
        _hits.Add((startCol, 2, visible, 1, iso));
        return visible;
    }

    private void AddSixelHit(int pxLeft, int pxWidth, uint cellPxW, Isotope iso)
    {
        if (cellPxW == 0) return;
        // Sixel surface is anchored at panel-local (col=0, row=2). Map the
        // tile's pixel range onto the cell range it visually covers.
        int colLeft = pxLeft / (int)cellPxW;
        int colRight = (pxLeft + pxWidth + (int)cellPxW - 1) / (int)cellPxW;
        _hits.Add((colLeft, 2, colRight - colLeft, ChainPixelRows, iso));
    }

    /// <summary>Returns the isotope occupying the given panel-local cell, if any.</summary>
    public Isotope? IsotopeAt(int col, int row)
    {
        foreach (var h in _hits)
            if (col >= h.Left && col < h.Left + h.Width
             && row >= h.Top && row < h.Top + h.Height)
                return h.Iso;
        return null;
    }

    /// <summary>Maps a mouse-down inside this panel to the clicked isotope.</summary>
    public bool TryClick(CL.MouseEvent ev, out Isotope iso)
    {
        iso = default;
        if (ev.IsRelease || ev.Button != 0) return false;
        if (HitTest(ev.X, ev.Y) is not { } cell) return false;
        if (IsotopeAt(cell.Col, cell.Row) is not { } found) return false;
        iso = found;
        return true;
    }

    /// <summary>
    /// Plain-text representation of the current chain, suitable for OSC-52 yank
    /// or piping to anywhere that expects copyable text. ASCII-only (no
    /// Unicode supers) so it round-trips cleanly through clipboards that don't
    /// preserve UTF-8.
    /// </summary>
    public string? GetChainPlainText()
    {
        if (_chain is null) return null;
        var sb = new System.Text.StringBuilder();
        sb.Append(_chain.Name).Append(": ");
        sb.Append(_chain.Start);
        foreach (var step in _chain.Steps)
        {
            string m = step.Mode switch
            {
                DecayMode.Alpha => "alpha",
                DecayMode.BetaMinus => "beta-",
                DecayMode.BetaPlus => "beta+",
                DecayMode.ElectronCapture => "EC",
                DecayMode.SpontaneousFission => "SF",
                DecayMode.IsomericTransition => "IT",
                _ => "?",
            };
            sb.Append(" --").Append(m).Append("--> ").Append(step.Daughter);
        }
        return sb.ToString();
    }

    private static int ApproxVisibleLength(string s)
    {
        int n = 0;
        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] == '\u001b' && i + 1 < s.Length && s[i + 1] == '[')
            {
                i += 2;
                while (i < s.Length && s[i] != 'm' && s[i] != 'H') i++;
                continue;
            }
            n++;
        }
        return n;
    }

    private void WriteRow(int row, string text, CL.VtStyle style, int width)
    {
        if (!TrySetCursorPosition(Viewport, 0, row)) return;
        var mode = Viewport.ColorMode;
        var visible = text.Length <= width ? text : text[..width];
        var pad = visible.Length < width ? new string(' ', width - visible.Length) : "";
        Viewport.Write($"{style.Apply(mode)}{visible}{pad}{CL.VtStyle.Reset}");
    }

    /// <summary>
    /// Renders one row of markdown source (paragraph or thematic break) into
    /// the given panel-local row. Takes the first wrapped line out of
    /// <see cref="CL.MarkdownRenderer.RenderLines"/> and pads to width using
    /// <see cref="CL.MarkdownRenderer.VisibleLength"/> so embedded SGR codes
    /// don't get counted against the visible column budget.
    /// </summary>
    private void WriteMarkdownRow(int row, string markdownSrc, int width)
    {
        if (!TrySetCursorPosition(Viewport, 0, row)) return;
        var lines = CL.MarkdownRenderer.RenderLines(markdownSrc, width, Viewport.ColorMode);
        var line = lines.Count > 0 ? lines[0] : "";
        int visible = CL.MarkdownRenderer.VisibleLength(line);
        var pad = visible < width ? new string(' ', width - visible) : "";
        Viewport.Write($"{line}{pad}{CL.VtStyle.Reset}");
    }

    public void Dispose()
    {
        _renderer?.Dispose();
        _renderer = null;
    }
}

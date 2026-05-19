using DIR.Lib;
using PeriodicTable;
using CL = global::Console.Lib;

namespace PeriodicTable.Tui.Soft;

/// <summary>
/// Right-docked panel that draws a 2D probability-density slice of the
/// orbital being filled by the selected element.
///
/// The orbital's principal quantum number n and angular momentum ℓ are
/// derived from <see cref="Element.Period"/> and <see cref="Element.Block"/>:
///   block s, period p  → ns
///   block p, period p  → np
///   block d, period p  → (p−1)d   (3d for period 4, etc.)
///   block f, period p  → (p−2)f   (4f for period 6, 5f for period 7)
///
/// Sixel path: paints |Y_ℓ0(θ)|² · radial(r) into a pixel canvas with a
/// blue→magenta gradient — the canonical lobed shapes (sphere / dumbbell /
/// d_z² / f_z³). Only the m=0 magnetic-quantum-number variant is drawn;
/// the lobe count tells the chemistry (s = 1, p = 2, d = 3 incl. torus,
/// f = 4).
///
/// Text fallback: a small ASCII-art icon of the corresponding shape so the
/// panel is still useful on Sixel-less terminals.
///
/// The panel is only docked when the terminal is wide enough at startup
/// (see <see cref="MinViewportCols"/>); otherwise it is omitted entirely
/// so the periodic table keeps its full width.
/// </summary>
public sealed class OrbitalPanel : CL.Widget, IDisposable
{
    /// <summary>Minimum terminal columns the panel needs to be useful.</summary>
    public const int MinViewportCols = 28;
    /// <summary>Width to dock the panel at when there's enough room.</summary>
    public const int DockedWidth = 32;

    private readonly string? _fontPath;
    private CL.SixelRgbaImageRenderer? _renderer;
    private (int Width, int Height) _surfacePx;

    private Element _element = Elements.ByAtomicNumber[1];

    public OrbitalPanel(CL.ITerminalViewport viewport, string? fontPath)
        : base(viewport)
    {
        _fontPath = fontPath;
    }

    public void SetElement(Element e) => _element = e;

    public override void Render()
    {
        var size = Viewport.Size;
        if (size.Width <= 0 || size.Height <= 0) return;

        var titleStyle = new CL.VtStyle(CL.SgrColor.BrightWhite, CL.SgrColor.Black);
        var dimStyle = new CL.VtStyle(CL.SgrColor.BrightBlack, CL.SgrColor.Black);
        var valueStyle = new CL.VtStyle(CL.SgrColor.BrightYellow, CL.SgrColor.Black);

        // Row 0: left-edge divider — visually separates the panel from the
        // periodic table on its left. We draw a vertical line at col 0 and
        // a horizontal stub-rule across the rest of the row so the corner
        // mirrors the bottom panels' top dividers.
        WriteRow(0, "\u250c\u2500 Orbital " + new string('\u2500', Math.Max(0, size.Width - 12)),
                 titleStyle, size.Width);

        var orbital = OrbitalKindFromElement(_element);
        var subshell = SubshellLabel(_element, orbital);

        // Row 1: subtitle "5f — m=0 slice".
        WriteRow(1, $"\u2502 {subshell}  m=0", dimStyle, size.Width);

        // Legend layout: lobes (1) + fill (1) + "config:" header (1) + up to
        // ConfWrapRows wrapped rows of the expanded electron configuration.
        // The detail panel already shows the noble-gas-shorthand form, so we
        // expand it here to give the user the complementary view.
        const int ConfWrapRows = 3;
        int legendRows = 3 + ConfWrapRows;

        int canvasTop = 2;
        int canvasBottom = size.Height - legendRows;
        int canvasRows = canvasBottom - canvasTop;
        if (canvasRows < 3)
        {
            // Tiny terminal: collapse legend to one line of conf to keep the
            // canvas alive.
            canvasRows = Math.Max(0, size.Height - 4);
            legendRows = Math.Min(4, size.Height - canvasTop - canvasRows);
        }

        // Pre-clear the canvas rows before Sixel renders. The Sixel surface
        // is filled with alpha=0 (transparent) inside TryRenderSixel — pixels
        // outside the orbital lobes are skipped by the encoder so the terminal
        // keeps whatever cell content is already there. Without this pre-clear
        // a previous frame's lobes would ghost through when the user changes
        // element. RenderTextFallback paints its own cells, so we only do this
        // when the Sixel path will run.
        if (_fontPath is not null && canvasRows > 0)
            for (int r = 0; r < canvasRows; r++)
                WriteRow(canvasTop + r, "│", dimStyle, size.Width);

        int n = PrincipalQuantumNumber(_element, orbital);
        bool sixelOk = _fontPath is not null
                    && canvasRows > 0
                    && TryRenderSixel(canvasTop, canvasRows, n, orbital);
        if (!sixelOk)
            RenderTextFallback(canvasTop, canvasRows, orbital, size.Width);

        // Legend rows.
        int legendRow = canvasTop + canvasRows;
        WriteRow(legendRow + 0,
                 $"\u2502 lobes: {LobeCount(orbital)}", dimStyle, size.Width);
        var (filled, capacity) = SubshellOccupancy(_element, orbital);
        WriteRow(legendRow + 1,
                 $"\u2502 fill : {filled}/{capacity}", dimStyle, size.Width);
        WriteRow(legendRow + 2,
                 $"\u2502 config:", dimStyle, size.Width);

        // Word-wrap the expanded configuration into the remaining rows. Each
        // row is prefixed with the panel's vertical edge + indent. If the
        // configuration overflows the available rows, the last row gets a
        // trailing ellipsis.
        int confRowStart = legendRow + 3;
        int confRowsAvailable = Math.Max(0, size.Height - confRowStart);
        int contentWidth = size.Width - 4; // "│   " prefix
        if (confRowsAvailable > 0 && contentWidth > 4)
        {
            // Right panel shows the noble-gas shorthand; the detail panel
            // below has the expanded form for the long view.
            var wrapped = WrapTokens(_element.ElectronConfiguration, contentWidth, confRowsAvailable);
            for (int i = 0; i < confRowsAvailable; i++)
            {
                var line = i < wrapped.Count ? wrapped[i] : "";
                WriteRow(confRowStart + i, $"\u2502   {line}", valueStyle, size.Width);
            }
        }

        // Left-edge column for any canvas rows we didn't already cover with
        // text — the Sixel surface paints over the cells it occupies, but the
        // panel's leftmost column stays text and needs the vertical edge.
        for (int r = canvasTop; r < legendRow; r++)
            WriteEdge(r, dimStyle);
    }

    private bool TryRenderSixel(int rowTop, int rowCount, int n, OrbitalKind kind)
    {
        var cell = Viewport.CellSize;
        if (cell.Width == 0 || cell.Height == 0) return false;

        // Reserve 2 cols on the left for the vertical edge + 1-col padding.
        int leftPadCols = 2;
        int canvasCols = Viewport.Size.Width - leftPadCols - 1;
        if (canvasCols < 6) return false;

        int pxW = canvasCols * (int)cell.Width;
        int pxH = rowCount * (int)cell.Height;
        if (pxH < 24 || pxW < 24) return false;

        if (_renderer is null || _surfacePx != (pxW, pxH))
        {
            _renderer?.Dispose();
            _renderer = new CL.SixelRgbaImageRenderer((uint)pxW, (uint)pxH);
            _surfacePx = (pxW, pxH);
        }

        var r = _renderer;
        // Opaque-black fill: each frame fully repaints the canvas so a
        // previous element's lobes cannot ghost through. We previously used
        // alpha=0 + P2=1 to skip empty pixels for byte-count savings, but
        // Windows Terminal does not clear already-emitted Sixel pixels when
        // the cell is overwritten with a text space, so transparent regions
        // in the new frame leaked the prior orbital. The pre-clear loop in
        // Render is now belt-and-suspenders.
        var bg = new RGBAColor32(0, 0, 0, 255);
        r.FillRectangle(new RectInt(new PointInt(pxW, pxH), new PointInt(0, 0)), bg);

        DrawOrbitalCloud(r, pxW, pxH, n, kind);

        // Pin canvas to (col=leftPadCols, row=rowTop) of THIS panel.
        var sixelVp = new CL.TerminalViewport(Viewport, leftPadCols, rowTop, canvasCols, rowCount);
        new CL.Canvas(sixelVp, r).Render();
        return true;
    }

    private static void DrawOrbitalCloud(
        CL.SixelRgbaImageRenderer r, int pxW, int pxH, int n, OrbitalKind kind)
    {
        int cx = pxW / 2;
        int cy = pxH / 2;
        // Use the smaller half so the orbital fits in both axes.
        double radius = Math.Min(cx, cy) - 3;
        if (radius < 6) return;

        int ell = kind switch
        {
            OrbitalKind.S => 0,
            OrbitalKind.P => 1,
            OrbitalKind.D => 2,
            OrbitalKind.F => 3,
            _ => 0,
        };

        // Hydrogenic radial wave function (Z=1, atomic units):
        //   R_n,ℓ(r) ∝ ρ^ℓ · exp(−ρ/2) · L_{n−ℓ−1}^{2ℓ+1}(ρ),  ρ = 2r/n
        // |R|² has (n−ℓ−1) radial nodes — that's what makes 1s, 2s, 3s look
        // different (0, 1, 2 concentric rings). We map the canvas radius onto
        // physical r ∈ [0, rMax] with rMax sized so the bulk of the orbital
        // fits the canvas. Empirical scaling rMax ≈ n²·1.6 + 4 keeps each
        // orbital reasonably-sized regardless of n.
        double rMax = n * n * 1.6 + 4.0;

        // Pre-compute per-pixel density to find the maximum, then re-scale.
        // Two passes is OK at this resolution (~30×24 cells × 9×17 px = ~80k).
        var density = new double[pxW * pxH];
        double max = 0.0;

        for (int y = 0; y < pxH; y++)
        {
            for (int x = 0; x < pxW; x++)
            {
                double dx = (x - cx) / radius;
                double dy = (y - cy) / radius;
                double r2 = dx * dx + dy * dy;
                if (r2 > 1.0) continue;
                double dist = Math.Sqrt(r2);
                // Treat vertical y as the principal axis (orbital "points up"
                // visually). cosθ = -dy / r so θ=0 is up.
                double cosT = dist > 1e-9 ? -dy / dist : 0.0;
                double ang = AngularDensity(kind, cosT);

                double rPhys = dist * rMax;
                double rho = 2.0 * rPhys / n;
                double L = LaguerreL(n - ell - 1, 2 * ell + 1, rho);
                double radial = Math.Pow(rho, 2 * ell) * Math.Exp(-rho) * L * L;

                double v = ang * radial;
                density[y * pxW + x] = v;
                if (v > max) max = v;
            }
        }
        if (max <= 0) return;

        // Gamma compression scaled by n. For low n the dynamic range of |ψ|²
        // between the central spike and outer regions is moderate; for high n
        // (e.g. 7s with 6 radial nodes) the outer shells are many orders of
        // magnitude dimmer than the inner spike. A heavier compression at
        // high n makes those outer shells visible without over-blooming the
        // simple s/p orbitals at low n.
        double gamma = 1.0 / (1.5 + 0.6 * n);  // n=1 → 0.48, n=7 → 0.18
        for (int y = 0; y < pxH; y++)
        {
            for (int x = 0; x < pxW; x++)
            {
                double v = density[y * pxW + x] / max;
                if (v < 1e-6) continue; // background threshold (well below gamma floor)
                double display = Math.Pow(v, gamma);
                if (display < 0.04) continue;
                var color = HeatColor(display);
                r.FillRectangle(
                    new RectInt(new PointInt(x + 1, y + 1), new PointInt(x, y)),
                    color);
            }
        }
    }

    /// <summary>
    /// Associated Laguerre polynomial L_k^a(x) by the standard recurrence:
    ///   (k+1) L_{k+1}^a = (2k + a + 1 − x) L_k^a − (k + a) L_{k−1}^a
    /// with L_0^a = 1, L_1^a = 1 + a − x. Returns 0 for k &lt; 0 (defensive).
    /// </summary>
    private static double LaguerreL(int k, int a, double x)
    {
        if (k < 0) return 0;
        double prev = 0;
        double curr = 1; // L_0^a
        for (int i = 0; i < k; i++)
        {
            double next = ((2 * i + a + 1 - x) * curr - (i + a) * prev) / (i + 1);
            prev = curr;
            curr = next;
        }
        return curr;
    }

    private static double AngularDensity(OrbitalKind kind, double cosT)
    {
        // Real-spherical-harmonic angular probabilities for m=0, normalised
        // to a peak of 1. The exact prefactors don't matter — we re-scale by
        // the per-frame max anyway. What matters is the cosθ polynomial,
        // because that is what gives each ℓ its signature lobe count.
        switch (kind)
        {
            case OrbitalKind.S:
                return 1.0;
            case OrbitalKind.P:
                return cosT * cosT;
            case OrbitalKind.D:
            {
                double t = 3 * cosT * cosT - 1;
                return t * t;
            }
            case OrbitalKind.F:
            {
                double t = 5 * cosT * cosT * cosT - 3 * cosT;
                return t * t;
            }
            default: return 0;
        }
    }

    private static RGBAColor32 HeatColor(double t)
    {
        // Black → navy → magenta → near-white. Ramps fast at the high end so
        // lobe maxima pop visually without washing out the cloud edges.
        if (t < 0.33)
        {
            double k = t / 0.33;
            return new RGBAColor32((byte)(20 * k), (byte)(20 * k), (byte)(60 + 140 * k), 255);
        }
        if (t < 0.66)
        {
            double k = (t - 0.33) / 0.33;
            return new RGBAColor32((byte)(20 + 200 * k), (byte)(20 + 20 * k), (byte)(200 + 40 * k), 255);
        }
        {
            double k = (t - 0.66) / 0.34;
            return new RGBAColor32((byte)(220 + 35 * k), (byte)(40 + 215 * k), (byte)(240 + 15 * k), 255);
        }
    }

    private void RenderTextFallback(int rowTop, int rowCount, OrbitalKind kind, int width)
    {
        var lobeStyle = new CL.VtStyle(CL.SgrColor.BrightMagenta, CL.SgrColor.Black);
        var dimStyle = new CL.VtStyle(CL.SgrColor.BrightBlack, CL.SgrColor.Black);

        string[] art = OrbitalAscii(kind);
        // Center vertically inside the canvas band.
        int top = rowTop + Math.Max(0, (rowCount - art.Length) / 2);

        for (int i = 0; i < rowCount; i++)
        {
            int absRow = rowTop + i;
            if (i >= art.Length || absRow < top || absRow - top >= art.Length)
            {
                WriteEdge(absRow, dimStyle);
                continue;
            }
            var line = art[absRow - top];
            int leftPad = Math.Max(2, (width - line.Length) / 2);
            string text = "\u2502" + new string(' ', leftPad - 1) + line;
            if (text.Length > width) text = text[..width];
            WriteRow(absRow, text, lobeStyle, width);
        }
    }

    private static string[] OrbitalAscii(OrbitalKind kind) => kind switch
    {
        OrbitalKind.S => [
            "  .--.  ",
            " /    \\ ",
            "(  ⬤  )",
            " \\    / ",
            "  '--'  ",
        ],
        OrbitalKind.P => [
            "  .--.  ",
            " /    \\ ",
            "(  ⬤  )",
            " \\    / ",
            "  ----  ",
            " /    \\ ",
            "(  ⬤  )",
            " \\    / ",
            "  '--'  ",
        ],
        OrbitalKind.D => [
            "  .--.   .--.  ",
            " ( ⬤ ) ( ⬤ ) ",
            "  '--'   '--'  ",
            "      ▒▒▒      ",
            "  .--.   .--.  ",
            " ( ⬤ ) ( ⬤ ) ",
            "  '--'   '--'  ",
        ],
        OrbitalKind.F => [
            " .-. .-. .-. ",
            "( ⬤ ⬤ ⬤ )",
            " '-' '-' '-' ",
            "             ",
            " .-. .-. .-. ",
            "( ⬤ ⬤ ⬤ )",
            " '-' '-' '-' ",
        ],
        _ => ["(unknown)"],
    };

    private void WriteEdge(int row, CL.VtStyle style)
    {
        if (!TrySetCursorPosition(Viewport, 0, row)) return;
        var mode = Viewport.ColorMode;
        Viewport.Write($"{style.Apply(mode)}\u2502{CL.VtStyle.Reset}");
    }

    private void WriteRow(int row, string text, CL.VtStyle style, int width)
    {
        if (!TrySetCursorPosition(Viewport, 0, row)) return;
        var mode = Viewport.ColorMode;
        var visible = text.Length <= width ? text : text[..width];
        var pad = visible.Length < width ? new string(' ', width - visible.Length) : "";
        Viewport.Write($"{style.Apply(mode)}{visible}{pad}{CL.VtStyle.Reset}");
    }

    public void Dispose()
    {
        _renderer?.Dispose();
        _renderer = null;
    }

    // --- Orbital-from-element mapping --------------------------------------

    public enum OrbitalKind { S, P, D, F }

    private static OrbitalKind OrbitalKindFromElement(Element e) => e.Block switch
    {
        Block.S => OrbitalKind.S,
        Block.P => OrbitalKind.P,
        Block.D => OrbitalKind.D,
        Block.F => OrbitalKind.F,
        _ => OrbitalKind.S,
    };

    /// <summary>
    /// Principal quantum number of the block-defining subshell. The d-block
    /// fills (period−1)d (3d during period 4); the f-block fills (period−2)f
    /// (4f during period 6, 5f during period 7).
    /// </summary>
    private static int PrincipalQuantumNumber(Element e, OrbitalKind kind) =>
        e.Period - kind switch
        {
            OrbitalKind.S => 0,
            OrbitalKind.P => 0,
            OrbitalKind.D => 1,
            OrbitalKind.F => 2,
            _ => 0,
        };

    /// <summary>Subshell label like "5f".</summary>
    private static string SubshellLabel(Element e, OrbitalKind kind)
        => $"{PrincipalQuantumNumber(e, kind)}{kind.ToString().ToLowerInvariant()}";

    private static int LobeCount(OrbitalKind kind) => kind switch
    {
        OrbitalKind.S => 1,
        OrbitalKind.P => 2,
        // d_z² has two main lobes plus the equatorial torus = 3 visual features.
        OrbitalKind.D => 3,
        // f_z³ shows 4 lobes (2 along axis, 2 weaker at the waist) in this slice.
        OrbitalKind.F => 4,
        _ => 0,
    };

    /// <summary>
    /// Reads how many electrons populate the highest subshell for this element,
    /// derived from the configuration string by finding the last subshell token.
    /// </summary>
    private static (int Filled, int Capacity) SubshellOccupancy(Element e, OrbitalKind kind)
    {
        int capacity = kind switch
        {
            OrbitalKind.S => 2,
            OrbitalKind.P => 6,
            OrbitalKind.D => 10,
            OrbitalKind.F => 14,
            _ => 0,
        };

        // Tokens look like "3d¹⁰", "4s²", "2p⁶" — find the last one matching
        // the target block letter and decode the Unicode-superscript count.
        var cfg = e.ElectronConfiguration;
        char blockChar = kind switch
        {
            OrbitalKind.S => 's',
            OrbitalKind.P => 'p',
            OrbitalKind.D => 'd',
            OrbitalKind.F => 'f',
            _ => '?',
        };

        for (int i = cfg.Length - 1; i >= 0; i--)
        {
            if (cfg[i] != blockChar) continue;
            // Read trailing superscript digits.
            int j = i + 1;
            int count = 0;
            bool any = false;
            while (j < cfg.Length)
            {
                int d = SuperscriptDigit(cfg[j]);
                if (d < 0) break;
                count = count * 10 + d;
                any = true;
                j++;
            }
            if (any && i > 0 && char.IsDigit(cfg[i - 1]))
                return (count, capacity);
        }
        return (0, capacity);
    }

    private static int SuperscriptDigit(char c) => c switch
    {
        '\u2070' => 0, '\u00B9' => 1, '\u00B2' => 2, '\u00B3' => 3,
        '\u2074' => 4, '\u2075' => 5, '\u2076' => 6, '\u2077' => 7,
        '\u2078' => 8, '\u2079' => 9,
        _ => -1,
    };

    /// <summary>
    /// Word-wraps a space-separated token sequence into at most <paramref name="maxRows"/>
    /// lines, each at most <paramref name="width"/> visible chars. If the input
    /// doesn't fit, the last produced row gets an ellipsis to flag truncation.
    /// </summary>
    private static System.Collections.Generic.List<string> WrapTokens(string text, int width, int maxRows)
    {
        var rows = new System.Collections.Generic.List<string>();
        if (width <= 0 || maxRows <= 0) return rows;
        var tokens = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var sb = new System.Text.StringBuilder();
        int i = 0;
        while (i < tokens.Length && rows.Count < maxRows)
        {
            sb.Clear();
            sb.Append(tokens[i]);
            i++;
            while (i < tokens.Length && sb.Length + 1 + tokens[i].Length <= width)
            {
                sb.Append(' ').Append(tokens[i]);
                i++;
            }
            rows.Add(sb.ToString());
        }
        if (i < tokens.Length && rows.Count > 0)
        {
            // Truncate the last row to make room for an ellipsis if needed.
            var last = rows[^1];
            const string ellipsis = " \u2026";
            if (last.Length + ellipsis.Length > width)
                last = last[..(width - ellipsis.Length)];
            rows[^1] = last + ellipsis;
        }
        return rows;
    }
}

using System.Text;
using CL = global::Console.Lib;

namespace PeriodicTable.Tui.Soft;

// PROTOTYPE: this whole namespace is destined to move upstream into Console.Lib
// once the API has settled. Console.Lib today treats one char = one cell and
// has no abstraction for "a logical block of text occupying W×H cells, with
// per-line alignment, styled sub-spans, and sub/superscript runs". The
// periodic table widget needs all three to render element cells (4×3 cells
// with atomic number top-right, symbol centered, mass bottom).
//
// Once stable, this becomes Console.Lib.SoftText / SoftRenderer / Subscripts.

public enum HAlign { Left, Center, Right }

/// <summary>One styled run inside a <see cref="SoftLine"/>.</summary>
public readonly record struct SoftSpan(string Text, CL.VtStyle? Style = null)
{
    public int VisibleLength => Text.Length;
}

public sealed record SoftLine(IReadOnlyList<SoftSpan> Spans, HAlign Align = HAlign.Center)
{
    public static SoftLine Of(string text, HAlign align = HAlign.Center, CL.VtStyle? style = null)
        => new([new SoftSpan(text, style)], align);

    public int VisibleLength
    {
        get
        {
            int n = 0;
            foreach (var s in Spans) n += s.VisibleLength;
            return n;
        }
    }
}

/// <summary>
/// A logical block of text occupying <see cref="Width"/>×<see cref="Height"/>
/// terminal cells. Lines shorter than <see cref="Width"/> are padded; lines
/// longer than <see cref="Width"/> are truncated at the visible boundary
/// (escape sequences are not counted toward width).
/// </summary>
public sealed class SoftText
{
    public int Width { get; }
    public int Height { get; }
    public IReadOnlyList<SoftLine> Lines { get; }

    public SoftText(int width, int height, IReadOnlyList<SoftLine> lines)
    {
        if (width < 1) throw new ArgumentOutOfRangeException(nameof(width));
        if (height < 1) throw new ArgumentOutOfRangeException(nameof(height));
        Width = width;
        Height = height;
        Lines = lines;
    }
}

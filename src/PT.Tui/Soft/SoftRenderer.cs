using System.Text;
using CL = global::Console.Lib;

namespace PeriodicTable.Tui.Soft;

public static class SoftRenderer
{
    /// <summary>
    /// Paints <paramref name="text"/> into a <c>(Width, Height)</c> rectangle
    /// at viewport-local <c>(col, row)</c>. Every cell in the rectangle is
    /// emitted (so callers can draw on top of arbitrary backgrounds without
    /// gaps). One <see cref="CL.ITerminalViewport.Write"/> call per row.
    /// </summary>
    /// <param name="background">
    /// If non-null, applied at the start of every row and after each styled
    /// span so padding cells inherit it. Pass null to only emit foreground
    /// styling for spans (transparent padding).
    /// </param>
    public static void Render(
        CL.ITerminalViewport viewport,
        int col,
        int row,
        SoftText text,
        CL.ColorMode mode,
        CL.VtStyle? background = null)
    {
        var sb = new StringBuilder(capacity: text.Width * 4);
        string bg = background.HasValue ? background.Value.Apply(mode) : "";

        for (int i = 0; i < text.Height; i++)
        {
            if (!TrySetCursor(viewport, col, row + i)) continue;

            sb.Clear();
            sb.Append(bg);

            var line = i < text.Lines.Count ? text.Lines[i] : null;
            int visibleLen = Math.Min(line?.VisibleLength ?? 0, text.Width);
            int extra = text.Width - visibleLen;
            int leftPad, rightPad;
            switch (line?.Align ?? HAlign.Left)
            {
                case HAlign.Center: leftPad = extra / 2; rightPad = extra - leftPad; break;
                case HAlign.Right:  leftPad = extra; rightPad = 0; break;
                default:            leftPad = 0; rightPad = extra; break;
            }

            if (leftPad > 0) sb.Append(' ', leftPad);

            if (line is not null)
            {
                int remaining = text.Width;
                foreach (var span in line.Spans)
                {
                    if (remaining <= 0) break;
                    int take = Math.Min(span.Text.Length, remaining);
                    if (span.Style is { } st)
                    {
                        sb.Append(st.Apply(mode));
                        if (take == span.Text.Length) sb.Append(span.Text);
                        else sb.Append(span.Text, 0, take);
                        // restore background so any following padding/span on
                        // this row keeps the cell background colour
                        if (background.HasValue) sb.Append(bg);
                        else sb.Append(CL.VtStyle.Reset);
                    }
                    else
                    {
                        if (take == span.Text.Length) sb.Append(span.Text);
                        else sb.Append(span.Text, 0, take);
                    }
                    remaining -= take;
                }
            }

            if (rightPad > 0) sb.Append(' ', rightPad);
            sb.Append(CL.VtStyle.Reset);
            viewport.Write(sb.ToString());
        }
    }

    private static bool TrySetCursor(CL.ITerminalViewport vp, int col, int row)
    {
        if (col < 0 || row < 0) return false;
        if (col >= vp.Size.Width || row >= vp.Size.Height) return false;
        vp.SetCursorPosition(col, row);
        return true;
    }
}

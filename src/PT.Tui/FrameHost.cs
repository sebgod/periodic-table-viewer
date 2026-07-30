using DIR.Lib;
using CL = global::Console.Lib;

namespace PeriodicTable.Tui;

/// <summary>
/// Holds one viewport per <see cref="ViewerFrameLayout"/> slot and re-points them all at a freshly
/// arranged frame. The replacement for <c>CL.Panel</c>'s dock chain: widgets are handed a viewport once,
/// at construction, and every later arrangement moves that same object rather than rebuilding anything.
///
/// <para>Separate from <c>Program</c> so the resize path is testable. A console's size cannot be changed
/// from outside the process under ConPTY — the terminal emulator owns it and the console API calls are
/// no-ops — so driving a real resize is not available even with the debug inspector. This type takes an
/// <see cref="CL.ITerminalViewport"/> and a frame and touches nothing else, which lets a test place the
/// same hosts at one size and then another and assert what moved.</para>
/// </summary>
internal sealed class FrameHost(CL.ITerminalViewport root)
{
    private readonly Dictionary<string, Hosted> _hosts = [];

    /// <summary>
    /// Registers the viewport for the widget hosted at <paramref name="key"/>. Its geometry is meaningless
    /// until <see cref="Place"/> arranges a frame — construct the widgets, then place.
    /// </summary>
    public CL.ITerminalViewport Host(string key)
    {
        var hosted = new Hosted(new CL.TerminalViewport(root, 0, 0, 0, 0));
        _hosts[key] = hosted;
        return hosted.Viewport;
    }

    /// <summary>
    /// Arranges <paramref name="frame"/> and re-points every registered viewport at its slot, reporting
    /// whether any of them actually moved — the replacement for <c>Panel.Recompute()</c>'s "did the
    /// terminal change" guard, which is what keeps the caller from repainting on every pump.
    /// </summary>
    public bool Place(ViewerFrameLayout frame)
    {
        var arranged = Layout.Engine.Arrange(
            frame.Build(),
            new Rect<int>(0, 0, frame.Columns, frame.Rows),
            CL.CellMeasureContext.CellAuthored);

        var moved = false;
        foreach (var (key, host) in _hosts)
        {
            // Driven from the HOSTS, not from the arranged tree: a slot the current shape omits has no
            // leaf to find, and Slot's empty rect is exactly the right answer for it. Walking the tree
            // instead would simply skip that host and leave its viewport parked at the rect it held
            // before the shape changed — a stale orbital gutter overlapping the table.
            //
            // Bitwise |=, not ||: every viewport must be re-pointed, not just the ones up to the first
            // that moved.
            moved |= host.Place(ViewerFrameLayout.Slot(arranged, key));
        }

        return moved;
    }

    /// <summary>
    /// One frame slot's viewport, plus the rect it was last placed at, so a re-arrange can report whether
    /// anything really moved instead of the caller repainting on every pump.
    /// </summary>
    private sealed class Hosted(CL.TerminalViewport viewport)
    {
        private Rect<int>? _last;

        /// <summary>
        /// The hosted viewport. Handed out as the interface because placing it is this class's job and
        /// nothing outside needs <see cref="CL.TerminalViewport.UpdateGeometry"/> — which is the only
        /// reason the concrete type is held at all.
        /// </summary>
        public CL.ITerminalViewport Viewport => viewport;

        /// <summary>Moves the viewport to <paramref name="rect"/>; true when that is somewhere new.</summary>
        public bool Place(Rect<int> rect)
        {
            viewport.UpdateGeometry(rect.X, rect.Y, rect.Width, rect.Height);

            var moved = _last is not { } last
                || last.X != rect.X || last.Y != rect.Y
                || last.Width != rect.Width || last.Height != rect.Height;

            _last = rect;
            return moved;
        }
    }
}

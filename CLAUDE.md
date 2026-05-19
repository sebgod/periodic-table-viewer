# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build / run

Solution: `periodic-table-viewer.slnx`. Library `PT` targets `net10.0`; the TUI targets `net10.0-windows`, AOT-published in Release.

```bash
dotnet build src/PT.Tui/PT.Tui.csproj -c Debug          # TUI
dotnet test  src/PT.Tests/PT.Tests.csproj -c Release    # tests (xUnit.v3 + Shouldly)
dotnet publish src/PT.Tui/PT.Tui.csproj -c Release      # AOT
```

The TUI cannot run inside the Claude Code shell — it detects redirected stdio and falls back to a non-interactive table dump on stdout. To launch interactively, use the `/run-tui` skill (it builds Debug and opens `pt-tui.exe` in a new console window via PowerShell `Start-Process`).

## Architecture

Three projects, layered:

- **`src/PT`** — library (`PeriodicTable` namespace), pure managed, AOT-compatible. `Element` record + `Elements` static frozen-dictionary table covering all 118 elements (Z, symbol, name, group, period, block, category, atomic weight, electron config). `Layout` maps elements to (col, row) on the standard wide-form periodic-table grid. `Subscripts` provides char-level Unicode super/subscript helpers (no Console.Lib dep — usable from anywhere).
- **`src/PT.Tui`** — `pt-tui` exe. Renders the table via `Console.Lib`. Entry point in `Program.cs`; the table itself is `PeriodicTableWidget`; the bottom card is `DetailPanel`. Soft-rendered text prototype lives under `Soft/` (see below).
- **`src/PT.Tests`** — xUnit.v3 + Shouldly. `net10.0` (no -windows) so it runs on Linux CI. Covers element-data invariants, layout positioning, and Unicode subscript mappings.

### Element data provenance

Symbols / atomic numbers / group / period were cross-referenced against `../physics/atoms.pl` (Prolog knowledge base). That source has known bugs that are corrected here:

- Sulfur was Z=15 (collision with phosphorus); fixed to Z=16.
- Hafnium had period 7; fixed to 6.
- Name typos `flurine`/`ribidium`/`tellerium` corrected.
- Elements 113/115/117/118 use the modern IUPAC names (Nh / Mc / Ts / Og), not the systematic placeholders.

Atomic weights, atomic-mass synthetic flags, electron configurations, and category assignments are not in the Prolog and were embedded directly. Tests assert the corrected values.

### Layout convention

The widget renders 18 cols × 9 rows. Periods 1–7 are the main grid. Lanthanides (Z=57–71) occupy row 8 cols 3–17; actinides (Z=89–103) row 9 cols 3–17. La and Ac live only in the f-block rows; the (col=3, row=6) and (col=3, row=7) cells in the main grid render as "*" placeholders pointing to the f-block — this is the most common educational layout (not the IUPAC 2021 group-3 = Lu/Lr variant).

Each cell is 5 cols × 3 rows (atomic number top-left, symbol centered, atomic mass bottom; the 5th col is padding so adjacent masses don't run together). One blank row separates main grid from f-block. Total rendered area: 90 × 28 terminal cells.

### Soft-rendered text (`Console.Lib.SoftText`)

Console.Lib treats one char = one cell. The periodic table cell + decay chain panel both need multi-cell rectangular text blocks with per-line alignment and styled spans. `Console.Lib.SoftText` / `SoftLine` / `SoftSpan` model that and `Console.Lib.SoftRenderer` paints into a viewport rectangle. `Console.Lib.Subscripts` provides Unicode ⁰¹²₀₁₂ helpers.

These were prototyped under `src/PT.Tui/Soft/` and `src/PT/Subscripts.cs`; they have been promoted upstream and are now consumed via `using Console.Lib;` directly. `SixelDecayChainPanel` and `OrbitalPanel` still live under `src/PT.Tui/Soft/` because they are chemistry-specific and not generic enough to promote.

### Sixel transparency (reverted)

`SixelDecayChainPanel` and `OrbitalPanel` initialise their Sixel surface with an opaque-black fill (alpha=255). Earlier versions used alpha=0 + P2=1 so the encoder would skip empty pixels and the terminal would preserve the underlying pre-painted text cells, saving Sixel bytes. That worked on xterm/foot but broke on Windows Terminal: WT does not clear already-emitted Sixel pixels when the underlying cell is overwritten with a text space, so transparent regions in a new frame leaked the previous frame's tiles/lobes (visible as "Th/Pa/Ra/U" pile-ups in the decay-chain strip when stepping through actinides whose chain layouts differ).

Each frame now fully repaints its band, eliminating ghosting at the cost of a larger Sixel payload — acceptable since these panels only redraw on input. `SixelDecayChainPanel`'s `WriteRow` blanking of rows 2-3 and `OrbitalPanel`'s canvas-row pre-clear loop are kept as belt-and-suspenders for the text-fallback path and in case alpha-0 is reintroduced.

### Font path resolution

Use `DIR.Lib.FontResolver.ResolveSystemFont()` to find a system monospace TTF. Returns `""` (not `null`) when no candidate exists — `Program.cs` maps empty to `null` because the panels' `string? fontPath` parameter signals "Sixel disabled" via `null`.

### OSC 52 clipboard

The `y` keybind copies the current decay-chain plain text via `Console.Lib.Clipboard.SetText(term, text)`. The Sixel-rendered isotope notation isn't selectable via the terminal's drag-select, so this is the path for "copy the chain text" when Sixel is on.

### TUI controls — clickable isotopes

In addition to navigating the periodic table, the decay-chain panel registers cell-aligned hit regions per isotope tile (Sixel path) or per isotope token (text-fallback path). Clicking maps back to the underlying `Isotope`, and `PeriodicTableWidget.SelectByZ` jumps the table selection. Mouse dispatch in `Program.cs` tries `chainPanel.TryClick(m, out _)` first, then falls back to `table.HandleMouse(m)`.

The `y` keybind yanks the current chain via OSC 52 (see above). The Sixel-rendered isotope notation isn't selectable via the terminal's drag-select, so this is the path for "copy the chain text" when Sixel is on.

### Console.Lib relationship

`Console.Lib` is a NuGet dependency, but its source lives at `../../sharpastro/Console.Lib` (separate repo). When iterating on widget changes, edit upstream, push to `SharpAstro/Console.Lib` `main`, wait for CI to publish (`2.<minor>.<run_number>`), then bump `<PackageReference Version>` in `PT.Tui.csproj`. CI run numbers tick by ~10 per push for that repo. `DIR.Lib` is pulled in transitively via Console.Lib.

### TUI controls

Arrow keys move selection (skipping blank cells and the f-block gap). Mouse-click selects a cell. `Home` jumps to H, `End` to Og. `q` or `Esc` quits.

### Adding fields to `Element`

The `Element` record is a public contract. When extending it (e.g. atomic radius, electronegativity), add the field as nullable if you can't supply it for all 118 elements, and add a unit test asserting which elements have non-null values. Don't break existing tests by changing semantics of existing fields (e.g. `AtomicWeight` is always set; synthetics use the most-stable mass number, signalled by `IsSynthetic`).

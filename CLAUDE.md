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

### Soft-rendered text (`PT.Tui/Soft/` — pending Console.Lib bump)

Console.Lib treats one char = one cell. The periodic table cell + decay chain panel both need multi-cell rectangular text blocks with per-line alignment and styled spans. `SoftText` / `SoftLine` / `SoftSpan` model that and `SoftRenderer` paints into a viewport rectangle. `Subscripts` (in `src/PT`) gives Unicode ⁰¹²₀₁₂ helpers.

**Status:** these have been **promoted upstream into Console.Lib `main`** as `Console.Lib.SoftText` / `SoftRenderer` / `Subscripts` (commit `78a93d7`). They will ship in the next published `2.4.<run>` package. Once that lands:

1. Bump `<PackageReference Include="Console.Lib" Version="2.4.X">` in `PT.Tui.csproj` to the new run number.
2. Delete `src/PT.Tui/Soft/SoftText.cs`, `src/PT.Tui/Soft/SoftRenderer.cs`, and `src/PT/Subscripts.cs`.
3. Replace `using PeriodicTable.Tui.Soft;` with `using Console.Lib;` in the consumers (`PeriodicTableWidget.cs`, `SixelDecayChainPanel.cs`).
4. Replace `using PeriodicTable;` (for `Subscripts`) with the same `using Console.Lib;` in `SixelDecayChainPanel.cs`.

`SubscriptsTests.cs` here can also be deleted — equivalent tests now live in Console.Lib.Tests.

`SixelDecayChainPanel` stays here — it's chemistry-specific and not generic enough to promote.

### Font path resolution (`SixelDecayChainPanel.FindSystemFont` — pending DIR.Lib bump)

`FindSystemFont()` inside `SixelDecayChainPanel` walks a list of candidate TTF paths (Consolas/Courier on Windows, Menlo/DejaVu otherwise) to feed `RgbaImageRenderer.DrawText`. The same logic exists in `tianwen/src/TianWen.UI.Abstractions/FontResolver.cs`.

**Status:** promoted upstream into `DIR.Lib` `main` as `DIR.Lib.FontResolver.ResolveSystemFont()` (DIR.Lib commit `c5e2013`). Note the upstream API returns `string` ("" for not found), not `string?` — match its semantics when migrating.

Once a new DIR.Lib package ships *and* a Console.Lib release transitively pulls it in (Console.Lib's `<ProjectReference>` to DIR.Lib falls back to the package when the sibling repo isn't present, so the package consumer chain is what matters):

1. Delete `FindSystemFont` from `src/PT.Tui/Soft/SixelDecayChainPanel.cs`.
2. In `Program.cs`, replace
   ```csharp
   var fontPath = term.HasSixelSupport ? SixelDecayChainPanel.FindSystemFont() : null;
   ```
   with
   ```csharp
   var resolved = term.HasSixelSupport ? DIR.Lib.FontResolver.ResolveSystemFont() : "";
   string? fontPath = resolved.Length > 0 ? resolved : null;
   ```
   The `string? fontPath` panel parameter stays — empty-string from the resolver maps to `null` for the panel's own "Sixel disabled" branch.

### OSC 52 clipboard (`Program.WriteOsc52` — pending Console.Lib bump)

The `y` keybind copies the current decay-chain plain text to the system clipboard via OSC 52. The local `WriteOsc52(IVirtualTerminal, string)` helper in `Program.cs` is a placeholder — the same logic has been promoted upstream as `Console.Lib.Clipboard.SetText(ITerminalViewport, string)` (Console.Lib commit `3f74dab`).

Once a Console.Lib package containing it ships:

1. Delete `WriteOsc52` from `src/PT.Tui/Program.cs` (and the `using System.Text;` if no longer needed).
2. Replace the call site `WriteOsc52(term, text);` with `CL.Clipboard.SetText(term, text);`.

### TUI controls — clickable isotopes

In addition to navigating the periodic table, the decay-chain panel registers cell-aligned hit regions per isotope tile (Sixel path) or per isotope token (text-fallback path). Clicking maps back to the underlying `Isotope`, and `PeriodicTableWidget.SelectByZ` jumps the table selection. Mouse dispatch in `Program.cs` tries `chainPanel.TryClick(m, out _)` first, then falls back to `table.HandleMouse(m)`.

The `y` keybind yanks the current chain via OSC 52 (see above). The Sixel-rendered isotope notation isn't selectable via the terminal's drag-select, so this is the path for "copy the chain text" when Sixel is on.

### Console.Lib relationship

`Console.Lib` is a NuGet dependency, but its source lives at `../../sharpastro/Console.Lib` (separate repo). When iterating on widget changes, edit upstream, push to `SharpAstro/Console.Lib` `main`, wait for CI to publish (`2.4.<run_number>`), then bump `<PackageReference Version>` in `PT.Tui.csproj`. CI run numbers tick by ~10 per push for that repo.

### TUI controls

Arrow keys move selection (skipping blank cells and the f-block gap). Mouse-click selects a cell. `Home` jumps to H, `End` to Og. `q` or `Esc` quits.

### Adding fields to `Element`

The `Element` record is a public contract. When extending it (e.g. atomic radius, electronegativity), add the field as nullable if you can't supply it for all 118 elements, and add a unit test asserting which elements have non-null values. Don't break existing tests by changing semantics of existing fields (e.g. `AtomicWeight` is always set; synthetics use the most-stable mass number, signalled by `IsSynthetic`).

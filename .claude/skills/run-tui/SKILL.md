---
name: run-tui
description: Build and launch pt-tui in a new Windows terminal window so it runs interactively (with a real TTY), and drive it non-interactively via the Console.Lib debug inspector. Use when the user asks to "run the TUI", "launch the TUI", "try the TUI", wants to manually test TUI changes, or when you need to verify a rendering/layout change yourself.
---

# run-tui

The TUI (`src/PT.Tui`) detects redirected stdio and falls back to a non-interactive table dump, so it can't be run inside the Claude Code shell. This skill launches it in a separate console window where it has a real terminal — and, via the debug inspector, lets you **drive and assert it yourself** instead of asking the user to click things.

## Steps

1. **Build first** (Debug, AOT off, fast). Run from the repo root:
   ```bash
   dotnet build src/PT.Tui/PT.Tui.csproj -c Debug
   ```
   If the build fails, stop and surface the error — do not launch.

2. **Pick the exe path**. Debug build output:
   ```
   src/PT.Tui/bin/Debug/net10.0/pt-tui.exe
   ```

3. **Launch in a new window** using PowerShell's `Start-Process`. Do **not** use `cmd //c start "title" cmd /k ...` from Git Bash — bash strips the title quotes and `start` then tries to run the title as a program ("Windows cannot find 'pt-tui'"). PowerShell handles quoting cleanly:
   ```bash
   powershell -NoProfile -Command "Start-Process cmd -ArgumentList '/k','src\PT.Tui\bin\Debug\net10.0\pt-tui.exe'"
   ```
   - `cmd /k` keeps the window open after the TUI exits so any startup error or final summary is visible.
   - Inside the single-quoted `ArgumentList`, use **backslash** paths (parsed by cmd).

4. **Don't poll the launched process.** It runs in its own window with its own input. If you are not driving it via the inspector, tell the user the window is open and let them drive it. Common keys to mention: `←↑→↓` navigate, `Home/End` first/last element, `y` yank chain, `q` or `Esc` quit.

## Driving it yourself: the debug inspector

Do **not** ask the user to click things you can drive. The TUI has a DEBUG-only inspector — a loopback JSON command server that reads the screen as TEXT and injects keys and clicks.

**It needs a Debug build AND local siblings**, and that second part is not optional: the inspector lives in Console.Lib / DIR.Lib behind `#if DEBUG`, and a *published* package is built in Release, so its inspector is compiled out. `PT.Tui.csproj` gates the wiring on `CONSOLE_INSPECTOR`, defined only when `UseLocalSiblings=true` and `Configuration=Debug`. Locally that is the default (`src/Directory.Build.props` auto-detects the sibling checkouts); in CI it is absent, which is why CI never compiles this code and the AOT publish never contains it.

Set `PT_INSPECTOR=1` to enable it. That also enables the cell buffer, which is what gives `screen` any content.

```bash
powershell -NoProfile -Command '$env:PT_INSPECTOR="1"; Start-Process cmd -ArgumentList "/k","src\PT.Tui\bin\Debug\net10.0\pt-tui.exe 2> inspector.log"'
until [ -s inspector.log ]; do sleep 0.3; done
PORT=$(grep -oE '127\.0\.0\.1:[0-9]+' inspector.log | cut -d: -f2)
```

The banner goes to **stderr** (a TUI owns stdout), hence the redirect. `*.log` is gitignored.

**Launch ONE instance and reuse it.** Each one holds the sibling Console.Lib / DIR.Lib DLLs open, so a stray instance makes the next `dotnet build` fail with MSB3021 file-in-use. `taskkill //F //IM pt-tui.exe` before rebuilding.

**Say so when you kill it.** `cmd /k` keeps the window open, so a killed app leaves a bare command prompt on screen — indistinguishable from a crash to whoever is looking at that window. Tell the user you killed it, in the same message. To tell the two apart: `taskkill` leaves **no** stack trace in `inspector.log`, a real crash does; and a live app answers `ping`.

Check for the process with the filtered form, `tasklist //FI "IMAGENAME eq pt-tui.exe"`. `tasklist | grep -i pt` is a trap — dozens of `dotnet.exe` rows sort first and `head` cuts off before any `pt-tui` line, so a running app reads as absent.

Protocol: newline-delimited JSON over TCP, `{"id":1,"method":"m","params":{}}` →
`{"id":1,"result":...}` or `{"id":1,"error":"..."}`.

| method | what it gives you |
|---|---|
| `ping` | `{ok, protocol, app}` — `app` is `"pt-tui"` |
| `size` | grid, cell size, and whether the terminal is buffered |
| `screen` | **every row as text** — the periodic table is TEXT, so assert it here |
| `row` `{row}` / `cell` `{column,row}` | one row; one cell's glyph, kind and pen |
| `appState` | selection (`z`/`symbol`/`name`/`category`/`period`/`block`), the frame's decision (`shape`, `detailRows`, `chainRows`, `orbitalCols`, `orbitalPanel`, `sixel`), the decay `chain` as plain text, and the paint accounting |
| `inputLog` | last 64 events **with the state each changed** |
| `key` `{key}` `{mods}` | a keystroke; `mods` for a chord (`"Ctrl"`, `"Ctrl+Shift"`) |
| `click` `{column,row}` | press+release at that cell's centre |
| `batch` `{steps}` | run `[{method,params}, …]` one per pump, results as an array |
| `wait` `{frames}` | idle N frames — only meaningful as a batch step |

`batch` and `wait` come from the shared core (DIR.Lib 7.3), not from Console.Lib, so the TUI gained them without a line of terminal code. A failing step is recorded in place (`"error: …"`) and the batch still completes, so a long script reports *which* step broke.

Key names: single letters, `Home`/`End`/`Esc`, `left`/`right`/`up`/`down` or their `ConsoleKey` spellings (`RightArrow`). A bare number is refused.

### Two ways to drive it

**MCP (preferred).** `.mcp.json` registers `tui-inspector` via `dnx Console.Lib.Inspector --yes`, which finds the running app by UDP discovery — no port to copy. Tools: `list_instances`, `screen`, `row`, `cell`, `app_state`, `input_log`, `key`, `keys`, `click`, `size`, `ping`.

**Script.** `proof_inspector.py <port>` in this skill directory is a worked driver and a regression check: it pings, asserts the table reached the cell plane, `Home`s to hydrogen, plays `RightArrow` and `End` by injected keys, and asserts each reaches `appState` and the input log. It is re-runnable against an instance a previous run already moved.

### Gotchas worth knowing up front

- **`screen` and `inputLog` return OBJECTS, not arrays** — `{"rows":[…]}` and `{"events":[…]}`. Indexing the result as a list silently iterates the KEY names instead, so assertions pass against the string `"rows"` and a broken screen reads as fine. This cost a full debugging detour; unwrap them.
- **The table is text; the decay chain and the orbital panel are Sixel.** Unlike chess's board, `screen` *can* assert the periodic table, the header, the detail panel and the status bar. It cannot assert the chain or orbital bands — those cells report `kind: "Image"` with no glyphs, which is correct, not a fault. Assert them via `appState.chain` (the plain-text chain rides along for exactly this reason) and `appState.sixel`.
- **`appState.sixel` tells you which path you are on.** With no system font the panels silently take the text-fallback path, and a blank band means something completely different there.
- **You cannot resize the window from out here, so don't try.** Under ConPTY the terminal emulator owns the window size: `SetConsoleScreenBufferSize` against the app's `CONOUT$` (even after `AttachConsole`) returns false, `mode con:` in the launching `cmd` is ignored, and the inspector has no `resize` verb — `size` keeps reporting the real window either way. This matters because re-deriving the frame on resize is what `ViewerFrameLayout` is FOR. That path is covered by `FrameHostTests` instead, which places the same hosted viewports at one size and then another; `appState.shape` / `orbitalCols` are what you assert at whatever size the window happens to be.
- **`appState.shape` is the frame's own answer, not a startup constant.** It is re-read from the live `ViewerFrameLayout` per call, so `1 + 1 + detailRows + chainRows <= size.rows` and `size.columns - orbitalCols >= 90` are checkable invariants — `proof_inspector.py` asserts both.
- **The cell buffer must be enabled BEFORE any widget exists.** It is turned on in `Main` right after `EnterAlternateScreen`, not inside `RunUi` — the same placement TianWen's TUI uses. Anything written before it is on sits in a buffer nothing has flushed.
- **This needs the `_bufferedSize` seed in `VirtualTerminal.EnableCellBuffer`.** Without it the first `Flush` reads as a resize, re-`Resize`s the buffer and blanks everything the frame just painted — leaving only what happened to be written after that flush (in PT: the orbital panel alone, because it blits before writing its text). Apps that repaint unconditionally heal on frame two; PT paints on demand and loses the content permanently. If the table renders blank with only the orbital panel visible, that fix has been lost.
- `appState` also reports `flushedCells` / `flushedOpaqueCells` / `lastFlushRuns` (Console.Lib 4.8 paint accounting). The totals say how much went out; `lastFlushRuns` says *which* cells, which is the question when the counts look fine and the screen does not. A nonzero `flushedOpaqueCells` means some pens are outside the modelled SGR vocabulary and those cells re-emit every frame — a perf smell, not a correctness bug.

## Release build (optional)

If the user explicitly asks for the published AOT binary, build with:
```bash
dotnet publish src/PT.Tui/PT.Tui.csproj -c Release
```
and launch from `src/PT.Tui/bin/Release/net10.0/win-arm64/publish/pt-tui.exe` instead. Otherwise stick with Debug — it builds in seconds and the TUI is not CPU-bound. Note the inspector is **not** compiled into a Release build.

$ARGUMENTS

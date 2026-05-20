---
name: run-tui
description: Build and launch pt-tui in a new Windows terminal window so it runs interactively (with a real TTY). Use when the user asks to "run the TUI", "launch the TUI", "try the TUI", or wants to manually test TUI changes.
---

# run-tui

The TUI (`src/PT.Tui`) detects redirected stdio and falls back to a non-interactive table dump, so it can't be run inside the Claude Code shell. This skill launches it in a separate console window where it has a real terminal.

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

4. **Don't poll the launched process.** It runs in its own window with its own input. Tell the user the window is open and let them drive it. Common keys to mention: `←↑→↓` navigate, `Home/End` first/last element, `q` or `Esc` quit.

## Release build (optional)

If the user explicitly asks for the published AOT binary, build with:
```bash
dotnet publish src/PT.Tui/PT.Tui.csproj -c Release
```
and launch from `src/PT.Tui/bin/Release/net10.0/win-arm64/publish/pt-tui.exe` instead. Otherwise stick with Debug — it builds in seconds and the TUI is not CPU-bound.

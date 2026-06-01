# Jot — plan

A lightning-fast, single-file text and code editor for Windows 11. It opens one file at a
time, almost instantly, with syntax highlighting and formatting for the major languages,
GitHub-style Markdown preview, code folding, smart bracket handling, and find/replace with
regular expressions. It is driven from a system-wide hotkey (default `Ctrl+Space`) that opens
the file currently selected in File Explorer, and from a normal application launch that reopens
the last file.

The name is **Jot**; the executable is `jot.exe`.

---

## 1. Goals and non-goals

**Goals**
- Open near-instantly on Windows 11. Performance beats portability everywhere they conflict.
- Edit exactly one file at a time. No project/workspace concept.
- Syntax highlighting for all major languages, chosen by file extension, otherwise auto-detected
  by content, and manually overridable.
- One-key code formatting honouring a system-wide config (defaults: 4-space indent, opening brace
  on the same line, trim trailing whitespace, final newline).
- GitHub-style Markdown preview.
- Code folding, smart auto-closing brackets, and find/replace including regex.
- A system-wide hotkey to open the Explorer-selected file; `Esc` closes the window; relaunching
  with no file reopens the last file if it still exists.
- A single, idempotent installer that installs on first run and updates on re-run.
- Published to GitHub with a genuinely useful README and a LICENSE.

**Non-goals (v1)**
- Multiple files/tabs, project trees, integrated terminal, debugging, extensions, plugins.
- Heavyweight language servers. (Basic error checking is a later branch + PR, see §11.)

---

## 2. Technology decision

**Chosen stack: C# on .NET 8, Avalonia 11 UI, AvaloniaEdit editor control, TextMateSharp for
grammars.** Framework-dependent build (small binary); the installer guarantees the runtime.

Why this stack:
- **AvaloniaEdit** is the maintained port of AvalonEdit (the editor behind ILSpy/SharpDevelop):
  it gives line numbers, virtualised rendering of large files, code folding, and a search panel
  out of the box, as a *native* control (no web view for editing → fast).
- **TextMateSharp** plugs into AvaloniaEdit and ships the same Grammar/theme set VS Code uses, so
  "all major languages" highlighting is a maintained dependency rather than hand-written `.xshd`.
- **.NET 8** is installed on the target machine (Desktop runtimes 8/9/10 present) and is LTS, so
  framework-dependent distribution is safe and keeps the binary tiny.
- Avalonia starts fast; combined with a warm background instance (see §6) repeat opens are instant.

**Markdown preview**: render Markdown → GitHub-styled HTML and show it in an embedded WebView2
(runtime already present: 148.x) via an Avalonia WebView integration. If that integration proves
fragile, fall back to the managed `Markdown.Avalonia` control styled to approximate GitHub. This
is the single highest-risk area and is isolated behind an interface so the fallback is a drop-in.

**Rejected alternatives** (recorded for the review):
- *Electron / VS Code-like*: far too slow to start; violates the core requirement.
- *Tauri (Rust + WebView2)*: good, but editing inside a web view starts slower than a native
  control and adds a JS build pipeline; native Avalonia is leaner for this use.
- *WPF + AvalonEdit*: excellent startup and rock-solid on Windows, but "all-languages" highlighting
  would mean assembling/maintaining many `.xshd` files or writing a TextMate→AvalonEdit bridge by
  hand. AvaloniaEdit already has the TextMate bridge, so it wins on effort and coverage.

---

## 3. Repository layout

```
Jot.sln
src/Jot/                     # the app (single project → single exe)
  Jot.csproj
  Program.cs                 # entry point, arg parsing, mode dispatch
  App.axaml(.cs)             # Avalonia app
  Editor/                    # EditorView, language detection, formatting, folding, brackets
  Markdown/                  # preview renderer + WebView host (+ managed fallback)
  Search/                    # find/replace incl. regex
  Config/                    # config model, load/save/merge, defaults
  Platform/                  # Win32 interop: global hotkey, Explorer selection, single-instance pipe
  Assets/                    # icon, bundled GitHub markdown css, themes
tests/Jot.Tests/             # xUnit: config, detection, formatters, search, pipe protocol
install/
  install.ps1                # idempotent install/update; fetches release, ensures runtime, wires up
  Install.cmd                # double-click wrapper → powershell -ExecutionPolicy Bypass -File install.ps1
  uninstall.ps1
README.md
LICENSE                      # MIT
PLAN.md
.gitignore
.github/workflows/build.yml  # build + test + (on tag) publish release artifacts
```

Single-file aim: the **app** is one project producing one `jot.exe` plus its dependency DLLs in
the install folder. "As small as possible" is interpreted as a small framework-dependent binary;
self-contained single-file (~30–40 MB) is the fallback if runtime presence proves unreliable.

---

## 4. Editor features

- **Highlighting**: language resolved by extension first; else content/shebang auto-detect; manual
  override via a status-bar language menu and a keybind. Dark GitHub-like theme by default.
- **Folding**: brace-based for C-family/JSON; indentation-based for YAML/Python-like.
- **Smart brackets**: typing an opener inserts the matching closer only when sensible (not before a
  word char); typing the closer over an auto-inserted one "types through" it; selection wrapping;
  backspace of an empty pair removes both. Configurable on/off.
- **Auto-indent** on newline honouring `indentSize`/`insertSpaces`; brace-aware extra indent.
- **Find/replace**: incremental find, replace, replace-all, with case-sensitive, whole-word, and
  **regex** toggles. Keys: `Ctrl+F`, `Ctrl+H`, `F3`/`Shift+F3`, `Esc` closes the panel.
- **Format document** (`Ctrl+Shift+F` and `Alt+Shift+F`): JSON, XML/HTML, and YAML get correct
  structural reformatting; C-family/generic gets brace+indent normalisation per config; unknown
  types optionally piped to an external formatter named in config, else a no-op with a status note.
- **Markdown preview** (`Ctrl+Shift+V`): split or toggled view, GitHub CSS, live-updates on edit.
- **Saving**: `Ctrl+S`; trims trailing whitespace / ensures final newline per config; preserves the
  file's existing line-ending style and encoding (BOM aware).
- Line numbers, current-line highlight, optional whitespace/word-wrap, large-file friendliness.

---

## 5. Configuration (system-wide, per user)

`%APPDATA%\Jot\config.json`, created with documented defaults on first run; openable in the editor
itself (a menu item / keybind). State (last file, window size) in `%APPDATA%\Jot\state.json`.

Defaults: `indentSize: 4`, `insertSpaces: true`, `braceStyle: "same-line"`,
`trimTrailingWhitespace: true`, `insertFinalNewline: true`, `wordWrap: false`, `theme: "dark"`,
`fontFamily: "Cascadia Code"`, `fontSize: 13`, `hotkey: "Ctrl+Space"`, `backgroundAgent: true`,
`autoCloseBrackets: true`, plus `languageOverrides` and optional `externalFormatters` maps.
Unknown keys are preserved; missing keys fall back to defaults (forward/backward compatible merge).

---

## 6. Launch model, hotkey, and Explorer integration

One exe, three entry behaviours:
- `jot.exe <path>` — open that file. If the background agent is running, hand the path to it over a
  named pipe so it shows instantly in the warm process; otherwise start normally.
- `jot.exe` — reopen the last file if it still exists, else an empty buffer.
- `jot.exe --agent` — hidden, autostarted at login: owns single-instance, keeps a warm window,
  registers the global hotkey, and listens on the named pipe.

**Global hotkey** (default `Ctrl+Space`): the agent creates a hidden message-only Win32 window via
P/Invoke, calls `RegisterHotKey`, and pumps messages on a dedicated thread. On trigger it reads the
**foreground Explorer window's selected item** through the Shell COM automation
(`Shell.Application`→`Windows()`, matched to `GetForegroundWindow`) and opens it. If the foreground
window is not Explorer or nothing is selected, it opens the last file. If `RegisterHotKey` fails
(e.g. PowerToys Peek owns `Ctrl+Space`), the installer/README explain how to free it, and a context
menu provides a reliable fallback (below). The hotkey is configurable.

**Explorer context menu**: installer registers an "Edit with Jot" verb so right-click → Edit always
works regardless of hotkey conflicts.

**Esc** hides the window (keeping the agent warm) so reopening is instant; with the agent disabled,
`Esc` exits the process. Single-instance ensures a second launch reuses the first.

---

## 7. Installer (single, idempotent)

`Install.cmd` (double-click) runs `install.ps1 -ExecutionPolicy Bypass`. `install.ps1`:
1. Ensures the .NET 8 runtime is present; if not, installs via `winget` (documented fallback link).
2. Downloads the latest release zip from GitHub Releases (or uses a local `-FromBuild` path for dev),
   and extracts to `%LOCALAPPDATA%\Jot\` (stopping any running agent first).
3. Registers the agent to autostart (per-user `Run` key) and starts it.
4. Registers the "Edit with Jot" context-menu verb and a Start-Menu shortcut.
5. Writes default config if absent (never overwrites an existing one).
Re-running performs an in-place update (steps 2–4) without prompts. `uninstall.ps1` reverses it.

The first release is built locally and published with `gh release create`; thereafter the installer
always pulls the latest, so re-running updates.

---

## 8. Testing strategy

- **xUnit unit tests**: config defaults/merge/round-trip; language detection (extension + content +
  shebang); JSON/XML/YAML formatter correctness; C-family brace/indent formatter (same-line braces,
  4-space); find/replace incl. regex edge cases; pipe message framing; last-file persistence.
- **Avalonia.Headless tests** where practical: smart-bracket typing, format keybind, Esc behaviour.
- **Manual/run verification** of the GUI via the run skill on each milestone.
- CI (`build.yml`) builds and runs tests on push; tagged commits also publish release artifacts.
- Subagent code review at each milestone and a security review before release.

---

## 9. Build milestones (each ends compiling + tested + reviewed)

1. **Skeleton**: solution, project, window opens a file passed on the command line; saves; line
   numbers. Verify it launches.
2. **Highlighting + detection**: TextMateSharp wired; extension/content detection; manual override.
3. **Editing UX**: smart brackets, auto-indent, folding, find/replace incl. regex.
4. **Config + formatting**: config load/save/defaults; format-document for JSON/XML/YAML/C-family.
5. **Markdown preview**: GitHub-styled preview (WebView2, managed fallback).
6. **Launch model**: single-instance pipe, warm agent, last-file reopen, Esc-hides.
7. **Hotkey + Explorer**: global `Ctrl+Space`, Explorer selection, context-menu verb.
8. **Installer + docs**: `install.ps1`/`Install.cmd`/`uninstall.ps1`, README, LICENSE, icon.
9. **Publish**: GitHub repo, CI, first release; verify the installer end-to-end on a clean path.

---

## 10. Conventions

Australian spelling, hyphens instead of em/en dashes, Oxford commas, in all of our own code and
docs. No reference to Claude or any AI tool anywhere — including commit messages and authorship
(no co-author trailer). MIT licence; copyright holder set to the user's GitHub identity.

---

## 11. Later: basic error checking (separate branch + PR)

After main is complete and tested, branch `feature/diagnostics`: lightweight, fast, in-process
checks only — JSON and YAML parse errors with squiggles and a status summary, optional JSON Schema
validation, and bracket-balance hints. No external language servers. Land via PR, not on main.

---

## 12. Revisions after subagent review

Decisions adopted from the architecture review:

- **Milestone 0 — de-risking spikes first.** Before the skeleton, prove the only two items with
  real "does this work here" risk: (a) a WebView2 surface inside Avalonia rendering a styled HTML
  string, and (b) `RegisterHotKey(Ctrl+Space)` plus reading the foreground Explorer selection via
  Shell COM. Everything else is well-trodden.
- **Startup**: enable `PublishReadyToRun=true` (R2R) — the highest-leverage cold-start win and
  compatible with framework-dependent. The warm agent is load-bearing for "instant", so it is fully
  warmed at login (window created and hidden, render context up), not lazily on first trigger.
- **Distribution**: stay **framework-dependent .NET 8** (smallest binary, per the brief) with
  `RollForward=LatestMinor`, targeting the installed `8.0.x` runtime; the installer guarantees the
  runtime (winget, with a documented direct-download fallback). Also publish a self-contained zip as
  a secondary artifact for machines without the runtime. NativeAOT is explicitly out (AvaloniaEdit +
  TextMateSharp + XAML reflection + WebView2 interop fight trimming/AOT).
- **Hotkey/Shell COM threading**: the `RegisterHotKey` message pump runs on a dedicated thread; the
  hotkey handler posts an event and the Shell-COM selection read happens on a properly initialised
  **STA** worker (avoids `RPC_E_*`). Desktop (`progman`/`WorkerW`) selection is out of scope for v1
  and documented. Elevated-Explorer selection is unreadable from a non-elevated agent (documented;
  the context-menu verb is the guaranteed path). Hotkey-registration failure is surfaced via the
  tray icon, not swallowed.
- **Single-instance**: a per-user named **mutex** gates single-instance; the per-user named **pipe**
  (named with the user SID, `PipeSecurity` restricted to the current user) is the IPC channel.
- **Tray icon** for the agent: exit, open last file, open config, and current-hotkey status — for
  trust and a kill/restart story.
- **Formatting scope**: real structural formatting only for **JSON, XML/HTML, and YAML**. No
  hand-rolled C-family brace-reflow engine in v1; other types get whitespace-only normalisation
  (trim trailing, indent width, final newline) plus optional pass-through to an external formatter
  named in config (e.g. `prettier`, `clang-format`) when present.
- **Markdown preview**: spike WebView2 first; if the integration is clean, use it for GitHub-fidelity
  rendering (runtime present), with managed `Markdown.Avalonia` as the isolated fallback. Created
  lazily on first preview.
- **Encoding/EOL**: detect and round-trip BOM, encoding, and dominant line-ending exactly; unit
  tests cover UTF-8 (no BOM/BOM), UTF-16LE BOM, CRLF/LF/mixed, and no-final-newline cases.
- **Large files**: guardrail — disable highlighting above a size threshold and for very long single
  lines (e.g. minified files) to protect responsiveness.
- **CI** runs on `windows-latest` (Shell COM, WebView2, hotkey, installer are Windows-only).
- **Code-signing**: no cert for v1; README documents the SmartScreen/AV first-run prompt and how to
  proceed, since an unsigned autostart app with a global hotkey will trip them.
- **Identity**: GitHub repo under `KingsMMA`; MIT `Copyright (c) 2026 KingsDev`.

## 13. Open risks

- Markdown preview WebView integration on Avalonia (mitigated by the managed fallback).
- `Ctrl+Space` conflict with PowerToys Peek (mitigated by configurability + context-menu fallback;
  documented).
- Reading the Explorer selection across elevation boundaries (document the limitation).
- External formatters are opt-in; built-in coverage is JSON/XML/YAML/C-family/generic only.

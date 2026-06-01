<div align="center">
  <img src="src/Jot/Assets/jot.png" width="96" alt="Jot logo">
  <h1>Jot</h1>
  <p>A lightning-fast, single-file text and code editor for Windows 11.</p>
</div>

Jot opens one file at a time, almost instantly. It is built for the quick edits you make all day:
tweaking a JSON or YAML file, fixing a config, jotting a note, or previewing some Markdown. Select a
file in File Explorer, press `Ctrl+Space`, edit, and you are done.

## Features

- **Opens instantly.** A warm background agent keeps Jot ready, so windows appear the moment you ask.
- **Syntax highlighting for every major language**, chosen from the file extension, the content, or
  picked by hand from the status bar.
- **Format on a key.** `Ctrl+Shift+F` tidies JSON and XML structurally, normalises whitespace
  elsewhere, and can call an external formatter you nominate (such as Prettier or clang-format).
- **GitHub-style Markdown preview** in a live split view (`Ctrl+Shift+V`).
- **Smart editing:** auto-closing brackets and quotes that type through, brace-aware indentation,
  and code folding.
- **Find and replace** with case, whole-word, and regular-expression matching.
- **One system-wide configuration** with sensible defaults: four-space indents, braces on the same
  line, trimmed trailing whitespace, and a final newline.
- **Open from File Explorer** with `Ctrl+Space` on the selected file, or right-click and choose
  **Edit with Jot**.

## Requirements

- Windows 10 or Windows 11 (Windows 11 is the focus).
- The .NET 8 runtime. The installer installs it for you automatically if it is missing, so there is
  nothing to set up by hand.

## Install

Open **PowerShell** and run:

```powershell
irm https://raw.githubusercontent.com/KingsMMA/jot/main/install/install.ps1 | iex
```

That downloads Jot, installs it under your user account (no administrator rights needed), starts the
background agent, and adds the `Ctrl+Space` hotkey and the **Edit with Jot** right-click entry.

Prefer to do it by hand? Download `Jot-installer.zip` from the
[latest release](https://github.com/KingsMMA/jot/releases/latest), extract it, and double-click
`Install.cmd`.

### Updating

Run the same command again. Jot updates itself in place to the latest version.

## Using Jot

- **From File Explorer:** select a file and press `Ctrl+Space`, or right-click it and choose
  **Edit with Jot**.
- **From the Start menu or a pinned shortcut:** opens the last file you were editing.
- **Press `Esc`** (or close the window) to put Jot away. It reopens instantly, because the background
  agent stays ready. Use the tray icon's **Exit Jot** to close it completely.

### Keyboard shortcuts

| Shortcut | Action |
| --- | --- |
| `Ctrl+Space` (anywhere) | Open the file selected in File Explorer |
| `Ctrl+S` | Save |
| `Ctrl+F` | Find |
| `Ctrl+H` | Replace |
| `Ctrl+Shift+F` or `Alt+Shift+F` | Format the document |
| `Ctrl+Shift+V` | Toggle the Markdown preview |
| `Ctrl+,` | Edit the configuration |
| `Esc` | Close the find panel, or hide the window |

## Configuration

Jot reads one configuration file that applies to every file you edit. There are no per-project
settings. Open it any time with `Ctrl+,`, or from the tray icon's **Edit configuration**. It lives at:

```
%APPDATA%\Jot\config.json
```

The defaults:

```json
{
  "indentSize": 4,
  "insertSpaces": true,
  "braceStyle": "same-line",
  "trimTrailingWhitespace": true,
  "insertFinalNewline": true,
  "wordWrap": false,
  "theme": "dark",
  "fontFamily": "Cascadia Code",
  "fontSize": 13,
  "hotkey": "Ctrl+Space",
  "backgroundAgent": true,
  "autoCloseBrackets": true,
  "markdownPreviewByDefault": true,
  "languageOverrides": {},
  "externalFormatters": {}
}
```

Notable settings:

- **`hotkey`** — the global shortcut that opens the Explorer selection, for example `Ctrl+Space`,
  `Ctrl+Alt+E`, or `Win+Apostrophe`. If another app already owns your chosen chord, the tray icon
  says the hotkey is unavailable; pick a different one and restart Jot.
- **`backgroundAgent`** — keep Jot warm for instant opens. Set it to `false` to have Jot exit fully
  whenever you close the window.
- **`markdownPreviewByDefault`** — open the preview automatically for Markdown files and keep it closed
  for everything else. Set it to `false` to leave the preview closed until you toggle it yourself.
- **`languageOverrides`** — per-language indentation, for example:
  `"languageOverrides": { "go": { "insertSpaces": false } }`.
- **`externalFormatters`** — a command per language that receives the document on standard input and
  writes the formatted result to standard output, for example:
  `"externalFormatters": { "python": "black -", "rust": "rustfmt" }`.

## Formatting

`Ctrl+Shift+F` formats the current document:

- **JSON** and **XML** (including HTML, SVG, and XAML) are reformatted structurally using your indent
  settings. Invalid input is left untouched and the reason is shown in the status bar.
- For any language with an entry in `externalFormatters`, that formatter is used.
- Everything else is given a safe whitespace tidy-up: trailing whitespace trimmed, leading tabs
  converted to spaces (when configured), and a final newline added.

## Markdown preview

Markdown files open with a live, GitHub-styled preview beside the editor (controlled by
`markdownPreviewByDefault`). Toggle it any time with `Ctrl+Shift+V`. The preview updates as you type.
Launch straight into it for any file with `jot notes.md --preview`.

## Error checking

JSON and YAML files are checked as you type. A genuine parse error gets a red underline at the
offending spot and a problem count in the status bar; click it to jump to the error. The check runs
in-process in a few milliseconds and only reports real syntax errors, never style opinions.

## Uninstall

```powershell
irm https://raw.githubusercontent.com/KingsMMA/jot/main/install/uninstall.ps1 | iex
```

Your configuration under `%APPDATA%\Jot` is kept. To remove it as well, download `uninstall.ps1` and
run it with `-RemoveConfig`.

## Build from source

You need the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```powershell
git clone https://github.com/KingsMMA/jot.git
cd jot
dotnet build                                   # build everything
dotnet test                                    # run the tests
dotnet run --project src/Jot -- path/to/file   # run it
.\install\build-release.ps1                    # produce dist\jot-win-x64.zip
.\install\install.ps1 -Source dist\app         # install your local build
```

## Licence

[MIT](LICENSE).

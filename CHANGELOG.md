# Changelog

All notable changes to Jot are recorded here. Versions follow simple incremental
numbering while Jot is pre-1.0.

## [0.1.5] - 2026-06-03

### Fixed
- The global hotkey now only opens Jot while File Explorer is focused. It
  previously fired in every application, so the chord was captured inside games,
  browsers, and other programs; now the keys pass straight through everywhere
  except File Explorer.

## [0.1.4] - 2026-06-03

### Added
- A pop-up settings editor (`Ctrl+,`, or **Settings** from the tray icon) with a
  control for every option and an **Edit raw config file** button for the advanced
  map settings.
- Three image-backdrop themes, `celestial`, `cosmic`, and `winter`, each darkened
  and placed behind a translucent, contrast-checked surface so text stays legible.
- A genuinely frosted, translucent `acrylic` theme that blurs the desktop behind
  the window, plus a matching `acrylic-lilac` variant.

### Changed
- Image-backdrop themes now draw the status bar with a solid gradient, so the file
  name, position, and language readout stay readable over a busy photograph.
- The acrylic material is tinted from each theme's background colour, so every
  acrylic theme keeps its own character.

### Fixed
- The `aurora` backdrop now shows through correctly instead of being hidden by an
  over-opaque surface.
- The `acrylic` theme is now reliably translucent, and switching to it while the
  window is already open takes effect immediately.
- Fixed a startup crash on some graphics back-ends caused by the acrylic backdrop
  having no material.

## [0.1.3] - 2026-06-01

### Added
- Selectable colour themes: `dark`, `light`, `rose`, `lilac`, soft blurred
  backdrops, and a frosted `acrylic` window, all contrast-checked so text stays
  clearly legible. (#3)

## [0.1.2] - 2026-06-01

### Added
- JSON and YAML error checking as you type, with a problem count in the status bar
  that jumps to the offending spot when clicked. (#1)
- The Markdown preview now opens by default for Markdown files. (#2)

## [0.1.1] - 2026-06-01

### Fixed
- The tray **Exit** command is no longer blocked by the close-to-hide handler.

## [0.1.0] - 2026-06-01

### Added
- First release of Jot, a lightning-fast single-file text and code editor for
  Windows 11: instant opens via a warm background agent, syntax highlighting and
  language detection for all major languages, format on a key (JSON, XML,
  whitespace, and external formatters), a GitHub-style Markdown preview, smart
  brackets, code folding, find and replace with regular expressions, a global
  `Ctrl+Space` hotkey, and an **Edit with Jot** context-menu entry.

[0.1.5]: https://github.com/KingsMMA/jot/compare/v0.1.4...v0.1.5
[0.1.4]: https://github.com/KingsMMA/jot/compare/v0.1.3...v0.1.4
[0.1.3]: https://github.com/KingsMMA/jot/compare/v0.1.2...v0.1.3
[0.1.2]: https://github.com/KingsMMA/jot/compare/v0.1.1...v0.1.2
[0.1.1]: https://github.com/KingsMMA/jot/compare/v0.1.0...v0.1.1
[0.1.0]: https://github.com/KingsMMA/jot/releases/tag/v0.1.0

# Changelog

All notable changes to PC Cleanup are documented here.

## [1.2.0] - 2026-11-14

### Changed

- Redesign the whole interface: sidebar with an animated drive gauge, settings and actions; collapsible category cards with per-target size meters; animated activity panel.
- Replace default WinForms controls with custom-painted, DPI-aware ones (buttons, toggle switches, checkboxes, stepper, progress bar with shimmer).
- Dark title bar and dark scrollbars on Windows 10/11, dark tooltips and mouse-wheel scrolling anywhere over the target list.
- Show categories in their natural order (Temporary Files first, Windows System last).
- Build now compiles every file under `src` (`/recurse`).

### Added

- Recommended / Select all / Clear selection shortcuts.
- Drive gauge shows how much of the used space the current selection can recover.
- **Open activity log** is now enabled and opens the logs folder when no log exists yet.

## [1.1.0] - 2026-11-11

### Added

- Add a screenshot of the application to the README.
- Place the **Restart as Admin** action directly in the Windows System (Administrator) section.

### Changed

- Make the Start button clearly distinguish **Start Preview** from **Start Cleanup**.
- Report preview completion separately and explicitly confirm that preview mode deletes no files.
- Refresh the README with clearer sections, visual markers, and an application screenshot.

## [1.0.0] - 2026-11-11

### Added

- Initial Windows desktop application release.
- Selectable cleanup targets grouped by category.
- Target scanning, dry-run preview, progress reporting, and timestamped logs.
- Safe handling for locked files and filesystem reparse points.
- Optional cleanup targets and guarded administrator-only Windows operations.
- Portable executable build and multi-resolution application icon.

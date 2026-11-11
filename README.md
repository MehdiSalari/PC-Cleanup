# PC Cleanup

PC Cleanup is a lightweight Windows desktop utility for reviewing and removing regenerable temporary files and caches. It provides a graphical checklist, a dry-run mode, live progress reporting, and an operation log.

## Features

- Select cleanup targets individually with checkboxes.
- Scan selected locations and estimate recoverable space before cleanup.
- Preview a cleanup without deleting anything (Dry Run).
- Show a progress bar, current operation, and timestamped log while work runs.
- Skip locked files and avoid following filesystem reparse points.
- Keep browser profiles, cookies, sign-ins, settings, personal documents, and project files out of the normal cache-cleanup targets.
- Offer optional, clearly separated cleanup for Downloads installers, Maven/Gradle caches, the Recycle Bin, and administrator-only Windows locations.
- Protect Windows Update downloads when update activity is detected or the system drive has less than 20 GB free.

## Cleanup categories

| Category | Examples |
| --- | --- |
| Temporary files | User temporary files older than the selected age |
| Developer caches | uv, pip, npm/npx, Pub, Dart, Yarn, pnpm, bun, Maven and Gradle |
| Application caches | Electron updater caches, application cache folders, Chromium-based browser caches |
| System caches | Crash dumps, Windows Error Reporting, thumbnails, icon and GPU shader caches, web and Direct3D caches |
| Optional cleanup | Recycle Bin and old EXE/ZIP installers in Downloads |
| Windows (Administrator) | Windows Update download cache, Windows Temp, Delivery Optimization, DISM component cleanup and Windows.old |

Some optional or administrator-only operations are unchecked or unavailable by default. Review each selected target before starting a live cleanup.

## Safety notes

- Use **Dry Run** first to inspect the proposed cleanup. It does not delete files.
- A live cleanup permanently deletes the selected cache/temp contents. Review the checklist and confirmation dialog before proceeding.
- Close applications before clearing their caches. Files that are locked or inaccessible may be skipped.
- The Windows Update cache is protected when update activity or a pending restart is detected. The low-disk protection can also skip Windows-level cleanup.
- **Force Windows Update** overrides the update-activity protection. Use it only when you understand the consequences.
- **ResetBase** is an advanced DISM option. It permanently removes the ability to uninstall currently installed Windows updates; it is off by default.
- Administrator rights are required for some Windows-level targets. The application requests elevation only when the user chooses the administrator action.
- Cleanup estimates are approximate. Actual reclaimed disk space can differ because of locked files, filesystem accounting, and concurrent system activity.

PC Cleanup is provided as-is. Keep backups of important data and do not select a target unless you understand what it contains.

## Download

Download the latest `PC-Cleanup.exe` from the [GitHub Releases](https://github.com/MehdiSalari/PC-Cleanup/releases) page. The executable is a portable Windows application; no installer is required.

## Requirements

- Windows 10 or Windows 11.
- .NET Framework 4.x runtime (included with supported Windows installations).
- Administrator privileges only for the Windows-level operations that require them.

## Build from source

The project uses the C# compiler (`csc.exe`) included with .NET Framework 4.x and Windows PowerShell 5.1.

1. Clone or download this repository.
2. Open Command Prompt in the repository directory.
3. Run:

   ```bat
   build.bat
   ```

The script regenerates `assets/app-icon.ico` from the source artwork and compiles `build/PC-Cleanup.exe`. Build output is intentionally excluded from Git; release binaries are attached to GitHub Releases.

## Project structure

```text
PC-Cleanup/
├── assets/
│   ├── app-icon-source.png   # Original icon artwork
│   └── app-icon.ico          # Multi-resolution Windows icon
├── build/
│   └── app.manifest          # Windows application manifest
├── scripts/
│   ├── Generate-Icon.ps1     # Regenerates the ICO file
│   └── Invoke-PC-Cleanup.ps1 # Standalone PowerShell cleanup script
├── src/
│   ├── Controls/             # Reusable WinForms controls
│   ├── Models/               # Cleanup operation and result models
│   ├── Properties/           # Assembly metadata and version
│   ├── Services/             # Filesystem and Recycle Bin services
│   ├── UI/                   # Main window and background workers
│   └── Program.cs            # Application entry point
├── .gitignore
├── CHANGELOG.md
├── README.md
├── VERSION
└── build.bat
```

## Versioning

This project follows [Semantic Versioning](https://semver.org/). The current version is recorded in `VERSION`; release tags use the `vMAJOR.MINOR.PATCH` format.

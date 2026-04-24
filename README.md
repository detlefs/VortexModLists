# VortexModLists

VortexModLists is an app for Windows that reads mod data from a local Nexus Mods Vortex installation and displays it in a clear table per game.

The main purpose is to provide a convenient way to export your Vortex mod lists for backup, sharing or documentation.

## What it does

- Detects and loads Vortex mod data from JSON sources (preferred: backup `hourly.json`).
- Lists managed games and lets you switch between them.
- Shows mod details in a table with these columns:
  - `modName`
  - `ID`
  - `Version`
  - `Homepage` (clickable `Link`)
  - `Status` (Active / Inactive)
- Supports status filtering directly in the **Status** column header.
- Applies the current filter to table view **and** exports.

## Vortex data files used by this app

VortexModLists reads Vortex metadata from JSON files that contain the `persistent.mods` structure.

### Preferred source (best choice)

1. **`%AppData%\Vortex\temp\state_backups_full\hourly.json`**
1. **`%AppData%\Vortex\temp\state_backups_full\startup.json`**

These are treated as the primary source because they are typically the most recent full backup JSON and directly readable.

On startup, the app tries to identify the most recently changed file.

### Other supported sources

If the preferred file is not available, the app can also read:

- other `*.json` files in the selected folder (including subfolders)
- `state.json`
- `vortex-state.json`

The app validates candidates and uses the first file that actually contains a valid Vortex mods structure.

## How to find the files

### Typical location

Open this folder in Windows Explorer:

`%AppData%\Vortex\temp\state_backups_full`

(Example expanded path: `C:\Users\<YourUser>\AppData\Roaming\Vortex\temp\state_backups_full`)

### In the app

- Use **Browse...** and select either:
  - `hourly.json`, or
  - a folder (the app searches JSON files recursively).
- Use **Reload** to re-scan and reload.

After loading, the path box shows the resolved file that was actually used.

## File suitability recommendation

Use this priority order:

1. `hourly.json` in `%AppData%\Vortex\temp\state_backups_full` (**recommended**)  
2. latest JSON backup in the same backup folder  
3. other JSON snapshots only when no recent backup JSON is available

If multiple files exist, prefer the newest backup JSON to avoid stale data.

## Export features

You can export either:
- the selected game, or
- all games

Supported formats:
- CSV
- Markdown
- Excel (`.xlsx`)

Additional actions:
- Copy CSV to clipboard
- Copy Markdown to clipboard

## User experience

- Automatic Light/Dark theme support based on Windows settings.
- English default language.
- German localization included.
- Localization structure prepared for adding more languages.
- Remembers window position/size and custom table column widths between sessions.

## Requirements

- Windows 10 or newer in 64 bit
- Nexus Mods Vortex installed with local state data available

## Notes

The app reads local Vortex metadata files only. It does not modify your Vortex setup or deployed mods.

## Disclaimer

* I craeted this tool myinly via vibe coding with an AI. It is provided as is without any warranty.
* I tested it in my own environment and on a Windows 10 test VM with current Vortex (1.16.9) installed.
* I **did not** test it with older versions of Vortex or on other platforms (e.g. Linux with Wine. I assuem it will not work)
* If you have ideas for the tool or find bugs, please post on GitHub in Issues or Discussions. I will try to respond when I can (no guarantee).
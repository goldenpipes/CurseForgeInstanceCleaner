# CurseForge Instance Cleaner

The CurseForge app sometimes finds game folders that aren't real installs
(private-server copies, launcher backups, old test folders) and lists them as
"Retail 2", "Retail 3" and so on. The app can only disable these, not remove
them. This tool removes them from the list.

## How to use

1. Download the files (**Code → Download ZIP**) and unzip them.
2. Quit CurseForge completely, including the tray icon.
3. Double-click **`CurseForge Instance Cleaner.bat`**.
4. Tick the instances you want gone and click **Remove ticked**.
5. Start CurseForge again.

Windows only. It uses the Windows PowerShell that comes with Windows, so there's nothing to install.

## What it changes

- Only CurseForge's list of instances:
  `%APPDATA%\CurseForge\agent\GameInstances\AddonGameInstance.json`
- Before every change it saves a backup next to that file
  (`AddonGameInstance.json.backup-<date>-<time>`). **Restore a backup...** puts one back.
- It refuses to change anything while CurseForge is running, since the app would write its old list back.
- Entries you keep are left exactly as they were.
- No game or addon folders on your drive are touched. CurseForge just stops
  tracking the addons in an instance you remove.

## Good to know

- Folders that still exist may come back if CurseForge scans for games again.
  Deleting or moving the folder stops that. If one comes back, remove it again
  here, or turn off **Manage addons for this instance** in CurseForge's settings.
- Rows in red are instances whose folder no longer exists.
- Games other than World of Warcraft show as "Game &lt;id&gt;".

## Command line

```
CurseForgeInstanceCleaner.ps1 -List
CurseForgeInstanceCleaner.ps1 -Remove 2,5
```

Numbers come from `-List`. Add `-Path <file>` to work on another copy of the file.

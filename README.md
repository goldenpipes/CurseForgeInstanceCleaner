# CurseForge Instance Cleaner

The CurseForge app sometimes finds game folders that aren't real installs
(private-server copies, launcher backups, old test folders) and lists them as
"Retail 2", "Retail 3" and so on. The app can only disable these, not remove
them. This tool removes them from the list.

## How to use

1. Download **CurseForgeInstanceCleaner.exe** from the
   [latest release](https://github.com/goldenpipes/CurseForgeInstanceCleaner/releases/latest).
2. Run it. Windows may say "Windows protected your PC" because the exe isn't
   signed: click **More info → Run anyway**.
3. Tick the instances you want gone and click **Remove ticked**.
4. If CurseForge is open, it asks before closing it, and offers to start it
   again afterwards.

Windows only. It needs .NET Framework 4, which comes with Windows 10 and 11.

## What it changes

- Only CurseForge's list of instances:
  `%APPDATA%\CurseForge\agent\GameInstances\AddonGameInstance.json`
- Before every change it saves a backup next to that file
  (`AddonGameInstance.json.backup-<date>-<time>`). **Restore a backup...** puts one back.
- CurseForge has to be closed while its list is changed (it would write its old
  list back), so the tool closes it, with your OK, and reads the list again
  after that.
- Entries you keep are left exactly as they were. After writing, the tool reads
  the file back and puts the original back if anything doesn't check out.
- No game or addon folders on your drive are touched. CurseForge just stops
  tracking the addons in an instance you remove.

## Good to know

- Folders that still exist may come back if CurseForge scans for games again.
  Deleting or moving the folder stops that. If one comes back, remove it again
  here, or turn off **Manage addons for this instance** in CurseForge's settings.
- Rows in red are instances whose folder no longer exists.
- Games other than World of Warcraft show as "Game &lt;id&gt;".
- To work on another copy of the file, use **Use another file...** or pass its
  path: `CurseForgeInstanceCleaner.exe <file>`.

## Building

Run `build.bat`. It uses the C# compiler that comes with Windows
(`%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe`); nothing else to install.

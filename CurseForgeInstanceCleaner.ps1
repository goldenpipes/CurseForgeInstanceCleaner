# CurseForge Instance Cleaner
# Removes unwanted game instances (wrongly scanned game folders) from the
# CurseForge app's list. The app only lets you disable them, not remove them.
#
# The list lives in %APPDATA%\CurseForge\agent\GameInstances\AddonGameInstance.json
# (a JSON array, one object per instance). This tool cuts whole entries out of
# that array as text, so everything it keeps stays byte-for-byte the same, and
# it always makes a timestamped backup next to the file first.
#
# Usage:
#   CurseForgeInstanceCleaner.ps1                 opens the window
#   CurseForgeInstanceCleaner.ps1 -List           prints the instances
#   CurseForgeInstanceCleaner.ps1 -Remove 2,5     removes entries by number (from -List)
#   -Path <file>                                  use another instance file

param(
    [string]$Path = (Join-Path $env:APPDATA 'CurseForge\agent\GameInstances\AddonGameInstance.json'),
    [switch]$List,
    [int[]]$Remove
)

$ErrorActionPreference = 'Stop'

# Splits a JSON array into its top-level elements' raw text.
Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
public static class CfJsonArray {
    public static string[] Split(string s) {
        var list = new List<string>();
        int i = 0, n = s.Length;
        while (i < n && char.IsWhiteSpace(s[i])) i++;
        if (i >= n || s[i] != '[') throw new FormatException("The file is not a JSON list.");
        i++;
        int depth = 0, start = -1;
        bool str = false, esc = false;
        for (; i < n; i++) {
            char c = s[i];
            if (str) {
                if (esc) esc = false;
                else if (c == '\\') esc = true;
                else if (c == '"') str = false;
                continue;
            }
            if (char.IsWhiteSpace(c)) continue;
            if (start < 0) start = i;
            if (c == '"') { str = true; }
            else if (c == '{' || c == '[') { depth++; }
            else if (c == '}' || c == ']') {
                if (depth == 0) {
                    if (c != ']') throw new FormatException("Unexpected } in the file.");
                    if (start < i) list.Add(s.Substring(start, i - start).Trim());
                    return list.ToArray();
                }
                depth--;
            }
            else if (c == ',' && depth == 0) {
                list.Add(s.Substring(start, i - start).Trim());
                start = -1;
            }
        }
        throw new FormatException("The file ends in the middle of the list.");
    }
}
'@

$GAMES = @{ 1 = 'World of Warcraft' }

$LIVE_DIR = Join-Path $env:APPDATA 'CurseForge'

# CurseForge rewrites its own files while running; copies elsewhere are safe to edit.
function Test-CurseForgeRunning([string]$file) {
    if ($file -and -not ([IO.Path]::GetFullPath($file)).StartsWith($LIVE_DIR, [StringComparison]::OrdinalIgnoreCase)) { return $false }
    @(Get-Process | Where-Object { $_.ProcessName -match 'curseforge' }).Count -gt 0
}

function Read-Instances([string]$file) {
    if (-not (Test-Path -LiteralPath $file)) { throw "Instance file not found:`n$file" }
    $bytes = [IO.File]::ReadAllBytes($file)
    $bom = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
    $text = (New-Object Text.UTF8Encoding $false).GetString($bytes)
    if ($bom) { $text = $text.Substring(1) }
    $parts = [CfJsonArray]::Split($text)
    # Windows PowerShell 5.1 hands the whole array on as one object: unroll it.
    $objs = @(ConvertFrom-Json $text | ForEach-Object { $_ })
    if ($objs.Count -ne $parts.Count) { throw "Could not read the file safely (found $($parts.Count) entries but parsed $($objs.Count))." }
    $rows = for ($i = 0; $i -lt $objs.Count; $i++) {
        $o = $objs[$i]
        $game = $GAMES[[int]$o.gameTypeID]
        if (-not $game) { $game = "Game $($o.gameTypeID)" }
        [pscustomobject]@{
            Number  = $i + 1
            Name    = [string]$o.name
            Game    = $game
            Folder  = [string]$o.installPath
            Exists  = [bool]($o.installPath -and (Test-Path -LiteralPath $o.installPath))
            Addons  = @($o.installedAddons).Count
            Enabled = [bool]$o.isEnabled
            Raw     = $parts[$i]
        }
    }
    [pscustomobject]@{ File = $file; Bom = $bom; Text = $text; Rows = @($rows) }
}

function Write-Text([string]$file, [string]$text, [bool]$bom) {
    [IO.File]::WriteAllText($file, $text, (New-Object Text.UTF8Encoding $bom))
}

function Backup-File([string]$file) {
    $dest = "$file.backup-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
    Copy-Item -LiteralPath $file -Destination $dest
    $dest
}

# Removes the rows whose Number is in $numbers; returns the backup's path.
function Remove-Instances($data, [int[]]$numbers) {
    if (Test-CurseForgeRunning $data.File) { throw 'CurseForge is running. Quit it completely (including the tray icon) first, or it will write its old list back.' }
    $keep = @($data.Rows | Where-Object { $numbers -notcontains $_.Number })
    $backup = Backup-File $data.File
    Write-Text $data.File ('[' + ($keep.Raw -join ',') + ']') $data.Bom
    $check = Read-Instances $data.File
    if ($check.Rows.Count -ne $keep.Count) {
        Copy-Item -LiteralPath $backup -Destination $data.File -Force
        throw 'The result did not check out, so the original was put back.'
    }
    $backup
}

# ---------- command line ----------
if ($List -or $Remove) {
    $data = Read-Instances $Path
    if ($Remove) {
        $bad = @($Remove | Where-Object { $_ -lt 1 -or $_ -gt $data.Rows.Count })
        if ($bad) { throw "No entry number $($bad -join ', ')." }
        $names = ($data.Rows | Where-Object { $Remove -contains $_.Number }).Name -join ', '
        $backup = Remove-Instances $data $Remove
        "Removed: $names"
        "Backup:  $backup"
        $data = Read-Instances $Path
    }
    $data.Rows | Format-Table Number, Name, Game, Addons, Enabled, Exists, Folder -AutoSize | Out-String -Width 250
    return
}

# ---------- window ----------
Add-Type -AssemblyName System.Windows.Forms, System.Drawing
[Windows.Forms.Application]::EnableVisualStyles()

$form = New-Object Windows.Forms.Form
$form.Text = 'CurseForge Instance Cleaner'
$form.Size = New-Object Drawing.Size(980, 520)
$form.MinimumSize = New-Object Drawing.Size(700, 360)
$form.StartPosition = 'CenterScreen'

$intro = New-Object Windows.Forms.Label
$intro.Dock = 'Top'
$intro.Height = 52
$intro.Padding = New-Object Windows.Forms.Padding(8, 8, 8, 0)
$intro.Text = "Tick the game instances to take off CurseForge's list, then click Remove. Quit CurseForge first.`n" +
    "Game and addon folders on your drive are not touched. A backup is made each time."

$grid = New-Object Windows.Forms.DataGridView
$grid.Dock = 'Fill'
$grid.AllowUserToAddRows = $false
$grid.AllowUserToDeleteRows = $false
$grid.AllowUserToResizeRows = $false
$grid.RowHeadersVisible = $false
$grid.SelectionMode = 'FullRowSelect'
$grid.AutoSizeColumnsMode = 'Fill'
$grid.BackgroundColor = [Drawing.SystemColors]::Window

$colCheck = New-Object Windows.Forms.DataGridViewCheckBoxColumn
$colCheck.HeaderText = 'Remove'
$colCheck.FillWeight = 12
[void]$grid.Columns.Add($colCheck)
foreach ($c in @(@('Name', 28), @('Game', 22), @('Addons', 10), @('Managed', 11), @('Folder found', 12), @('Folder', 80))) {
    $col = New-Object Windows.Forms.DataGridViewTextBoxColumn
    $col.HeaderText = $c[0]
    $col.FillWeight = $c[1]
    $col.ReadOnly = $true
    [void]$grid.Columns.Add($col)
}

$buttons = New-Object Windows.Forms.FlowLayoutPanel
$buttons.Dock = 'Bottom'
$buttons.Height = 44
$buttons.Padding = New-Object Windows.Forms.Padding(6)

function New-Button([string]$text) {
    $b = New-Object Windows.Forms.Button
    $b.Text = $text
    $b.AutoSize = $true
    $b.Height = 28
    [void]$buttons.Controls.Add($b)
    $b
}
$btnRemove = New-Button 'Remove ticked'
$btnRefresh = New-Button 'Refresh'
$btnRestore = New-Button 'Restore a backup...'
$btnOpen = New-Button 'Open file location'
$btnBrowse = New-Button 'Use another file...'

$status = New-Object Windows.Forms.StatusStrip
$statusText = New-Object Windows.Forms.ToolStripStatusLabel
$statusText.Spring = $true
$statusText.TextAlign = 'MiddleLeft'
[void]$status.Items.Add($statusText)

$form.Controls.Add($grid)
$form.Controls.Add($intro)
$form.Controls.Add($buttons)
$form.Controls.Add($status)

$script:data = $null

function Show-Error([string]$msg) {
    [void][Windows.Forms.MessageBox]::Show($form, $msg, 'CurseForge Instance Cleaner', 'OK', 'Warning')
}

function Load-Grid {
    $grid.Rows.Clear()
    $script:data = $null
    try {
        $script:data = Read-Instances $Path
    } catch {
        $statusText.Text = $_.Exception.Message -replace "`n", ' '
        return
    }
    foreach ($r in $script:data.Rows) {
        $yesNo = @{ $true = 'Yes'; $false = 'No' }
        $i = $grid.Rows.Add($false, $r.Name, $r.Game, $r.Addons, $yesNo[$r.Enabled], $yesNo[$r.Exists], $r.Folder)
        $grid.Rows[$i].Tag = $r.Number
        if (-not $r.Exists) { $grid.Rows[$i].DefaultCellStyle.ForeColor = [Drawing.Color]::Firebrick }
    }
    $msg = "$($script:data.Rows.Count) instances in $Path"
    if (Test-CurseForgeRunning $Path) { $msg ='CurseForge is running: quit it before removing anything.   ' + $msg }
    $statusText.Text = $msg
}

$grid.add_CurrentCellDirtyStateChanged({
    if ($grid.IsCurrentCellDirty) { $grid.CommitEdit('CurrentCellChange') }
})

$btnRefresh.add_Click({ Load-Grid })

$btnRemove.add_Click({
    if (-not $script:data) { Show-Error 'No instance file loaded.'; return }
    $ticked = @(foreach ($row in $grid.Rows) { if ($row.Cells[0].Value -eq $true) { $row } })
    if ($ticked.Count -eq 0) { Show-Error 'Tick at least one instance first.'; return }
    if (Test-CurseForgeRunning $Path) {
        Show-Error 'CurseForge is running. Quit it completely (including the tray icon), then try again.'
        return
    }
    $names = ($ticked | ForEach-Object { '  - ' + $_.Cells[1].Value + '  (' + $_.Cells[6].Value + ')' }) -join "`n"
    $tracked = 0
    foreach ($row in $ticked) { $tracked += [int]$row.Cells[3].Value }
    $warn = ''
    if ($tracked -gt 0) { $warn = "`n`nCurseForge will stop tracking $tracked addon(s) in these. The addon files stay on your drive." }
    $answer = [Windows.Forms.MessageBox]::Show($form, "Remove these from CurseForge's list?`n`n$names$warn", 'Confirm', 'YesNo', 'Question')
    if ($answer -ne 'Yes') { return }
    try {
        $backup = Remove-Instances $script:data ([int[]]@($ticked | ForEach-Object { $_.Tag }))
        Load-Grid
        $statusText.Text = "Removed $($ticked.Count). Backup: $backup"
    } catch {
        Show-Error $_.Exception.Message
        Load-Grid
    }
})

$btnRestore.add_Click({
    if (Test-CurseForgeRunning $Path) { Show-Error 'Quit CurseForge before restoring a backup.'; return }
    $dlg = New-Object Windows.Forms.OpenFileDialog
    $dlg.InitialDirectory = Split-Path $Path
    $dlg.Filter = 'Backups|' + (Split-Path $Path -Leaf) + '.backup-*|All files|*.*'
    if ($dlg.ShowDialog($form) -ne 'OK') { return }
    try {
        [void](Read-Instances $dlg.FileName)
        if (Test-Path -LiteralPath $Path) { [void](Backup-File $Path) }
        Copy-Item -LiteralPath $dlg.FileName -Destination $Path -Force
        Load-Grid
        $statusText.Text = 'Restored ' + (Split-Path $dlg.FileName -Leaf)
    } catch {
        Show-Error $_.Exception.Message
    }
})

$btnOpen.add_Click({
    $dir = Split-Path $Path
    if (Test-Path -LiteralPath $dir) { Start-Process explorer.exe "`"$dir`"" } else { Show-Error "Folder not found:`n$dir" }
})

$btnBrowse.add_Click({
    $dlg = New-Object Windows.Forms.OpenFileDialog
    $dlg.Filter = 'CurseForge instance file|*GameInstance*.json|JSON|*.json'
    if (Test-Path -LiteralPath (Split-Path $Path)) { $dlg.InitialDirectory = Split-Path $Path }
    if ($dlg.ShowDialog($form) -eq 'OK') { $script:Path = $dlg.FileName; Load-Grid }
})

$form.add_Shown({ Load-Grid })
[void]$form.ShowDialog()

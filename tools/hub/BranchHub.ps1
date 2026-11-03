# Branch-Hub: zeigt alle Branches und startet jeden per Klick zum Testen.
# Der Commit-Stand läuft in der Prüfkopie work\hub, damit weder main noch die Arbeitsordner der Sessions verändert werden.
# Git liefert UTF-8; ohne das werden Umlaute in Commit-Texten falsch angezeigt.
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
Add-Type -AssemblyName System.Windows.Forms, System.Drawing
[System.Windows.Forms.Application]::EnableVisualStyles()

$Repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$Runner = Join-Path $PSScriptRoot 'RunBranch.ps1'
$HubCopy = (Join-Path $Repo 'work\hub').Replace('\', '/')

# Ordnernamen der Arbeitskopien, die die Untersessions angelegt haben.
$SessionNames = @{
    'story' = 'Story'; 'grafik' = 'Grafik'; 'atmosphaere' = 'Atmosphäre'; 'menues' = 'Menüführung'
    'sequenzen' = 'Zwischensequenzen'; 'gegner' = 'Gegner'; 'level-mechanik' = 'Level und Mechanik'
    'ton-musik' = 'Ton und Musik'; 'kampagne' = 'Kampagne'; 'spieltest' = 'Spieltest'
}

function Invoke-Git { & git.exe -C $Repo @args 2>$null }

function Get-Worktrees {
    $map = @{}
    $path = $null
    foreach ($line in (Invoke-Git worktree list --porcelain)) {
        if ($line -like 'worktree *') { $path = $line.Substring(9) }
        elseif ($line -like 'branch refs/heads/*') { $map[$line.Substring(18)] = $path }
    }
    $map
}

function Get-SessionName([string]$folder) {
    if (-not $folder) { return '' }
    if ($folder.Replace('\', '/') -eq $Repo.Replace('\', '/')) { return 'Hauptordner' }
    $leaf = Split-Path $folder -Leaf
    foreach ($key in $SessionNames.Keys) { if ($leaf -like "$key-*") { return "Session $($SessionNames[$key])" } }
    $leaf
}

function Get-Branches {
    $worktrees = Get-Worktrees
    $local = @(Invoke-Git for-each-ref --format='%(refname:short)' refs/heads)
    $rows = @()
    $refs = @($local | ForEach-Object { $_ }) + @(Invoke-Git for-each-ref --format='%(refname:short)' refs/remotes/origin |
        Where-Object { $_ -ne 'origin/HEAD' -and $_ -ne 'origin' -and ($local -notcontains $_.Substring(7)) })
    foreach ($ref in $refs) {
        $info = Invoke-Git log -1 --date=format:'%d.%m. %H:%M' --format='%ct|%cd|%s' $ref
        if (-not $info) { continue }
        $parts = $info -split '\|', 3
        $counts = (Invoke-Git rev-list --left-right --count "main...$ref") -split '\s+'
        $folder = $worktrees[$ref]
        $dirty = 0
        if ($folder) { $dirty = @(& git.exe -C $folder status --porcelain 2>$null | Where-Object { $_ -notmatch '\.cs\.uid$' }).Count }
        $rows += [pscustomobject]@{
            Ref = $ref; Time = [long]$parts[0]; Age = $parts[1]; Subject = $parts[2]
            Behind = [int]$counts[0]; Ahead = [int]$counts[1]; Folder = $folder; Dirty = $dirty
        }
    }
    $rows | Sort-Object Time -Descending
}

$form = New-Object System.Windows.Forms.Form
$form.Text = 'Messingstadt Branch-Hub'
$form.Size = New-Object System.Drawing.Size(1180, 640)
$form.StartPosition = 'CenterScreen'
$form.BackColor = [System.Drawing.Color]::FromArgb(36, 32, 28)
$form.ForeColor = [System.Drawing.Color]::FromArgb(226, 214, 190)
$form.Font = New-Object System.Drawing.Font('Segoe UI', 9.5)

$list = New-Object System.Windows.Forms.ListView
$list.View = 'Details'
$list.FullRowSelect = $true
$list.MultiSelect = $false
$list.HideSelection = $false
$list.Dock = 'Fill'
$list.BackColor = [System.Drawing.Color]::FromArgb(48, 43, 37)
$list.ForeColor = $form.ForeColor
foreach ($col in @(('Branch', 230), ('Ort', 170), ('Zuletzt', 110), ('Neu ggü. main', 100), ('Fehlt aus main', 105), ('Offen', 60), ('Letzter Commit', 380))) {
    [void]$list.Columns.Add($col[0], $col[1])
}

$panel = New-Object System.Windows.Forms.FlowLayoutPanel
$panel.Dock = 'Bottom'
$panel.Height = 92
$panel.Padding = New-Object System.Windows.Forms.Padding(8)

$status = New-Object System.Windows.Forms.Label
$status.Dock = 'Bottom'
$status.Height = 44
$status.Padding = New-Object System.Windows.Forms.Padding(10, 4, 10, 4)
$status.Text = 'Doppelklick spielt den gewählten Branch.'

$keepSave = New-Object System.Windows.Forms.CheckBox
$keepSave.Text = 'Spielstand behalten (sonst nach dem Test zurückspielen)'
$keepSave.AutoSize = $true
$keepSave.Margin = New-Object System.Windows.Forms.Padding(12, 10, 4, 4)

function New-Button([string]$text, [scriptblock]$onClick) {
    $b = New-Object System.Windows.Forms.Button
    $b.Text = $text
    $b.AutoSize = $true
    $b.Height = 32
    $b.Padding = New-Object System.Windows.Forms.Padding(6, 2, 6, 2)
    $b.FlatStyle = 'Flat'
    $b.BackColor = [System.Drawing.Color]::FromArgb(92, 66, 40)
    $b.Add_Click($onClick)
    [void]$panel.Controls.Add($b)
    $b
}

function Selected {
    if ($list.SelectedItems.Count -eq 0) {
        [void][System.Windows.Forms.MessageBox]::Show('Erst einen Branch auswählen.', 'Branch-Hub')
        return $null
    }
    $list.SelectedItems[0].Tag
}

function Start-Runner([string[]]$arguments, [string]$what) {
    $all = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$Runner`"") + $arguments
    if ($keepSave.Checked) { $all += '-KeepSave' }
    Start-Process powershell.exe -ArgumentList $all -WorkingDirectory $Repo
    $status.Text = "Gestartet: $what. Fortschritt und Fehler stehen im neuen Konsolenfenster."
}

function Run-Commit([string]$mode) {
    $row = Selected
    if ($row) { Start-Runner @('-Ref', "`"$($row.Ref)`"", '-Mode', $mode) "$mode für $($row.Ref)" }
}

function Refresh-List {
    $form.Cursor = 'WaitCursor'
    $list.BeginUpdate()
    $list.Items.Clear()
    foreach ($row in Get-Branches) {
        $item = New-Object System.Windows.Forms.ListViewItem($row.Ref)
        [void]$item.SubItems.Add((Get-SessionName $row.Folder))
        [void]$item.SubItems.Add($row.Age)
        [void]$item.SubItems.Add($(if ($row.Ahead) { "$($row.Ahead) Commits" } else { 'nichts' }))
        [void]$item.SubItems.Add($(if ($row.Behind) { "$($row.Behind)" } else { 'aktuell' }))
        [void]$item.SubItems.Add($(if ($row.Dirty) { "$($row.Dirty)" } else { '' }))
        [void]$item.SubItems.Add($row.Subject)
        $item.Tag = $row
        if ($row.Ahead -gt 0) { $item.ForeColor = [System.Drawing.Color]::FromArgb(240, 196, 110) }
        elseif ($row.Dirty -gt 0) { $item.ForeColor = [System.Drawing.Color]::FromArgb(200, 170, 230) }
        else { $item.ForeColor = [System.Drawing.Color]::FromArgb(150, 140, 125) }
        [void]$list.Items.Add($item)
    }
    $list.EndUpdate()
    $form.Cursor = 'Default'
    $status.Text = "Gelb: hat Commits, die nicht in main sind. Lila: nur nicht committete Änderungen in der Session. Grau: nichts Neues. Stand $(Get-Date -Format 'HH:mm')."
}

[void](New-Button 'Spielen' { Run-Commit 'play' })
[void](New-Button 'Arbeitsstand spielen' {
        $row = Selected
        if (-not $row) { return }
        if (-not $row.Folder -or $row.Folder.Replace('\', '/') -eq $HubCopy) {
            [void][System.Windows.Forms.MessageBox]::Show('Dieser Branch hat keinen eigenen Arbeitsordner. Nimm „Spielen“.', 'Branch-Hub')
            return
        }
        Start-Runner @('-Folder', "`"$($row.Folder)`"", '-Mode', 'play') "Arbeitsstand $(Get-SessionName $row.Folder)"
    })
[void](New-Button 'Tests' { Run-Commit 'tests' })
[void](New-Button 'Im Godot-Editor' { Run-Commit 'editor' })
[void](New-Button 'Commits zeigen' {
        $row = Selected
        if (-not $row) { return }
        $log = (Invoke-Git log --format='%h  %cr  %s' -n 25 "main..$($row.Ref)") -join "`r`n"
        if (-not $log) { $log = 'Keine Commits, die nicht schon in main sind.' }
        [void][System.Windows.Forms.MessageBox]::Show($log, "Neu in $($row.Ref)")
    })
[void](New-Button 'Ordner öffnen' {
        $row = Selected
        if ($row -and $row.Folder) { Start-Process explorer.exe $row.Folder }
    })
[void](New-Button 'Aktualisieren' { Refresh-List })
[void](New-Button 'Von GitHub holen' {
        $form.Cursor = 'WaitCursor'
        Invoke-Git fetch --prune origin | Out-Null
        Refresh-List
    })
[void]$panel.Controls.Add($keepSave)

$list.Add_DoubleClick({ Run-Commit 'play' })
$form.Controls.Add($list)
$form.Controls.Add($status)
$form.Controls.Add($panel)
$form.Add_Shown({ Refresh-List })
[void]$form.ShowDialog()

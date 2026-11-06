# Startet einen Branch zum Testen. Läuft in einem eigenen Konsolenfenster, das der Branch-Hub öffnet.
#   -Ref <branch>   Commit-Stand in der Prüfkopie work/hub (stört weder main noch die Sessions)
#   -Folder <pfad>  Arbeitsstand einer Session direkt aus ihrem Ordner, mit nicht committeten Änderungen
#   -Mode play | editor | tests
param(
    [string]$Ref,
    [string]$Folder,
    [ValidateSet('play', 'editor', 'tests')][string]$Mode = 'play',
    [switch]$KeepSave
)

# Continue: Godot schreibt Meldungen nach stderr, das darf in PowerShell 5.1 nicht abbrechen.
$ErrorActionPreference = 'Continue'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$Repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$HubCopy = Join-Path $Repo 'work\hub'
$GodotDir = if ($env:GODOT_DIR) { $env:GODOT_DIR } else { 'C:\Users\user\.tools\godot\Godot_v4.7.2-stable_mono_win64' }
$GodotConsole = Join-Path $GodotDir 'Godot_v4.7.2-stable_mono_win64_console.exe'
$UserData = Join-Path $env:APPDATA 'Godot\app_userdata\Messingstadt'
$SaveFiles = 'kampagne.json', 'kampagne_2.json', 'kampagne_3.json', 'einstellungen.json', 'einstieg_gesehen'

function Step([string]$text) { Write-Host ''; Write-Host "== $text" -ForegroundColor Yellow }

function Fail([string]$text) {
    Write-Host ''
    Write-Host "FEHLER: $text" -ForegroundColor Red
    Read-Host 'Enter schließt das Fenster'
    exit 1
}

function Invoke-Checked([string]$what, [scriptblock]$block) {
    & $block
    if ($LASTEXITCODE -ne 0) { Fail "$what ist fehlgeschlagen (Code $LASTEXITCODE)." }
}

function Save-Userdata([string]$backup) {
    New-Item -ItemType Directory -Force $backup | Out-Null
    foreach ($f in $SaveFiles) {
        $p = Join-Path $UserData $f
        if (Test-Path $p) { Copy-Item $p $backup -Force }
    }
}

function Restore-Userdata([string]$backup) {
    foreach ($f in $SaveFiles) {
        $p = Join-Path $UserData $f
        $b = Join-Path $backup $f
        if (Test-Path $b) { Copy-Item $b $p -Force }
        elseif (Test-Path $p) { Remove-Item $p -Force }
    }
    Remove-Item $backup -Recurse -Force
}

if (-not (Test-Path $GodotConsole)) { Fail "Godot nicht gefunden: $GodotConsole (Umgebungsvariable GODOT_DIR setzen)." }

if ($Folder) {
    $Root = $Folder
    $Host.UI.RawUI.WindowTitle = "Hub: Arbeitsstand $(Split-Path $Folder -Leaf)"
    Step "Arbeitsstand aus $Folder (mit nicht committeten Änderungen)"
}
else {
    if (-not $Ref) { Fail 'Weder -Ref noch -Folder angegeben.' }
    $Root = $HubCopy
    $Host.UI.RawUI.WindowTitle = "Hub: $Ref"
    if (-not (Test-Path (Join-Path $HubCopy '.git'))) {
        Step 'Prüfkopie work\hub anlegen'
        Invoke-Checked 'Anlegen der Prüfkopie' { git -C $Repo worktree add --detach $HubCopy $Ref }
    }
    Step "Stand $Ref in die Prüfkopie holen"
    Invoke-Checked 'Auschecken' { git -C $HubCopy checkout --quiet --force --detach $Ref }
    # Untracked Reste vom vorigen Branch weg; ignorierte Dateien wie der Import-Cache in .godot bleiben.
    Invoke-Checked 'Aufräumen' { git -C $HubCopy clean -fdq }
    git -C $HubCopy log -1 --format='   %h %s (%an, %ar)'
}

if ($Mode -eq 'tests') {
    Step 'Tests'
    $tests = Join-Path $Root 'tests\Messingstadt.Core.Tests'
    dotnet test $tests --nologo -v q
    $code = $LASTEXITCODE
    Write-Host ''
    if ($code -eq 0) { Write-Host 'Alle Tests grün.' -ForegroundColor Green } else { Write-Host "Tests mit Fehlern (Code $code)." -ForegroundColor Red }
    Read-Host 'Enter schließt das Fenster'
    exit $code
}

$Game = Join-Path $Root 'game'
Step 'Spiel bauen'
Invoke-Checked 'Build' { dotnet build $Game -c Debug --nologo -v q }

Step 'Grafik importieren (beim ersten Mal dauert das)'
& $GodotConsole --path $Game --headless --import 2>&1 | Where-Object { $_ -match 'ERROR' } | ForEach-Object { Write-Host "   $_" -ForegroundColor DarkYellow }

if ($Mode -eq 'editor') {
    Step 'Godot-Editor öffnen'
    Start-Process (Join-Path $GodotDir 'Godot_v4.7.2-stable_mono_win64.exe') -ArgumentList '--editor', '--path', "`"$Game`""
    exit 0
}

$backup = Join-Path $env:TEMP "messingstadt-hub-save-$PID"
if (-not $KeepSave) {
    Save-Userdata $backup
    Write-Host '   Spielstand und Einstellungen gesichert, nach dem Test kommen sie zurück.'
}

Step 'Spiel läuft. Fenster schließen beendet den Test.'
& $GodotConsole --path $Game 2>&1 | Where-Object { $_ -match 'ERROR|Exception' } | ForEach-Object { Write-Host "   $_" -ForegroundColor DarkYellow }

if (-not $KeepSave) {
    Restore-Userdata $backup
    Write-Host '   Spielstand und Einstellungen zurückgespielt.'
}
Write-Host ''
Write-Host 'Test beendet.' -ForegroundColor Green
Start-Sleep -Seconds 2

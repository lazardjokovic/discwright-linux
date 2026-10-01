<#
.SYNOPSIS
    Has Windows DiscWright write the discproject.json the tests here compare
    against, with its own Save-Project.

.DESCRIPTION
    Runs on Windows, under Windows PowerShell 5.1. Loads the function
    definitions out of DiscWright.ps1 the way its own suite does, then writes a
    project holding one of each kind of entry: a GOG download and a folder of
    game files. Both values of Source appear in it, so the schema the port
    claims to write is the schema Windows really writes rather than this port's
    idea of it.

    The output goes to tests\fixtures\projects\windows-<version>.json, named
    after the version of DiscWright.ps1 that made it, read out of the script.

    Written because this was done by hand for schema 9 and would have been done
    by hand again for schema 10.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File tools\windows\Make-ProjectReference.ps1 `
        -DiscWright ..\discwright\DiscWright.ps1

.EXAMPLE
    # With a real GOG download rather than a made-up one, which is better when
    # there is one to hand.
    .\tools\windows\Make-ProjectReference.ps1 -DiscWright ..\discwright\DiscWright.ps1 `
        -GogFolder 'F:\DWdemo\Alan Wake'
#>
param(
    [Parameter(Mandatory)][string]$DiscWright,

    # A real GOG download, if there is one. Without it a stand-in is built,
    # which is enough for the schema but names a folder that never existed.
    [string]$GogFolder,

    # Where the made-up folders go. They are deleted afterwards; only the JSON
    # is kept, and it names these paths.
    [string]$WorkDir = (Join-Path $env:TEMP 'dwprojref')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$source = Get-Content -LiteralPath $DiscWright -Raw
$version = [regex]::Match($source, '\$APP_VERSION\s*=\s*''([^'']+)''').Groups[1].Value
if (-not $version) { throw "No `$APP_VERSION in $DiscWright" }

$parseErrors = $null
$ast = [System.Management.Automation.Language.Parser]::ParseFile($DiscWright, [ref]$null, [ref]$parseErrors)
if ($parseErrors -and $parseErrors.Count) { throw "$DiscWright has $($parseErrors.Count) parse errors" }
foreach ($f in $ast.FindAll({ param($n)
        $n -is [System.Management.Automation.Language.FunctionDefinitionAst] }, $false)) {
    . ([scriptblock]::Create($f.Extent.Text))
}
$PROJECT_FILE = 'discproject.json'
# Dot-sourcing the functions leaves the script's own constants unset, and the
# app writes its version into every project file. A reference that says null
# there does not look like one a real run wrote.
$APP_VERSION = $version

if (Test-Path $WorkDir) { Remove-Item -LiteralPath $WorkDir -Recurse -Force }
New-Item -ItemType Directory -Force -Path $WorkDir | Out-Null

# A GOG download, real if one was named.
if (-not $GogFolder) {
    $GogFolder = Join-Path $WorkDir 'gog_game'
    New-Item -ItemType Directory -Force -Path $GogFolder | Out-Null
    $fs = [IO.File]::Create((Join-Path $GogFolder 'setup_gog_game_1.0_(12345).exe'))
    $fs.SetLength(3MB); $fs.Close()
}
$gog = Get-GameInfo $GogFolder
if (-not $gog.Ok) { throw "the GOG folder was refused: $($gog.Msg)" }

# A folder of game files, which is what schema 9 added: subfolders and all, and
# an installer named inside it.
$files = Join-Path $WorkDir 'Portable Game'
New-Item -ItemType Directory -Force -Path (Join-Path $files 'data') | Out-Null
$fs = [IO.File]::Create((Join-Path $files 'PortableGame.exe')); $fs.SetLength(2MB); $fs.Close()
Set-Content -LiteralPath (Join-Path $files 'data\config.ini') -Value 'x=1' -Encoding Ascii
$loose = Get-FolderInfo $files (Join-Path $files 'PortableGame.exe')
if (-not $loose.Ok) { throw "the files folder was refused: $($loose.Msg)" }

$out = Join-Path $WorkDir 'out'
New-Item -ItemType Directory -Force -Path $out | Out-Null
$null = Save-Project @{
    Games = @($gog, $loose); Label = 'REFERENCE DISC'
    IconPath = 'C:\art\icon.ico'; IconIsIco = $true; Menu = $true
    BgPath = 'C:\art\background.jpg'; BgAsIs = $false; PanelSide = 'Right'
    # Schema 10's two pictures, set to real paths rather than left empty. The
    # port does not print anything and carries them anyway, so that a project
    # opened and saved on Linux does not come back with somebody's choice
    # quietly gone. A reference file with nulls in it would let that through.
    CoverPath = 'C:\art\cover.png'; DiscArtPath = 'C:\art\disc-face.png'
    Divider = $false; ShowTitle = $true; TitleText = 'REFERENCE DISC'
    WindowBorder = $true; ButtonStyle = 'Minimal'; MusicFile = $null
    Buttons = @('Play', 'Install', 'Manual', 'Extras', 'Exit')
    ManualPath = $null; ExtrasPath = $null; ExtraItems = @()
    MediaKey = ''; LinuxInfo = $true; LegacyFs = $false } $out

$fixtures = Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) 'tests\fixtures\projects'
$dest = Join-Path $fixtures "windows-$version.json"
Copy-Item (Join-Path $out $PROJECT_FILE) $dest -Force
Remove-Item -LiteralPath $WorkDir -Recurse -Force

$raw = [IO.File]::ReadAllBytes($dest)
$bom = ($raw[0] -eq 0xEF -and $raw[1] -eq 0xBB -and $raw[2] -eq 0xBF)
$doc = Get-Content -LiteralPath $dest -Raw | ConvertFrom-Json
"Written by DiscWright $version into $dest"
"  schema  : $($doc.Version)"
"  entries : " + (($doc.Games | ForEach-Object { "$($_.GameName) [$($_.Source)]" }) -join ', ')
"  utf-8 with a byte order mark : $bom"
"  crlf only                    : $(-not ([Text.Encoding]::UTF8.GetString($raw) -replace "`r`n", '' -match "`n"))"
""
"Now point tests\test_project.py at it and run the suite."

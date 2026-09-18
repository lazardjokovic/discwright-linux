<#
.SYNOPSIS
    Compares an ISO built on Linux with one built by Windows DiscWright, the way
    Windows sees them.

.DESCRIPTION
    Runs on Windows, under Windows PowerShell 5.1. Mounts each ISO, hashes every
    file, and reads the volume label, the name Explorer shows for the drive, and
    the filesystems each disc carries. The filesystems come from the Volume
    Recognition Sequence at byte 32768, not from a tool that might guess.

    Both ISOs should come from the same staged disc folder, so that every
    difference is the writer's. See docs/xorriso-spike.md for the run this was
    written for.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File tools\windows\Compare-Isos.ps1 `
        -WindowsIso 'F:\DWdemo\out\ALAN WAKE.iso' -LinuxIso 'F:\DWdemo\linuxspike\alanwake-linux.iso'
#>
param(
    [Parameter(Mandatory)][string]$WindowsIso,
    [Parameter(Mandatory)][string]$LinuxIso
)
$ErrorActionPreference = 'Stop'

function Get-Filesystems([string]$Path) {
    $fs = [IO.File]::OpenRead($Path)
    try {
        [void]$fs.Seek(32768, [IO.SeekOrigin]::Begin)
        $buf = New-Object byte[] 2048
        $ids = @()
        for ($i = 0; $i -lt 32; $i++) {
            if ($fs.Read($buf, 0, 2048) -lt 7) { break }
            $id = [Text.Encoding]::ASCII.GetString($buf, 1, 5)
            if ($id -notmatch '^[A-Z0-9]{5}$') { break }
            $ids += $id
        }
        return (($ids | Select-Object -Unique) -join ' ')
    } finally { $fs.Dispose() }
}

function Open-Iso([string]$Path) {
    $img = Mount-DiskImage -ImagePath $Path -PassThru
    Start-Sleep -Seconds 2
    $vol = $img | Get-Volume
    $root = $vol.DriveLetter + ':\'
    $shell = New-Object -ComObject Shell.Application
    $shown = $shell.NameSpace(17).ParseName($root).Name
    $files = @{}
    Get-ChildItem -LiteralPath $root -Recurse -Force -File | ForEach-Object {
        $rel = $_.FullName.Substring($root.Length)
        $files[$rel] = @{ Size = $_.Length; Hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
    }
    return [pscustomobject]@{
        Label = $vol.FileSystemLabel; FsType = $vol.FileSystem
        Shown = $shown; Files = $files; Vrs = (Get-Filesystems $Path)
    }
}

$sides = @()
foreach ($p in $WindowsIso, $LinuxIso) {
    Write-Host ("mounting and hashing " + (Split-Path $p -Leaf) + ' ...')
    try { $sides += Open-Iso $p }
    finally { Dismount-DiskImage -ImagePath $p | Out-Null }
}
$w, $l = $sides

Write-Host ''
'{0,-22} {1,-38} {2}' -f '', 'built on Windows', 'built on Linux'
'{0,-22} {1,-38} {2}' -f 'filesystems (VRS)', $w.Vrs, $l.Vrs
'{0,-22} {1,-38} {2}' -f 'Windows mounts it as', $w.FsType, $l.FsType
'{0,-22} {1,-38} {2}' -f 'volume label', $w.Label, $l.Label
'{0,-22} {1,-38} {2}' -f 'Explorer shows', $w.Shown, $l.Shown
'{0,-22} {1,-38} {2}' -f 'files', $w.Files.Count, $l.Files.Count

Write-Host ''
$bad = 0
foreach ($k in ($w.Files.Keys + $l.Files.Keys | Sort-Object -Unique)) {
    $a = $w.Files[$k]; $b = $l.Files[$k]
    if (-not $a)             { Write-Host "  only on Linux disc:   $k"; $bad++; continue }
    if (-not $b)             { Write-Host "  only on Windows disc: $k"; $bad++; continue }
    if ($a.Hash -ne $b.Hash) { Write-Host ("  DIFFERENT: $k  ({0} vs {1} bytes)" -f $a.Size, $b.Size); $bad++ }
}
if ($bad -eq 0) { Write-Host ("every file identical, byte for byte ({0} files)" -f $w.Files.Count) }
else            { Write-Host "$bad differences"; exit 1 }

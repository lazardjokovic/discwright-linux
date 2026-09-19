<#
.SYNOPSIS
    Has Windows DiscWright compose the menu backgrounds the Linux tests compare
    against.

.DESCRIPTION
    Runs on Windows, under Windows PowerShell 5.1. Loads the function definitions
    out of DiscWright.ps1 the way its own test suite does (the script cannot be
    dot-sourced, because its last statement opens the window) and calls the real
    New-Background on the two pictures in tests\fixtures\background, once for
    each case in tests\test_background.py.

    The output goes to tests\fixtures\background\windows-<version>, named after
    the version of DiscWright.ps1 that made it, read out of the script itself.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File tools\windows\Make-BackgroundReference.ps1 `
        -DiscWright C:\Users\me\Projects\discwright\DiscWright.ps1
#>
param(
    [Parameter(Mandatory)][string]$DiscWright
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$source = Get-Content -LiteralPath $DiscWright -Raw
$version = [regex]::Match($source, '\$APP_VERSION\s*=\s*''([^'']+)''').Groups[1].Value
if (-not $version) { throw "No `$APP_VERSION in $DiscWright" }

$parseErrors = $null
$ast = [System.Management.Automation.Language.Parser]::ParseFile($DiscWright, [ref]$null, [ref]$parseErrors)
if ($parseErrors -and $parseErrors.Count) { throw "$DiscWright has $($parseErrors.Count) parse errors" }
foreach ($f in $ast.FindAll({ param($n) $n -is [System.Management.Automation.Language.FunctionDefinitionAst] }, $false)) {
    . ([scriptblock]::Create($f.Extent.Text))
}

$fixtures = Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) 'tests\fixtures\background'
$out = Join-Path $fixtures "windows-$version"
New-Item -ItemType Directory -Force -Path $out | Out-Null

# Name, source picture, title, panel side, divider, title shown. The leading
# commas keep each case an array of its own; without them @() flattens them all.
# Keep in step with CASES in tests/test_background.py.
$cases = @(
    ,@('wide-right',          'wide.png',   '',                                      'Right', $false, $false)
    ,@('wide-left',           'wide.png',   '',                                      'Left',  $false, $false)
    ,@('wide-left-divider',   'wide.png',   '',                                      'Left',  $true,  $false)
    ,@('narrow-right',        'narrow.png', '',                                      'Right', $false, $false)
    ,@('narrow-left',         'narrow.png', '',                                      'Left',  $false, $false)
    ,@('wide-right-title',    'wide.png',   'ALAN WAKE',                             'Right', $false, $true)
    ,@('wide-left-long-title','wide.png',   'THE WITCHER ENHANCED EDITION DIRECTORS CUT', 'Left', $false, $true)
    ,@('wide-right-no-title', 'wide.png',   'ALAN WAKE',                             'Right', $false, $false)
)
foreach ($c in $cases) {
    $dest = Join-Path $out ($c[0] + '.png')
    New-Background (Join-Path $fixtures $c[1]) $c[2] $dest $c[3] $c[4] $c[5]
    "  $($c[0]).png"
}
"Composed by DiscWright $version into $out"

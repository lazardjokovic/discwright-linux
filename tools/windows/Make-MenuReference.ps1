<#
.SYNOPSIS
    Has Windows DiscWright write the menus the Linux tests compare against.

.DESCRIPTION
    Runs on Windows, under Windows PowerShell 5.1. Loads the function definitions
    out of DiscWright.ps1 the way its own test suite does and calls the real
    New-MenuHta once for each case in tests\fixtures\menu\cases.json, which
    tests\test_menu.py reads too, so the two cannot drift apart.

    The output goes to tests\fixtures\menu\windows-<version>, named after the
    version of DiscWright.ps1 that made it, read out of the script itself.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File tools\windows\Make-MenuReference.ps1 `
        -DiscWright C:\Users\me\Projects\discwright\DiscWright.ps1
#>
param(
    [Parameter(Mandatory)][string]$DiscWright
)
$ErrorActionPreference = 'Stop'

$source = Get-Content -LiteralPath $DiscWright -Raw
$version = [regex]::Match($source, '\$APP_VERSION\s*=\s*''([^'']+)''').Groups[1].Value
if (-not $version) { throw "No `$APP_VERSION in $DiscWright" }

$parseErrors = $null
$ast = [System.Management.Automation.Language.Parser]::ParseFile($DiscWright, [ref]$null, [ref]$parseErrors)
if ($parseErrors -and $parseErrors.Count) { throw "$DiscWright has $($parseErrors.Count) parse errors" }
foreach ($f in $ast.FindAll({ param($n) $n -is [System.Management.Automation.Language.FunctionDefinitionAst] }, $false)) {
    . ([scriptblock]::Create($f.Extent.Text))
}

$fixtures = Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) 'tests\fixtures\menu'
$out = Join-Path $fixtures "windows-$version"
New-Item -ItemType Directory -Force -Path $out | Out-Null

# UTF-8 explicitly: Windows PowerShell reads a file without a BOM as ANSI.
$cases = (Get-Content -LiteralPath (Join-Path $fixtures 'cases.json') -Raw -Encoding UTF8 | ConvertFrom-Json).cases
foreach ($c in $cases) {
    # New-MenuHta takes a hashtable; ConvertFrom-Json gives an object.
    $cfg = @{}
    foreach ($p in $c.cfg.PSObject.Properties) { $cfg[$p.Name] = $p.Value }
    New-MenuHta $cfg (Join-Path $out ($c.name + '.hta'))
    "  $($c.name).hta"
}
"Written by DiscWright $version into $out"

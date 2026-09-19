<#
.SYNOPSIS
    Reads a discproject.json with Windows DiscWright's own Import-Project.

.DESCRIPTION
    Runs on Windows, under Windows PowerShell 5.1. Loads the function
    definitions out of DiscWright.ps1 the way its own test suite does, reads the
    file with the real Import-Project, and prints what it got as JSON. That is
    how a project written on Linux is checked to open on Windows, including the
    byte order mark, which PowerShell 5.1 needs to read the file as UTF-8.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File tools\windows\Read-Project.ps1 `
        -DiscWright ..\discwright\DiscWright.ps1 -Project out\discproject.json
#>
param(
    [Parameter(Mandatory)][string]$DiscWright,
    [Parameter(Mandatory)][string]$Project
)
$ErrorActionPreference = 'Stop'

$parseErrors = $null
$ast = [System.Management.Automation.Language.Parser]::ParseFile($DiscWright, [ref]$null, [ref]$parseErrors)
if ($parseErrors -and $parseErrors.Count) { throw "$DiscWright has $($parseErrors.Count) parse errors" }
foreach ($f in $ast.FindAll({ param($n) $n -is [System.Management.Automation.Language.FunctionDefinitionAst] }, $false)) {
    . ([scriptblock]::Create($f.Extent.Text))
}

$p = Import-Project $Project
if (-not $p) { throw "Import-Project could not read $Project" }
$p | ConvertTo-Json -Depth 6

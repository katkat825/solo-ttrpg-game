<#
.SYNOPSIS
    Checks every shipped map parses, so a malformed one is caught before it ships.

.DESCRIPTION
    A map that half-loads is a room with a wall missing and nothing to tell you. MapReader
    refuses a broken map and names the line, but Board.Load then falls back to an empty room and
    carries on - so a malformed map that shipped is silent in the game rather than an error
    anyone sees. This is the machine that makes it loud: it reads every res://maps/*.map through
    the same MapReader the board uses and fails naming any that don't parse.

    Exit code 0 if every map parses, 1 if any doesn't (or if the folder is empty). Safe to wire
    into a pre-commit hook or CI beside check-fairness.ps1 and check-locale.ps1.

    Run it after adding or editing a map.

.PARAMETER Godot
    Path to the Godot mono console binary. Falls back to $env:GODOT, then a search of
    $env:GODOT_ROOT or C:\Godot. Discovery lives in Find-Godot.ps1, which every check script
    dot-sources.

.EXAMPLE
    .\check-maps.ps1
#>
[CmdletBinding()]
param(
    [string] $Godot
)

$ErrorActionPreference = 'Stop'

# discovery lives in one file, dot-sourced by every check script - see Find-Godot.ps1
. (Join-Path $PSScriptRoot 'Find-Godot.ps1')
$Godot = Find-Godot $Godot

$project = Join-Path $PSScriptRoot 'game'

Write-Host "godot   $Godot"
Write-Host "project $project"
Write-Host ""

& $Godot --headless --path $project 'res://Diagnostics/map_check.tscn'
exit $LASTEXITCODE

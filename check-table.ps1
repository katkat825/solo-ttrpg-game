<#
.SYNOPSIS
    Walks a place on the real table: the mat, the figures on it, the cards beside them, and a
    fight begun and left on the same map.

.DESCRIPTION
    check-world.ps1 walks a world with no table at all, and that is the proof the World layer is
    Godot-free. This is the other half of the claim: the same world, shown.

    It boots the shipped table.tscn - board, tray, DM, companion, sheet - points its walk at a
    campaign, and checks what a person would otherwise have to check by looking:

      - the mat on the table is the place being walked, and the hero piece is where the world
        has them
      - everybody present is a figure on it, not only the ones you could hit
      - the sheet offers exactly the checks this place allows, and they have words
      - walking up to somebody lays their verbs out as cards, with words on them
      - answering one puts the author's line on the DM's note
      - a place change is the DM's hands lifting the old mat away and laying the new one down,
        in that order
      - a swing becomes a real fight mustered from what was standing there, and the end of one
        becomes facts and figures back on the mat

    It does NOT play the fight. check-fight.ps1 does that properly on the real felt, and a second
    worse copy of it here would be worth less than nothing - so this forces the end and checks
    what comes back out.

    Exit code 0 if the table walked, 1 if it did not.

.PARAMETER Godot
    Path to the Godot mono console binary. Falls back to $env:GODOT, then a search of
    $env:GODOT_ROOT or C:\Godot. Discovery lives in Find-Godot.ps1.

.PARAMETER Campaign
    Which campaign to walk. Defaults to saltmarch, which is the one written to be a world.

.EXAMPLE
    .\check-table.ps1
    .\check-table.ps1 -Campaign greyhollow -Place the_hollow
#>
[CmdletBinding()]
param(
    [string] $Godot,
    [string] $Campaign = '',
    [string] $Place = '',
    [int] $Seed = 0
)

$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'Find-Godot.ps1')
$Godot = Find-Godot $Godot

$project = Join-Path $PSScriptRoot 'game'

Write-Host "godot   $Godot"
Write-Host "project $project"
Write-Host ""

$userArgs = @()
if ($Campaign) { $userArgs += "--campaign=$Campaign" }
if ($Place)    { $userArgs += "--place=$Place" }
if ($Seed)     { $userArgs += "--seed=$Seed" }

if ($userArgs.Count -gt 0) {
    & $Godot --headless --path $project 'res://Diagnostics/table_check.tscn' -- @userArgs
} else {
    & $Godot --headless --path $project 'res://Diagnostics/table_check.tscn'
}

exit $LASTEXITCODE

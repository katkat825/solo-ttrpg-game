<#
.SYNOPSIS
    Boots the real room, walks its objects, fills in a character sheet, and proves the build has no
    menus in it.

.DESCRIPTION
    Phase R's mechanical half. "The room is the entire UI. There should be no menu screens anywhere
    in this game" (THE_TABLE.md section 6) is a hard rule, and a hard rule nobody checks is a rule
    that lasts until the first awkward milestone. This is the check.

    It instances the shipped room.tscn - which instances the shipped table.tscn, and with it the
    board, the tray, the DM, the companion and the sheet - and holds it to R5's verify list:

      - NO MENUS ANYWHERE. Every .tscn and .cs in the project is looked at, and a file whose name
        says menu, panel, modal, hud or screen fails the check. Only Diagnostics/ is excused, and
        that is named rather than pattern-matched so a second exception has to be argued for
      - every object THE_TABLE.md section 6 promises is in the room and has a name in this locale.
        A missing object is a thing the player cannot reach at all, because there is no menu to
        fall back on
      - the sheet fills in by touching its blanks, and there is no Confirm button anywhere in it
      - THE POOL THROWN ON THE TRAY IS BUILT FROM WHAT IS WRITTEN ON THE SHEET. Change a line and
        the dice change - which is the difference between the sheet being the character and the
        sheet being a picture of one
      - the DM picks the finished sheet up, turns it round, pauses on something and sets it down
      - a box on the shelf is a real save file, and taking one down puts its sheet on the table

    What it cannot check is whether any of it FEELS like sitting at a table, which is the actual
    verdict and still needs a person in the editor.

    Exit code 0 if the room holds together, 1 if it does not.

    BUILD THE PROJECT FIRST. This runs the compiled assembly, not the source: `dotnet build game`
    (or `dotnet test`, which builds it) before running, or you are checking the last build.

    RE-IMPORT AFTER EDITING game/locale/game.csv. The translation Godot reads is the .translation
    binary beside the CSV, and a new ui.* key that is in the CSV but not the binary shows up here as
    "has no name in this locale" while check-locale.ps1 passes. `godot --headless --path game
    --import` rebuilds it.

.PARAMETER Godot
    Path to the Godot mono console binary. Falls back to $env:GODOT, then a search of
    $env:GODOT_ROOT or C:\Godot. Discovery lives in Find-Godot.ps1, which every check script
    dot-sources.

.EXAMPLE
    .\check-room.ps1
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

& $Godot --headless --path $project 'res://Diagnostics/room_check.tscn'
exit $LASTEXITCODE

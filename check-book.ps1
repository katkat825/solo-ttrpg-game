<#
.SYNOPSIS
    Boots the real room, reads the books off its bookcase, and opens them.

.DESCRIPTION
    Phase BK's mechanical half. A campaign is a book you pick up and open, the bookcase is the corner
    of the room a main menu would have been, and the campaign book's contents page is the pause menu
    that is not one. Most of that is an eye check - a book has to LOOK like a book - but the claims
    underneath are checkable, and these are the ones that would rot silently:

      - A CAMPAIGN IS A BOOK, and there is one on the case for every campaign installed, whether it
        has ever been played or not. That is the difference between a bookcase and a save folder
      - THE CONTENTS PAGE IS A PAGE, with a real word on every line, and every line turns to
        something. A contents page reading as a key is a bug the locale audit cannot see
      - THE RULES BOOK IS A PEER. Pressing "?" opens it over whatever you were reading and closing
        it puts you back on that page, because a book handed to you does not take the one you held
      - FIVE CHARACTERS PER CAMPAIGN AND A BLANK RIBBON, with the blank gone at five and a sixth
        refused - counted off the save folder rather than from a number kept somewhere
      - THE RELOAD TABS: five down the edge, newest first, a sixth for the next five, and this
        campaign's saves rather than the whole folder's
      - THE STORY SO FAR, and the half of it that survives a reload because it is derived from facts
      - A SIDE ERRAND CAN BE HANDED BACK AND TAKEN ON AGAIN, and the story you are in cannot
      - THE WORKSHOP DOOR: one blank object per content type, each naming the local skeleton it
        copies. A content type with no skeleton yet is a CAUTION and says so - the Workshop itself
        is Steam's (CONTENT_PIPELINE.md P8)
      - THE DICE-SKIN SWAP from the book, including that a skin nobody owns is refused

    Exit code 0 if the books hold together, 1 if they do not. Cautions are printed and do not fail.

    BUILD THE PROJECT FIRST. This runs the compiled assembly, not the source: `dotnet build game`
    (or `dotnet test`, which builds it) before running, or you are checking the last build.

    RE-IMPORT AFTER EDITING game/locale/game.csv. The translation Godot reads is the .translation
    binary beside the CSV, and a new ui.* key that is in the CSV but not the binary shows up here as
    "has no words" while check-locale.ps1 passes. `godot --headless --path game --import` rebuilds it.

.PARAMETER Godot
    Path to the Godot mono console binary. Falls back to $env:GODOT, then a search of
    $env:GODOT_ROOT or C:\Godot. Discovery lives in Find-Godot.ps1, which every check script
    dot-sources.

.EXAMPLE
    .\check-book.ps1
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

& $Godot --headless --path $project 'res://Diagnostics/book_check.tscn'
exit $LASTEXITCODE

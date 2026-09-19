<#
.SYNOPSIS
    Boots the real room and plays it with no mouse, no eyes and no ears.

.DESCRIPTION
    Phase AX's mechanical half. Accessibility is a standing requirement, held like "no menus" rather
    than saved for a someday pass - and a standing rule with nothing checking it is a rule that lasts
    until the first awkward milestone. This is the check.

    It instances the shipped room.tscn - which instances the shipped table.tscn, and with it the
    board, the tray, the DM, the companion and the sheet - and holds it to what a machine can hold:

      - EVERY REACHABLE THING HAS A NAME, A BODY, AND A BODY BIG ENOUGH TO HIT. A thing with no name
        is silent to a screen reader; a thing too small to land on is a thing the player cannot do at
        all, because there is no menu to fall back on
      - THE HAND GETS EVERYWHERE AND COMES ROUND. Reaching walks every live thing exactly once and
        wraps, and reaches backward too - which is the difference between keyboard navigation and a
        keyboard trap
      - NOTHING SAID OUT LOUD IS STILL A KEY. A missing string on screen reads as ui.door.name and is
        impossible to miss; in a voice it is a noise, and nothing else would catch it
      - EVERY DIAL ON THE SETTINGS PAGE TURNS, changes what it says it changes, and is still true when
        it is read back off disk
      - EVERY ACT HAS A KEY, IS IN THE INPUT MAP, AND SHARES IT WITH NOTHING - and rebinding refuses a
        key another act holds rather than leaving two acts on it
      - RESIZABLE TEXT RESIZES EVERY WORD ON THE TABLE AND PUTS THEM ALL BACK EXACTLY, which is the
        one that would rot silently: scaling from the current size rather than the authored one
        compounds, and three visits to the settings page leave the table unreadable
      - NOTHING MEANS ANYTHING BY COLOUR ALONE. Every coloured cue in the game is in the register with
        the twin that carries the same meaning without colour, and a cue with no twin fails
      - EVERY SOUND HAS WORDS, and a caption for a folder with no recordings in it is printed as a
        caution rather than pretended about
      - THE ROOM IS DARK AT MIDNIGHT IN DECEMBER AND IS NOT AT NOON IN JUNE, and nine in the evening
        is not the same room in June as in December - which is the whole reason the date is in it

    IT NEEDS A PERSON AFTER IT. Whether the room actually reads well aloud, whether high contrast is
    contrast, whether the arms look like anybody's - those are an ear-and-eye check, the same as the
    rest of the presentation layer. A headless machine also has no text-to-speech voice at all, so the
    check says so and counts what would have been spoken instead.

    Exit code 0 if the room can be played without a mouse, 1 if it cannot.

    BUILD THE PROJECT FIRST. This runs the compiled assembly, not the source: `dotnet build game`
    (or `dotnet test`, which builds it) before running, or you are checking the last build.

    RE-IMPORT AFTER EDITING game/locale/game.csv. The translation Godot reads is the .translation
    binary beside the CSV, and a new ui.* key that is in the CSV but not the binary shows up here as
    a reachable thing whose name is still a key, while check-locale.ps1 passes.
    `godot --headless --path game --import` rebuilds it.

.PARAMETER Godot
    Path to the Godot mono console binary. Falls back to $env:GODOT, then a search of
    $env:GODOT_ROOT or C:\Godot. Discovery lives in Find-Godot.ps1, which every check script
    dot-sources.

.EXAMPLE
    .\check-access.ps1
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

& $Godot --headless --path $project 'res://Diagnostics/access_check.tscn'
exit $LASTEXITCODE

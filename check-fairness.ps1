<#
.SYNOPSIS
    Throws the dice tray's dice thousands of times headless and checks the faces are uniform.

.DESCRIPTION
    Physics dice are not fair by construction, and a die that favours a face
    invalidates every number the balance simulator produces.
    Runs the real dice_tray.tscn - not a copy - with many independent pools in the air at
    once, so 2,000 throws take about half a minute instead of an hour.

    Exit code 0 if the dice look fair, 1 if any of them is measurably biased. Safe to wire
    into a pre-commit hook or CI.

    At 2,000 throws this reliably catches a face running at 22% or worse, and catches the
    25% case every time. Subtler bias needs more throws - try 10,000.

.PARAMETER Throws
    How many settles to count. Defaults to DiceFairness.DefaultThrows - 2000 at the time of
    writing, and that file is the one to check rather than this line.

.PARAMETER Dice
    How many dice are thrown together AND collide with each other. Defaults to
    DiceFairness.DefaultPoolSize - 1, which measures a die alone in the tray, the M2 experiment. Pass 3 for the M3 experiment: three dice
    thrown on the same frame, genuinely knocking into one another. These are different
    physical systems and a die that is fair alone can be biased in a crowd, so a pass at
    -Dice 1 says nothing about -Dice 3.

    Cannot exceed the number of throw points in the scene - colliding dice must not spawn
    inside each other.

.PARAMETER Shape
    Which die to measure, as a number of sides: 4, 6, 8, 10 or 12. Defaults to
    DiceFairness.DefaultShape - a d6. Every die in the sweep is switched to it, whatever the tray
    scene was saved with, so a run always measures one shape and always says which.

    Worth running all five. A cube is the shape least likely to be unfair - its faces are
    all square to one another - so a pass at 6 says the least about the other four. The d10
    is the one to distrust: its kite faces meet at shallow angles, which makes it the
    likeliest of the set to come to rest against a wall on something that isn't a face.

.PARAMETER Tray
    Which tray skin to measure, by bare name - a .tres in game/Tray/skins/. Left off, the
    sweep measures WHATEVER SKIN THE SCENE SHIPS WITH and names it in the report - which is
    the only default that keeps meaning "the tray the game is played in" if the scene's skin
    ever changes. This script named 'wood' by hand until F5 and would have gone on measuring
    wood after the game stopped shipping it.

    A skin changes friction and bounce, and bounce is exactly what decides how a die settles,
    so this is a real experiment and not a cosmetic flag. A pass in one tray says nothing
    about another, and a MIXED tray is a third physical system that neither of its materials
    covers on its own: felt is fine, wood is fine, felt-with-wood has to be swept itself.

    EVERY SKIN SHIPS ONLY AFTER ITS OWN SWEEP. A tray that quietly favours a face is a hidden
    mechanical difference, and the art direction says cosmetics never affect
    mechanics.

.PARAMETER Parallel
    How many independent pools run at once. Purely a speed knob; pools cannot see each
    other, so this changes wall-clock and nothing else. Defaults to 50 solo dice, or about
    48 dice worth of colliding pools.

.PARAMETER Godot
    Path to the Godot mono console binary. Falls back to $env:GODOT, then a search of
    $env:GODOT_ROOT or C:\Godot. Discovery lives in Find-Godot.ps1, which every check script
    dot-sources.

.EXAMPLE
    .\check-fairness.ps1
    .\check-fairness.ps1 -Dice 3
    .\check-fairness.ps1 -Shape 10
    .\check-fairness.ps1 -Dice 3 -Throws 10000
    .\check-fairness.ps1 -Tray gamblers -Dice 3
#>
# EVERY DEFAULT BELOW IS ZERO OR EMPTY, MEANING "NOT SPECIFIED", AND THAT IS DELIBERATE (F5).
# The sweep's real defaults live in DiceFairness.cs and only there. This script used to state
# its own copies and they had already diverged - it defaulted -Tray to 'wood' while the C#
# defaulted to "none" - so a bare .\check-fairness.ps1 and a bare --fairness= measured two
# different things and nothing said so. Only what was actually asked for is passed on now, and
# the sweep prints what it filled in.
[CmdletBinding()]
param(
    [int] $Throws = 0,
    [int] $Dice = 0,
    [ValidateSet(0, 4, 6, 8, 10, 12)]
    [int] $Shape = 0,
    [string] $Tray,
    [int] $Parallel = 0,
    [string] $Godot
)

$ErrorActionPreference = 'Stop'

# discovery lives in one file, dot-sourced by every check script - see Find-Godot.ps1
. (Join-Path $PSScriptRoot 'Find-Godot.ps1')
$Godot = Find-Godot $Godot

$project = Join-Path $PSScriptRoot 'game'

# checked here rather than in the engine so a typo costs a second instead of a scene load
if ($Tray) {
    $skin = Join-Path $PSScriptRoot "game\Tray\skins\$Tray.tres"
    if (-not (Test-Path $skin)) {
        $available = (Get-ChildItem (Join-Path $PSScriptRoot 'game\Tray\skins') -Filter '*.tres' |
                      Select-Object -ExpandProperty BaseName) -join ', '
        Write-Error "No tray skin '$Tray'. Available: $available"
    }
}

# --fairness is what asks for a sweep at all, so it always goes; the rest only when given
$sweepArgs = @(if ($Throws -gt 0) { "--fairness=$Throws" } else { '--fairness' })
if ($Dice -gt 0)     { $sweepArgs += "--dice=$Dice" }
if ($Shape -gt 0)    { $sweepArgs += "--shape=$Shape" }
if ($Tray)           { $sweepArgs += "--tray=$Tray" }
if ($Parallel -gt 0) { $sweepArgs += "--parallel=$Parallel" }

Write-Host "godot   $Godot"
Write-Host "project $project"
Write-Host "sweep   $($sweepArgs -join ' ')"
Write-Host ""

& $Godot --headless --path $project -- @sweepArgs
exit $LASTEXITCODE

<#
.SYNOPSIS
    Plays the fight headless on the real table and checks it against what the felt says.

.DESCRIPTION
    Phase C is the phase you have to watch - no test can say whether two actions a round feels
    like heroism. This checks the half that goes wrong quietly instead: that the damage dealt is
    the number on the Impact die with the ring round it, that the tally beside a piece says what
    the Actor says, that a downed piece leaves its square, and that a foe's turn happens exactly
    once.

    It instances table.tscn - the shipped scene, the real board, real physics dice on real felt -
    and drives it through Board.Claims, the same entry point a mouse click uses. Every swing is a
    genuine handful of dice settling, so it takes about a second and a half each.

    Exit code 0 if the fight holds together, 1 if it doesn't. Safe to wire into a pre-commit hook
    or CI beside check-fairness.ps1 and check-locale.ps1.

    BUILD THE PROJECT FIRST. This runs the compiled assembly, not the source: `dotnet build game`
    (or `dotnet test`, which builds it) before running, or you are checking the last build.

.PARAMETER Fights
    How many complete fights to play. Default is the check's own, which is 3.

.PARAMETER Plain
    Play it straight and measure it. The check normally handicaps the hero on purpose to reach
    each milestone - Winding him to order, giving up a turn to arm a readied strike, spending
    Nerve on whatever is reachable, playing the last fight as a caster - which makes the win rate
    meaningless. With -Plain it does none of that, and the win rate is the number to hold against
    SIMULATION.md section 5.

.PARAMETER Campaign
    Which campaign's encounter to play as the first fight. Defaults to whatever FightCheck names,
    which is the one that ships. Pass a campaign id to play somebody else's - the second-campaign
    test (CONTENT_PIPELINE.md P7) is what this exists for, and it exists because that test found
    that a campaign could only be chosen by editing a scene.

.PARAMETER Encounter
    Which encounter in it. Defaults to FightCheck's own.

.PARAMETER Hero
    Which hero to play it as - an archetype id. Defaults to whatever the scene names, which is the
    Barbarian. Pass a class id to play a class authored in a folder (CLASSES_AND_KITS.md K0), e.g.
    hearthguard.warden. The caster's fight and the boss's ignore it, the same way they ignore
    -Campaign, because the check names its own hero for those.

.PARAMETER Grown
    Which growth steps the hero has been given - a comma-separated list of step ids off its class's
    own `growth` list (CLASSES_AND_KITS.md K3). Defaults to none. WHEN a hero earns one is a
    campaign's business; this is the hatch that lets a grown hero be played and checked before
    there is a chapter shell to grow him.

.PARAMETER Godot
    Path to the Godot mono console binary. Falls back to $env:GODOT, then a search of
    $env:GODOT_ROOT or C:\Godot. Discovery lives in Find-Godot.ps1, which every check script
    dot-sources.

.EXAMPLE
    .\check-fight.ps1

.EXAMPLE
    .\check-fight.ps1 -Fights 10

.EXAMPLE
    .\check-fight.ps1 -Campaign greyhollow -Encounter the_cistern

.EXAMPLE
    .\check-fight.ps1 -Hero hearthguard.warden
#>
[CmdletBinding()]
param(
    [int] $Fights,
    [switch] $Plain,
    [string] $Campaign,
    [string] $Encounter,
    [string] $Hero,
    [string] $Grown,
    [string] $Godot
)

$ErrorActionPreference = 'Stop'

# discovery lives in one file, dot-sourced by every check script - see Find-Godot.ps1
. (Join-Path $PSScriptRoot 'Find-Godot.ps1')
$Godot = Find-Godot $Godot

$project = Join-Path $PSScriptRoot 'game'

# only what was actually asked for is passed on, so FightCheck holds every default and the script
# cannot drift from it - the lesson F5 took out of check-fairness.ps1
$userArgs = @()
if ($PSBoundParameters.ContainsKey('Fights')) { $userArgs += "--fights=$Fights" }
if ($Plain) { $userArgs += '--plain' }
if ($Campaign) { $userArgs += "--campaign=$Campaign" }
if ($Encounter) { $userArgs += "--encounter=$Encounter" }
if ($Hero) { $userArgs += "--hero=$Hero" }
if ($Grown) { $userArgs += "--grown=$Grown" }

Write-Host "godot   $Godot"
Write-Host "project $project"
Write-Host ""

if ($userArgs.Count -gt 0) {
    & $Godot --headless --path $project 'res://Diagnostics/fight_check.tscn' -- @userArgs
} else {
    & $Godot --headless --path $project 'res://Diagnostics/fight_check.tscn'
}

exit $LASTEXITCODE

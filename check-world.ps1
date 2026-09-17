<#
.SYNOPSIS
    Walks a whole campaign world headless - places, people, verbs, a fight begun and left, roads,
    the quest log - and proves every place comes back identically from a save.

.DESCRIPTION
    The World layer's answer to the fairness sweep.

    The combat core has a rigorous is-it-correct story: the balance sim, check-fairness.ps1, the
    whole discipline in SIMULATION.md. The World layer cannot have one - YOU CANNOT CHI-SQUARED A
    TOWN - so what "correct" means for it is the content validator plus the save round-trip
    (PLACES_AND_PERSISTENCE.md section 10). check-campaign.ps1 runs the first half over every
    folder on the machine. This is the second half:

      - every place is entered in its own right, and remembers having been visited
      - what is standing on it is BASE MAP PLUS FACTS APPLIED and nothing else: the grave is not
        there until the man is dead, and the cleared room does not re-arm itself
      - every verb the author permitted is done, and a spent one leaves the menu because a fact
        says so - not because anything was remembered about the last time you looked
      - a fight BEGINS inside a place and ENDS back into it: the map is still there, the corpse is
        not, and who died is a durable fact. That transition is the only genuinely new runtime in
        the layer (section 9) and it is the thing this check exists to guard
      - every road is walked, and whatever the wayside table said happened has words behind it
      - the quest log is read off the facts, with a title in this locale
      - and then it saves, reloads, and asserts every place reconstructs IDENTICALLY - down to
        which verbs are still on the menu

    It is not a second engine. Content.World.Exploring is what the room drives, and it is
    Godot-free on purpose, which is the whole reason this check can exist at all.

    Exit code 0 if the world walked clean, 1 if it did not. A CAUTION is printed and does not fail
    the run: "this quest's turn-in NPC is attackable" is a sentence an author should read, not a
    refusal (section 6). Safe to wire into a pre-commit hook or CI beside check-campaign.ps1.

    BUILD THE PROJECT FIRST. This runs the compiled assembly, not the source: `dotnet build game`
    (or `dotnet test`, which builds it) before running, or you are checking the last build.

.PARAMETER Campaign
    Which campaign to walk. Defaults to whatever WorldCheck names - saltmarch, the one written to
    exercise every part of the layer.

.PARAMETER Seed
    The seed for the wayside tables and the loot. Defaults to WorldCheck's own, so two runs of this
    script walk the same world and a change in the output is a change in the code.

.PARAMETER Godot
    Path to the Godot mono console binary. Falls back to $env:GODOT, then a search of
    $env:GODOT_ROOT or C:\Godot. Discovery lives in Find-Godot.ps1, which every check script
    dot-sources.

.EXAMPLE
    .\check-world.ps1

.EXAMPLE
    .\check-world.ps1 -Campaign greyhollow
#>
[CmdletBinding()]
param(
    [string] $Campaign,
    [int] $Seed,
    [string] $Godot
)

$ErrorActionPreference = 'Stop'

# discovery lives in one file, dot-sourced by every check script - see Find-Godot.ps1
. (Join-Path $PSScriptRoot 'Find-Godot.ps1')
$Godot = Find-Godot $Godot

$project = Join-Path $PSScriptRoot 'game'

# only what was actually asked for is passed on, so WorldCheck holds every default and the script
# cannot drift from it - the lesson F5 took out of check-fairness.ps1
$userArgs = @()
if ($Campaign) { $userArgs += "--campaign=$Campaign" }
if ($PSBoundParameters.ContainsKey('Seed')) { $userArgs += "--seed=$Seed" }

Write-Host "godot   $Godot"
Write-Host "project $project"
Write-Host ""

if ($userArgs.Count -gt 0) {
    & $Godot --headless --path $project 'res://Diagnostics/world_check.tscn' -- @userArgs
} else {
    & $Godot --headless --path $project 'res://Diagnostics/world_check.tscn'
}

exit $LASTEXITCODE

<#
.SYNOPSIS
    Plays every conversation every installed campaign ships, headless, and holds the words to the
    locale.

.DESCRIPTION
    Phase W's mechanical half. A bark bank that compiles can still sound like nobody, and no script
    will ever tell you whether it does - that part stays a reading test. What this holds is
    everything around the words:

      - every conversation runs from its first line to its end, taking every branch offered,
        without the runtime complaining and without running away with itself
      - every line that comes out has words behind it in this locale
      - THE WORDS CAME THROUGH THE LOCALIZER. Each line is played twice, once in the pseudolocale;
        one that reads the same both times was shown from the .yarn source, which is English
        hard-coded in a data folder and the one mistake W0 is easy to make by accident
      - every beat of the shared spine is phrased by every voice on the shelf - W5's "no companion
        may withhold a beat the player needs", checked rather than promised
      - every rung of every hint ladder reaches a beat that exists and has words, and the fourth
        ask escalates to the companion noticing instead of repeating itself
      - a campaign with camp scenes has a quiet one, because a night falls back to quiet and to
        nothing else

    It is not a second reader. Content.Campaigns.Package is the validator and Content.Dialogue is
    the runtime the game plays; this runs both, so it cannot pass something the game then refuses.

    Exit code 0 if every conversation played clean, 1 if any did not. Safe to wire into a
    pre-commit hook or CI beside check-locale.ps1 and check-campaign.ps1.

    BUILD THE PROJECT FIRST. This runs the compiled assembly, not the source: `dotnet build game`
    (or `dotnet test`, which builds it) before running, or you are checking the last build.

.PARAMETER ExpectSome
    Fail if nothing on the shelf has a word of dialogue in it. Off by default, because a shelf of
    mini packs and class packs is a perfectly good shelf - turn it on in a build where a campaign
    with dialogue is expected to be there.

.PARAMETER Godot
    Path to the Godot mono console binary. Falls back to $env:GODOT, then a search of
    $env:GODOT_ROOT or C:\Godot. Discovery lives in Find-Godot.ps1, which every check script
    dot-sources.

.EXAMPLE
    .\check-dialogue.ps1

.EXAMPLE
    .\check-dialogue.ps1 -ExpectSome
#>
[CmdletBinding()]
param(
    [switch] $ExpectSome,
    [string] $Godot
)

$ErrorActionPreference = 'Stop'

# discovery lives in one file, dot-sourced by every check script - see Find-Godot.ps1
. (Join-Path $PSScriptRoot 'Find-Godot.ps1')
$Godot = Find-Godot $Godot

$project = Join-Path $PSScriptRoot 'game'

# only what was actually asked for is passed on, so DialogueCheck holds every default and the
# script cannot drift from it - the lesson F5 took out of check-fairness.ps1
$userArgs = @()
if ($ExpectSome) { $userArgs += '--expect-some' }

Write-Host "godot   $Godot"
Write-Host "project $project"
Write-Host ""

if ($userArgs.Count -gt 0) {
    & $Godot --headless --path $project 'res://Diagnostics/dialogue_check.tscn' -- @userArgs
} else {
    & $Godot --headless --path $project 'res://Diagnostics/dialogue_check.tscn'
}

exit $LASTEXITCODE

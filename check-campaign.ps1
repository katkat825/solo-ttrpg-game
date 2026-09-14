<#
.SYNOPSIS
    Validates every campaign folder on this machine and prints what each one turned into.

.DESCRIPTION
    A campaign is a folder, and a broken third-party folder is the normal case rather than a bug
    (ARCHITECTURE.md section 9). This runs the content validator over every campaign it can find -
    the local campaigns/ folder today, plus every subscribed Workshop item once P8 lands - and
    reports every schema and cross-reference problem at once, each named by its file and field.

    It is not a second program. Content.Campaigns.Package.Read IS the validator, and it is exactly
    what the game runs when it loads a shelf, so this cannot pass something the game then refuses.

    It also prints what a good campaign read back as: the id it registered under, its format and
    minimum engine version, its title as the locale gives it, its monsters, items, maps, chapters
    and encounters, and who stands on which spawn slot of which room. That half is for the author
    who has just written one and wants to see the game agree.

    Exit code 0 if every campaign loaded clean, 1 if any did not. Safe to wire into a pre-commit
    hook or CI beside check-maps.ps1 and check-locale.ps1.

    BUILD THE PROJECT FIRST. This runs the compiled assembly, not the source: `dotnet build game`
    (or `dotnet test`, which builds it) before running, or you are checking the last build.

.PARAMETER ExpectSome
    Fail if no campaigns were found. Off by default, because a fresh install with nothing
    subscribed has to boot and play (ARCHITECTURE.md section 8) - turn it on in a build where a
    campaign is expected to be there.

.PARAMETER Godot
    Path to the Godot mono console binary. Falls back to $env:GODOT, then a search of
    $env:GODOT_ROOT or C:\Godot. Discovery lives in Find-Godot.ps1, which every check script
    dot-sources.

.EXAMPLE
    .\check-campaign.ps1

.EXAMPLE
    .\check-campaign.ps1 -ExpectSome
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

# only what was actually asked for is passed on, so CampaignCheck holds every default and the
# script cannot drift from it - the lesson F5 took out of check-fairness.ps1
$userArgs = @()
if ($ExpectSome) { $userArgs += '--expect-some' }

Write-Host "godot   $Godot"
Write-Host "project $project"
Write-Host ""

if ($userArgs.Count -gt 0) {
    & $Godot --headless --path $project 'res://Diagnostics/campaign_check.tscn' -- @userArgs
} else {
    & $Godot --headless --path $project 'res://Diagnostics/campaign_check.tscn'
}

exit $LASTEXITCODE

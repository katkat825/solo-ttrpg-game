<#
.SYNOPSIS
    Checks the base game addresses the player in the second person and never gives them a gender.

.DESCRIPTION
    The player picks no gender and no pronouns; the DM and the companion address YOU
    (CONVENTIONS.md section 7, ACCESSIBILITY.md section 2). It is cleaner than pronoun substitution
    and it dodges a trap the key grammar creates - many languages inflect around gender well past a
    swapped word, and one-key-per-whole-sentence cannot assemble those from fragments.

    It was also the one standing rule with nothing holding it, which is why this exists: the
    content was already compliant when it was written, and a rule that is true by habit stays true
    until somebody writes forty barks in an afternoon.

    THIS IS NOT A GREP FOR "he". NPCs are gendered, legitimately and constantly, so the file is
    read in two scopes:

      - SPOKEN TO YOU - the engine's own strings under combat.* and ui.*, a voice reading the throw
        back, and a companion's barks. There is no third party in any of these: the subject is you,
        the dice, or the thing in front of you, and a monster is an "it". Any gendered word here is
        a problem
      - EVERYWHERE ELSE - conversation lines, narration, quest text. Pronouns are left alone
        entirely, because a third person is usually a real third person. Only a term of ADDRESS is
        flagged - "sir", "my lady", "lad" - because whoever is being addressed in this game is the
        player

    A campaign may keep a 'locale/gendered.allowed' file listing keys where a term of address is
    aimed at somebody in the fiction rather than at the player. The check says how many it excused.

    ENGLISH ONLY, and it says so when it runs: the word lists are English, so it holds the 'en'
    column. A locale that has gone wrong in another language needs a reader of that language.

    WORKSHOP CONTENT IS NOT HELD TO THIS. The campaigns checked are named in
    Diagnostics/voice_check.tscn, and that list is the whole of the exemption.

    Exit code 0 if the base game speaks to you, 1 if it speaks about you. Safe to wire into CI
    beside check-locale.ps1.

.PARAMETER Godot
    Path to the Godot mono console binary. Falls back to $env:GODOT, then a search of
    $env:GODOT_ROOT or C:\Godot. Discovery lives in Find-Godot.ps1, which every check script
    dot-sources.

.EXAMPLE
    .\check-voice.ps1
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

& $Godot --headless --path $project 'res://Diagnostics/voice_check.tscn'
exit $LASTEXITCODE

<#
.SYNOPSIS
    Checks the locale file covers every key the engine can put in front of a player.

.DESCRIPTION
    A missing translation does not crash and does not look
    broken: it puts "skill.larceny.name" on the screen and waits for someone to notice. Key
    discipline is what makes that failure findable by a machine instead, and this is the
    machine.

    Checks, in order:

      - the CSV is registered in Project Settings and the translation server loaded it
      - every key EngineKeys.All() emits has English against it
      - nothing in the file is spare - a key nothing emits is either a typo or dead weight
        a translator would be paid to translate
      - every key in the file obeys the grammar in KeyConventions
      - a *.name_numbered key has its {0}, and nothing else does

    Exit code 0 if the locale is complete, 1 if it isn't. Safe to wire into a pre-commit
    hook or CI beside check-fairness.ps1.

    Run it after adding a skill, a condition, an archetype or a piece of gear - all of which
    add keys to the checklist without touching this file.

.PARAMETER Godot
    Path to the Godot mono console binary. Falls back to $env:GODOT, then a search of C:\Godot.

.EXAMPLE
    .\check-locale.ps1
#>
[CmdletBinding()]
param(
    [string] $Godot
)

$ErrorActionPreference = 'Stop'

if (-not $Godot) { $Godot = $env:GODOT }

if (-not $Godot) {
    # The console binary, not the plain one: only that variant writes to stdout on Windows.
    $Godot = Get-ChildItem 'C:\Godot' -Recurse -Filter '*mono*console.exe' -ErrorAction SilentlyContinue |
             Sort-Object FullName -Descending |
             Select-Object -First 1 -ExpandProperty FullName
}

if (-not $Godot -or -not (Test-Path $Godot)) {
    Write-Error "Godot not found. Pass -Godot <path to the mono console exe> or set `$env:GODOT."
}

$project = Join-Path $PSScriptRoot 'game'

Write-Host "godot   $Godot"
Write-Host "project $project"
Write-Host ""

& $Godot --headless --path $project 'res://Diagnostics/locale_audit.tscn'
exit $LASTEXITCODE

<#
.SYNOPSIS
    Finds the Godot binary the headless checks run in. Dot-source it; don't run it.

.DESCRIPTION
    THE ONE PLACE THE GODOT ROOT AND THE BINARY HEURISTIC ARE WRITTEN DOWN.

    This was fourteen identical lines in check-fairness.ps1 and check-locale.ps1, comment
    included - and the two things in it most likely to need changing, the hardcoded C:\Godot
    root and the mono-console heuristic, were exactly the two that had to be changed twice.
    Phases B, C and P each want their own headless check, so the third copy was already on
    its way (SEAMS.md section 5).

    Dot-source it and call Find-Godot:

        . (Join-Path $PSScriptRoot 'Find-Godot.ps1')
        $Godot = Find-Godot $Godot

    Where it looks, in order:

      1. the path passed in, if any            (-Godot on the calling script)
      2. $env:GODOT                            a specific binary
      3. $env:GODOT_ROOT, else C:\Godot        searched for the mono console binary

    Throws if it finds nothing, so every check script fails the same clear way.
#>

# Searched when neither -Godot nor $env:GODOT names a binary. Override with $env:GODOT_ROOT
# rather than editing this - a machine with Godot somewhere else shouldn't need a diff.
$script:GodotSearchRoot = if ($env:GODOT_ROOT) { $env:GODOT_ROOT } else { 'C:\Godot' }

function Find-Godot {
    [CmdletBinding()]
    param(
        # A path given on the command line. Wins over everything if it exists.
        [string] $Godot
    )

    if (-not $Godot) { $Godot = $env:GODOT }

    if (-not $Godot) {
        # The console binary, not the plain one: only that variant writes to stdout on Windows.
        # Newest first, by name - v4.7.1 sorts above v4.7.0.
        $Godot = Get-ChildItem $script:GodotSearchRoot -Recurse -Filter '*mono*console.exe' -ErrorAction SilentlyContinue |
                 Sort-Object FullName -Descending |
                 Select-Object -First 1 -ExpandProperty FullName
    }

    if (-not $Godot -or -not (Test-Path $Godot)) {
        throw "Godot not found$(if ($Godot) { " at '$Godot'" } else { " under '$($script:GodotSearchRoot)'" }). " +
              "Pass -Godot <path to the mono console exe>, or set `$env:GODOT to the binary " +
              "or `$env:GODOT_ROOT to the folder to search."
    }

    return $Godot
}

<#
.SYNOPSIS
    Takes named models out of a raw asset pack and into the game.

.DESCRIPTION
    Step one of the asset pipeline (THE_BOARD.md B5): EXTRACT AND CULL. A pack holds a few hundred
    models; the game needs a handful. This copies out the ones named and the atlas they share, and
    nothing else.

    THE RAW PACK STAYS ZIPPED IN assets/, WHICH IS GITIGNORED. `game/` holds only what ships, so
    the repo never carries 200 MB of dungeon furniture nobody has used yet, and re-pulling with a
    different list is one command rather than an archaeology dig through a folder of leftovers.
    Same discipline THIRD_PARTY.md already applies to textures.

    A .gltf sits beside a .bin and an atlas .png and is useless without them, so all three come
    across together and keep their names. The next step is bake-palette.ps1 on the atlas.

.PARAMETER Pack
    The zip in assets/, by name, with or without .zip.

.PARAMETER Into
    The folder under game/models/ to put them in - "dungeon", "heroes".

.PARAMETER Models
    Model names without extension, as they are inside the pack: wall, wall_doorway, Barbarian.
    Every file whose name matches, in any of the pack's format folders that this project can
    import (.gltf, .glb and the .bin beside them), comes across.

.PARAMETER Atlas
    The shared texture to bring with them. Defaults to every .png sitting in the same folder as
    the models that were pulled, which is how both KayKit packs are laid out.

.EXAMPLE
    .\tools\pull-models.ps1 -Pack KayKit_Dungeon_Pack_1.1_FREE -Into dungeon -Models wall,wall_doorway,rubble_large
    .\tools\pull-models.ps1 -Pack KayKit_Adventurers_2.0_FREE -Into heroes -Models Barbarian
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $Pack,

    [Parameter(Mandatory = $true)]
    [string] $Into,

    [Parameter(Mandatory = $true)]
    [string[]] $Models,

    [string[]] $Atlas
)

$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.IO.Compression.FileSystem

$root = Split-Path $PSScriptRoot -Parent
$zipPath = Join-Path $root "assets\$($Pack -replace '\.zip$', '').zip"

if (-not (Test-Path $zipPath)) {
    $available = (Get-ChildItem (Join-Path $root 'assets') -Filter '*.zip' |
                  Select-Object -ExpandProperty BaseName) -join ', '
    Write-Error "No pack '$Pack' in assets/. Available: $available"
}

# NOT $into: PowerShell variables are case-insensitive, so a local called $into IS the $Into
# parameter, and the folder name is gone by the time anything prints it
$destination = Join-Path $root "game\models\$Into"
New-Item -ItemType Directory -Force -Path $destination | Out-Null

Write-Host "pack    $zipPath"
Write-Host "into    $destination"
Write-Host ""

$zip = [System.IO.Compression.ZipFile]::OpenRead($zipPath)

try {
    # gltf first, glb second: a .gltf keeps its texture as a file this pipeline can bake, and a
    # .glb may have swallowed it. only one format of each model comes across
    $wanted = @()

    foreach ($model in $Models) {
        $found = $zip.Entries | Where-Object {
            $_.FullName -match "/(gltf|glb)/" -and
            [System.IO.Path]::GetFileNameWithoutExtension($_.FullName) -eq $model -and
            $_.FullName -match '\.(gltf|glb|bin)$'
        }

        if (-not $found) {
            Write-Warning "no model '$model' in $Pack - skipped"
            continue
        }

        $wanted += $found
    }

    if (-not $wanted) { Write-Error "Nothing to pull: none of the models named are in this pack." }

    # the atlas lives beside the models it belongs to
    $folders = $wanted | ForEach-Object { Split-Path $_.FullName -Parent } | Select-Object -Unique

    $textures = $zip.Entries | Where-Object {
        $_.FullName -match '\.png$' -and
        (Split-Path $_.FullName -Parent) -in $folders -and
        ($null -eq $Atlas -or [System.IO.Path]::GetFileNameWithoutExtension($_.FullName) -in $Atlas)
    }

    foreach ($entry in @($wanted) + @($textures)) {
        $to = Join-Path $destination (Split-Path $entry.FullName -Leaf)

        [System.IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $to, $true)

        Write-Host ("  {0,-28} {1,8:n0} bytes" -f (Split-Path $to -Leaf), $entry.Length)
    }
}
finally {
    $zip.Dispose()
}

Write-Host ""
Write-Host "next    .\tools\bake-palette.ps1 -Texture game\models\$Into\<atlas>.png"
Write-Host "        then open the project once so Godot imports them"


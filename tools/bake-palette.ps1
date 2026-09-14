<#
.SYNOPSIS
    Recolours a texture to the game's palette, in place.

.DESCRIPTION
    Step two of the asset pipeline (ART_DIRECTION.md section 9.1, THE_BOARD.md B5). Every pixel is
    mapped to the nearest of the fourteen colours in palette.ps1, so a pack drawn by somebody else
    comes out wearing this game's colours.

    IT WORKS BECAUSE OF HOW THESE PACKS ARE BUILT. KayKit and Quaternius models do not have
    painted textures - they share one atlas of FLAT SWATCHES and the UVs point at them. So
    remapping the atlas recolours the entire pack at once, exactly and reversibly, and a model
    imported later is already correct. That is what makes "impose one palette" cheap enough to be
    the highest-leverage art task rather than a month of texture work.

    IN PLACE. The baked texture is what the game ships and what the .gltf imports, so there is one
    file rather than a raw one and a treated one that can drift apart. The untreated original is
    still in the zip in assets/, which is the whole reason raw packs stay zipped.

    RE-BAKE FROM THE ZIP, NOT ON TOP OF A BAKE. At -Shading 0 this is idempotent - every colour is
    already a palette colour and maps to itself. Above 0 it is not: each pass measures the source's
    shading against the palette again and flattens a little more of what is left. So changing the
    dial means pull-models.ps1 again first, which costs a second and takes the doubt out of it.

    Nearest is measured with the "redmean" approximation rather than plain RGB distance - it
    weights the channels by where the colour sits along the red axis and is far closer to what an
    eye calls similar. Plain RGB distance sends warm browns to grey often enough to matter.

    Transparent pixels are left alone: an atlas's alpha is cutout information, not colour.

.PARAMETER Texture
    The .png to recolour, rewritten in place - or a FOLDER, in which case every .png in it is
    baked. The folder form is the one to use: importing a .glb makes Godot extract the texture it
    had swallowed and drop it beside the model, unbaked, and baking the folder catches that copy
    too. Re-importing the model regenerates it raw again, so bake the folder after any re-import.

.PARAMETER Shading
    How much of the source texture's OWN light and dark survives the remap, 0 to 1. Default 0.3.

    A pack's atlas is not quite flat: a swatch often carries a soft ramp that fakes form, and
    mapping every pixel to one palette colour throws all of it away. Strictly that is correct -
    the painted-miniature shader does the lighting, and baked-in shading fights banded lighting -
    but taken all the way to 0 the models lose the modelling their author drew into them.

    So the nearest palette colour is scaled by how light or dark the source pixel was relative to
    that colour, by this much. 0 is the strict reading, one flat colour per swatch. 1 keeps the
    pack's shading entirely and only shifts its hue. THIS IS ONE OF THE TWO DIALS IN THIS PHASE
    THAT HAS TO BE JUDGED BY EYE - re-baking is one command, so turn it and look.

.PARAMETER WhatIf
    Report what would change - how many distinct colours, and which palette entries they land on -
    without writing anything.

.EXAMPLE
    .\tools\bake-palette.ps1 -Texture game\models\dungeon\dungeon_texture.png
    .\tools\bake-palette.ps1 -Texture game\models\heroes\barbarian_texture.png -WhatIf
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $Texture,

    [ValidateRange(0.0, 1.0)]
    [double] $Shading = 0.3,

    [switch] $WhatIf
)

$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'palette.ps1')

Add-Type -AssemblyName System.Drawing

$target = (Resolve-Path $Texture).Path
$palette = @(Get-PaletteColours)

if (Test-Path $target -PathType Container) {
    $pngs = @(Get-ChildItem $target -Filter '*.png' -File)

    if (-not $pngs) { Write-Error "No .png in $target." }

    foreach ($png in $pngs) {
        & $PSCommandPath -Texture $png.FullName -Shading $Shading -WhatIf:$WhatIf
        Write-Host ""
    }

    exit 0
}

$path = $target

Write-Host "texture $path"
Write-Host "palette $($palette.Count) colours, keeping $($Shading * 100)% of the source's own shading"

$bitmap = [System.Drawing.Bitmap]::FromFile($path)
$width = $bitmap.Width
$height = $bitmap.Height

# a copy, because FromFile keeps the file locked until the bitmap is disposed and the copy is
# what gets written back over it
$image = New-Object System.Drawing.Bitmap $bitmap
$bitmap.Dispose()

$rect = New-Object System.Drawing.Rectangle 0, 0, $width, $height
$data = $image.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadWrite,
                        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)

$bytes = New-Object byte[] ($data.Stride * $height)
[System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $bytes, 0, $bytes.Length)

# one lookup per DISTINCT colour, not per pixel. an atlas of flat swatches has a few hundred
# colours in a million pixels, so this is the difference between a second and several minutes
$seen = @{}
$landed = @{}
$changed = 0

for ($i = 0; $i -lt $bytes.Length; $i += 4) {
    # BGRA on little-endian Windows, which is what Format32bppArgb actually lays out
    $b = $bytes[$i]; $g = $bytes[$i + 1]; $r = $bytes[$i + 2]; $a = $bytes[$i + 3]

    if ($a -eq 0) { continue }

    $key = ($r -shl 16) -bor ($g -shl 8) -bor $b

    if (-not $seen.ContainsKey($key)) {
        $best = $null
        $nearest = [double]::MaxValue

        foreach ($p in $palette) {
            # redmean: the weights shift with how red the pair is, which is what makes it agree
            # with an eye better than a plain sum of squares
            $mean = ($r + $p.R) / 2.0
            $dr = $r - $p.R; $dg = $g - $p.G; $db = $b - $p.B

            $distance = (2 + $mean / 256.0) * $dr * $dr +
                        4 * $dg * $dg +
                        (2 + (255 - $mean) / 256.0) * $db * $db

            if ($distance -lt $nearest) { $nearest = $distance; $best = $p }
        }

        # keep some of the source pixel's own light and dark, as a scale on the palette colour -
        # Rec. 601 luma, which is close enough for a knob nobody reads in numbers
        $sourceLuma = 0.299 * $r + 0.587 * $g + 0.114 * $b
        $paletteLuma = 0.299 * $best.R + 0.587 * $best.G + 0.114 * $best.B

        $scale = if ($paletteLuma -le 1) { 1.0 } else { 1.0 + $Shading * ($sourceLuma / $paletteLuma - 1.0) }

        $seen[$key] = [pscustomobject]@{
            Name = $best.Name
            R    = [Math]::Min(255, [Math]::Max(0, [int][Math]::Round($best.R * $scale)))
            G    = [Math]::Min(255, [Math]::Max(0, [int][Math]::Round($best.G * $scale)))
            B    = [Math]::Min(255, [Math]::Max(0, [int][Math]::Round($best.B * $scale)))
        }

        $landed[$best.Name] = 1 + ($landed[$best.Name])
    }

    $to = $seen[$key]

    if ($to.R -ne $r -or $to.G -ne $g -or $to.B -ne $b) { $changed++ }

    $bytes[$i] = [byte]$to.B
    $bytes[$i + 1] = [byte]$to.G
    $bytes[$i + 2] = [byte]$to.R
}

Write-Host ""
Write-Host "colours $($seen.Count) distinct in the source"
Write-Host "pixels  $changed of $($width * $height) recoloured"
Write-Host ""

foreach ($name in ($landed.Keys | Sort-Object)) {
    Write-Host ("  {0,-12} {1} of the source's colours" -f $name, $landed[$name])
}

if ($WhatIf) {
    $image.UnlockBits($data)
    $image.Dispose()
    Write-Host ""
    Write-Host "nothing written - -WhatIf"
    exit 0
}

[System.Runtime.InteropServices.Marshal]::Copy($bytes, 0, $data.Scan0, $bytes.Length)
$image.UnlockBits($data)
$image.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
$image.Dispose()

Write-Host ""
Write-Host "baked   $path"

<#
THE PALETTE. Fourteen colours, and every asset in the game is recoloured to them.

ART_DIRECTION.md section 9.1 calls this the highest-leverage art task in the project, and the
reason is arithmetic rather than taste: thousands of developers ship these same CC0 packs, and a
game that imposes one palette on nine mixed sources stops looking like nine mixed sources. It is
cheap, it is nearly automatic (see bake-palette.ps1), and it does about 80% of the work of making
the game look like one game.

THIS FILE IS THE ONE STATEMENT OF IT. Dot-source it; do not copy the numbers anywhere. They are
not in a .tres, not in a shader and not in a doc - ART_DIRECTION.md points here instead, because a
palette written down twice is a palette that drifts, and every asset in the game would have to be
re-baked to find out.

HOW IT WAS CHOSEN. The table already exists and the palette has to live on it: an oak tray, green
felt, a sepia parchment battle map, onyx dice. So: warm, low saturation, nothing that fights the
wood. Four steps of stone because the dungeon is made of it and a ramp reads as form under flat
lighting; three woods and leathers because every prop on a table is one of them; two metals; three
accents kept muted so a banner or a cloak draws the eye without shouting; one skin.

Nothing here is pure black or pure white. Painted miniatures never are - the darkest thing on one
is a liner wash and the brightest is a drybrushed edge, and both carry the colour of the paint
under them.
#>

$script:Palette = [ordered]@{
    # liner and deep shadow - where the wash pools. violet-black, never neutral black
    ink         = '#16131A'

    # the stone ramp: the dungeon, and anything carved
    stone_dark  = '#3B3A42'
    stone       = '#6B6A70'
    stone_light = '#9D9B96'

    # parchment, bone, chalk - the lightest thing on the table, and the map's own colour
    bone        = '#D8CFB8'

    # wood and leather, the other half of everything a table is made of
    wood_dark   = '#3A2A1E'
    wood        = '#6B4A2F'
    leather     = '#8C6239'

    # metals. iron is cool against the stone, brass is the only warm shine
    iron        = '#55585E'
    brass       = '#B08D42'

    # accents, deliberately muted - a banner should draw the eye, not win the room
    blood       = '#7D2F2B'
    moss        = '#4A5D36'
    slate_blue  = '#3F4F63'

    # skin
    flesh       = '#C2906A'
}

function Get-PaletteColours {
    $script:Palette.GetEnumerator() | ForEach-Object {
        $hex = $_.Value.TrimStart('#')

        [pscustomobject]@{
            Name = $_.Key
            R    = [Convert]::ToInt32($hex.Substring(0, 2), 16)
            G    = [Convert]::ToInt32($hex.Substring(2, 2), 16)
            B    = [Convert]::ToInt32($hex.Substring(4, 2), 16)
        }
    }
}

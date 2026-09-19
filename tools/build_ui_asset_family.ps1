param(
    [Parameter(Mandatory = $true)]
    [string]$ReferenceDirectory,
    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$colors = @('gold', 'crimson', 'violet', 'blue', 'green', 'silver')
$families = @(
    @{ Prefix='button_plate'; Ratio='3x1'; Width=1536; Height=512; SourcePrefix='button_plate'; SourceRatio='3x1' },
    @{ Prefix='button_plate'; Ratio='5x1'; Width=1530; Height=306; SourcePrefix='button_plate'; SourceRatio='5x1' },
    @{ Prefix='row_plate'; Ratio='6x1'; Width=1536; Height=256; SourcePrefix='row_plate'; SourceRatio='6x1' },
    @{ Prefix='container'; Ratio='3x4'; Width=768; Height=1024; SourcePrefix='container'; SourceRatio='3x4' },
    @{ Prefix='container'; Ratio='9x16'; Width=576; Height=1024; SourcePrefix='container'; SourceRatio='9x16' },
    @{ Prefix='container'; Ratio='3x2'; Width=1536; Height=1024; SourcePrefix='container'; SourceRatio='3x2' },
    @{ Prefix='container'; Ratio='2x1'; Width=1536; Height=768; SourcePrefix='container'; SourceRatio='2x1' },
    # No approved 5:1 container existed. Expand the approved 2:1 center only.
    @{ Prefix='container'; Ratio='5x1'; Width=1530; Height=306; SourcePrefix='container'; SourceRatio='2x1' },
    @{ Prefix='banner_flag'; Ratio='3x4'; Width=768; Height=1024; SourcePrefix='banner_flag'; SourceRatio='3x4' },
    @{ Prefix='banner_flag'; Ratio='9x16'; Width=576; Height=1024; SourcePrefix='banner_flag'; SourceRatio='9x16' }
)

function Draw-Slice(
    [System.Drawing.Graphics]$Graphics,
    [System.Drawing.Bitmap]$Source,
    [System.Drawing.Rectangle]$DestinationRectangle,
    [System.Drawing.Rectangle]$SourceRectangle
) {
    if ($DestinationRectangle.Width -le 0 -or $DestinationRectangle.Height -le 0) { return }
    $Graphics.DrawImage(
        $Source, $DestinationRectangle,
        $SourceRectangle.X, $SourceRectangle.Y,
        $SourceRectangle.Width, $SourceRectangle.Height,
        [System.Drawing.GraphicsUnit]::Pixel)
}

function Resize-NineSlice(
    [System.Drawing.Bitmap]$Source,
    [int]$Width,
    [int]$Height,
    [string]$Destination
) {
    $bitmap = [System.Drawing.Bitmap]::new($Width, $Height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
        $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality

        $sourceShort = [Math]::Min($Source.Width, $Source.Height)
        $targetShort = [Math]::Min($Width, $Height)
        $sourceMargin = [Math]::Max(8, [int][Math]::Round($sourceShort * 0.12))
        $targetMargin = [Math]::Max(12, [int][Math]::Round($targetShort * 0.12))
        $sx = @(0, $sourceMargin, ($Source.Width - $sourceMargin), $Source.Width)
        $sy = @(0, $sourceMargin, ($Source.Height - $sourceMargin), $Source.Height)
        $dx = @(0, $targetMargin, ($Width - $targetMargin), $Width)
        $dy = @(0, $targetMargin, ($Height - $targetMargin), $Height)

        for ($row = 0; $row -lt 3; $row++) {
            for ($column = 0; $column -lt 3; $column++) {
                $sourceRect = [System.Drawing.Rectangle]::new(
                    $sx[$column], $sy[$row],
                    $sx[$column + 1] - $sx[$column],
                    $sy[$row + 1] - $sy[$row])
                $destinationRect = [System.Drawing.Rectangle]::new(
                    $dx[$column], $dy[$row],
                    $dx[$column + 1] - $dx[$column],
                    $dy[$row + 1] - $dy[$row])
                Draw-Slice $graphics $Source $destinationRect $sourceRect
            }
        }
    } finally {
        $graphics.Dispose()
    }

    try {
        $bitmap.Save($Destination, [System.Drawing.Imaging.ImageFormat]::Png)
    } finally {
        $bitmap.Dispose()
    }
}

$references = (Resolve-Path -LiteralPath $ReferenceDirectory).Path
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$output = (Resolve-Path -LiteralPath $OutputDirectory).Path

foreach ($family in $families) {
    foreach ($color in $colors) {
        $sourceName = '{0}_{1}_{2}.png' -f $family.SourcePrefix, $color, $family.SourceRatio
        $sourcePath = Join-Path $references $sourceName
        if (-not (Test-Path -LiteralPath $sourcePath)) { throw "Missing approved reference: $sourcePath" }

        $destinationName = '{0}_{1}_{2}.png' -f $family.Prefix, $color, $family.Ratio
        $destinationPath = Join-Path $output $destinationName
        $source = [System.Drawing.Bitmap]::FromFile($sourcePath)
        try {
            Resize-NineSlice $source $family.Width $family.Height $destinationPath
        } finally {
            $source.Dispose()
        }

        if ($family.Prefix -eq 'button_plate' -and $family.Ratio -eq '3x1') {
            Copy-Item -LiteralPath $destinationPath -Destination (Join-Path $output ('button_plate_{0}.png' -f $color)) -Force
        }
    }
}

Write-Output "Rebuilt 60 exact-ratio assets and 6 legacy button paths from approved committed pixels."

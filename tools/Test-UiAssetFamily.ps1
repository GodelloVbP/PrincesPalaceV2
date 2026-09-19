param(
    [string]$AssetDirectory = 'Assets/_Project/Art/UI/Buttons/Processed'
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$colors = @('gold', 'crimson', 'violet', 'blue', 'green', 'silver')
$families = @(
    @{ Prefix = 'button_plate'; Ratio = '3x1'; Width = 1536; Height = 512 },
    @{ Prefix = 'button_plate'; Ratio = '5x1'; Width = 1530; Height = 306 },
    @{ Prefix = 'row_plate'; Ratio = '6x1'; Width = 1536; Height = 256 },
    @{ Prefix = 'container'; Ratio = '3x4'; Width = 768; Height = 1024 },
    @{ Prefix = 'container'; Ratio = '9x16'; Width = 576; Height = 1024 },
    @{ Prefix = 'container'; Ratio = '3x2'; Width = 1536; Height = 1024 },
    @{ Prefix = 'container'; Ratio = '2x1'; Width = 1536; Height = 768 },
    @{ Prefix = 'container'; Ratio = '5x1'; Width = 1530; Height = 306 },
    @{ Prefix = 'banner_flag'; Ratio = '3x4'; Width = 768; Height = 1024 },
    @{ Prefix = 'banner_flag'; Ratio = '9x16'; Width = 576; Height = 1024 }
)

$failures = [System.Collections.Generic.List[string]]::new()

foreach ($family in $families) {
    foreach ($color in $colors) {
        $name = '{0}_{1}_{2}.png' -f $family.Prefix, $color, $family.Ratio
        $path = Join-Path $AssetDirectory $name
        if (-not (Test-Path -LiteralPath $path)) {
            $failures.Add("missing: $name")
            continue
        }

        $bitmap = [System.Drawing.Bitmap]::FromFile((Resolve-Path -LiteralPath $path))
        try {
            if ($bitmap.Width -ne $family.Width -or $bitmap.Height -ne $family.Height) {
                $failures.Add("wrong size: $name is $($bitmap.Width)x$($bitmap.Height)")
            }

            $maxX = $bitmap.Width - 1
            $maxY = $bitmap.Height - 1
            foreach ($point in @(@(0, 0), @($maxX, 0), @(0, $maxY), @($maxX, $maxY))) {
                if ($bitmap.GetPixel($point[0], $point[1]).A -ne 0) {
                    $failures.Add("opaque canvas corner: $name")
                    break
                }
            }

            $samples = 0
            $red = 0.0
            $green = 0.0
            $blue = 0.0
            for ($iy = 4; $iy -le 12; $iy++) {
                for ($ix = 4; $ix -le 12; $ix++) {
                    $x = [int]($bitmap.Width * $ix / 16)
                    $y = [int]($bitmap.Height * $iy / 16)
                    $pixel = $bitmap.GetPixel($x, $y)
                    if ($pixel.A -gt 240) {
                        $red += $pixel.R
                        $green += $pixel.G
                        $blue += $pixel.B
                        $samples++
                    }
                }
            }
            if ($samples -eq 0) {
                $failures.Add("no opaque interior samples: $name")
            } else {
                $meanR = $red / $samples
                $meanG = $green / $samples
                $meanB = $blue / $samples
                $luma = 0.2126 * $meanR + 0.7152 * $meanG + 0.0722 * $meanB
                $spread = [Math]::Max($meanR, [Math]::Max($meanG, $meanB)) - [Math]::Min($meanR, [Math]::Min($meanG, $meanB))
                # Approved source families vary slightly; their sampled means top out near 22.
                # Leave a small resampling tolerance while rejecting the 32-50 slate interiors.
                if ($luma -gt 25) { $failures.Add(('interior too bright: {0} luma={1:N1}' -f $name, $luma)) }
                if ($spread -gt 18) { $failures.Add(('interior color cast: {0} spread={1:N1}' -f $name, $spread)) }
            }
        } finally {
            $bitmap.Dispose()
        }
    }
}

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Error $_ }
    throw "UI asset verification failed with $($failures.Count) issue(s)."
}

Write-Output "Verified $($families.Count * $colors.Count) UI assets: dimensions, alpha corners, and quiet neutral-black interiors."

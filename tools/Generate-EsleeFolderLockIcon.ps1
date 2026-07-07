param(
    [string]$SourcePath = "",
    [string]$OutputDirectory = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$projectRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")).Path
if ([string]::IsNullOrWhiteSpace($SourcePath)) {
    $SourcePath = Join-Path $projectRoot "12cc6217-3fb5-4bf2-8821-ea6b53083df4.png"
}

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $projectRoot "assets\icons"
}

if (-not (Test-Path -LiteralPath $SourcePath)) {
    throw "Source image not found: $SourcePath"
}

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

$sourceCopy = Join-Path $OutputDirectory "EsleeFolderLock-source.png"
Copy-Item -LiteralPath $SourcePath -Destination $sourceCopy -Force

Add-Type -AssemblyName System.Drawing

$sizes = @(16, 24, 32, 48, 64, 128, 256)
$pngFrames = New-Object System.Collections.Generic.List[object]

$sourceImage = [System.Drawing.Image]::FromFile($sourceCopy)
try {
    $baseSide = [Math]::Min($sourceImage.Width, $sourceImage.Height)
    $cropSide = [int][Math]::Round($baseSide * 0.72)
    if ($cropSide -lt 1) {
        throw "Invalid crop size for source image."
    }

    $cropX = [int][Math]::Round(($sourceImage.Width - $cropSide) / 2)
    $remainingY = $sourceImage.Height - $cropSide
    $cropY = [int][Math]::Round($remainingY * 0.12)
    if ($cropX -lt 0) { $cropX = 0 }
    if ($cropY -lt 0) { $cropY = 0 }
    if ($cropX + $cropSide -gt $sourceImage.Width) { $cropX = $sourceImage.Width - $cropSide }
    if ($cropY + $cropSide -gt $sourceImage.Height) { $cropY = $sourceImage.Height - $cropSide }

    $cropRect = New-Object System.Drawing.Rectangle($cropX, $cropY, $cropSide, $cropSide)

    foreach ($size in $sizes) {
        $bitmap = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        try {
            $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
            try {
                $graphics.Clear([System.Drawing.Color]::FromArgb(255, 14, 22, 30))
                $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceOver
                $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
                $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
                $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality

                $destRect = New-Object System.Drawing.Rectangle(0, 0, $size, $size)
                $graphics.DrawImage($sourceImage, $destRect, $cropRect, [System.Drawing.GraphicsUnit]::Pixel)
            }
            finally {
                $graphics.Dispose()
            }

            $pngPath = Join-Path $OutputDirectory "EsleeFolderLock-$size.png"
            $bitmap.Save($pngPath, [System.Drawing.Imaging.ImageFormat]::Png)
            $pngFrames.Add([pscustomobject]@{
                Size = $size
                Path = $pngPath
                Bytes = [System.IO.File]::ReadAllBytes($pngPath)
            })
        }
        finally {
            $bitmap.Dispose()
        }
    }
}
finally {
    $sourceImage.Dispose()
}

$icoPath = Join-Path $OutputDirectory "EsleeFolderLock.ico"
$stream = [System.IO.File]::Create($icoPath)
try {
    $writer = New-Object System.IO.BinaryWriter($stream)
    try {
        $writer.Write([UInt16]0)
        $writer.Write([UInt16]1)
        $writer.Write([UInt16]$pngFrames.Count)

        $offset = 6 + (16 * $pngFrames.Count)
        foreach ($frame in $pngFrames) {
            $sizeByte = [byte]0
            if ($frame.Size -ne 256) {
                $sizeByte = [byte]$frame.Size
            }

            $writer.Write($sizeByte)
            $writer.Write($sizeByte)
            $writer.Write([byte]0)
            $writer.Write([byte]0)
            $writer.Write([UInt16]1)
            $writer.Write([UInt16]32)
            $writer.Write([UInt32]$frame.Bytes.Length)
            $writer.Write([UInt32]$offset)
            $offset += $frame.Bytes.Length
        }

        foreach ($frame in $pngFrames) {
            $writer.Write([byte[]]$frame.Bytes)
        }
    }
    finally {
        $writer.Dispose()
    }
}
finally {
    $stream.Dispose()
}

Write-Host "Generated icon assets in $OutputDirectory"
Write-Host "Source copy: $sourceCopy"
Write-Host "ICO: $icoPath"

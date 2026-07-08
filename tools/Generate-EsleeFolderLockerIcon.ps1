param(
    [string]$SourcePath = "",
    [string]$OutputDirectory = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$projectRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")).Path
if ([string]::IsNullOrWhiteSpace($SourcePath)) {
    $SourcePath = Join-Path $projectRoot "eslee-folder-lock.png"
}

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $projectRoot "assets\icons"
}

if (-not (Test-Path -LiteralPath $SourcePath)) {
    throw "Source image not found: $SourcePath"
}

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

$sourceCopy = Join-Path $OutputDirectory "eslee-folder-locker-source.png"
Copy-Item -LiteralPath $SourcePath -Destination $sourceCopy -Force

Add-Type -AssemblyName System.Drawing

$sizes = @(16, 24, 32, 48, 64, 128, 256)
$pngFrames = New-Object System.Collections.Generic.List[object]

$sourceImage = [System.Drawing.Image]::FromFile($sourceCopy)
try {
    foreach ($size in $sizes) {
        $bitmap = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        try {
            $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
            try {
                $graphics.Clear([System.Drawing.Color]::Transparent)
                $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceOver
                $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
                $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
                $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality

                $sourceRatio = $sourceImage.Width / $sourceImage.Height
                $targetRatio = 1.0
                if ($sourceRatio -gt $targetRatio) {
                    $drawWidth = $size
                    $drawHeight = [int][Math]::Round($size / $sourceRatio)
                    $drawX = 0
                    $drawY = [int][Math]::Round(($size - $drawHeight) / 2)
                }
                else {
                    $drawHeight = $size
                    $drawWidth = [int][Math]::Round($size * $sourceRatio)
                    $drawX = [int][Math]::Round(($size - $drawWidth) / 2)
                    $drawY = 0
                }

                $destRect = New-Object System.Drawing.Rectangle($drawX, $drawY, $drawWidth, $drawHeight)
                $graphics.DrawImage($sourceImage, $destRect)
            }
            finally {
                $graphics.Dispose()
            }

            $pngPath = Join-Path $OutputDirectory "eslee-folder-locker-$size.png"
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

$icoPath = Join-Path $OutputDirectory "eslee-folder-locker.ico"
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

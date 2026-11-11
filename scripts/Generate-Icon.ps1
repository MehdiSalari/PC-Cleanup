$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$sourcePath = Join-Path $PSScriptRoot '..\assets\app-icon-source.png'
$outputPath = Join-Path $PSScriptRoot '..\assets\app-icon.ico'
$source = [System.Drawing.Bitmap]::FromFile([IO.Path]::GetFullPath($sourcePath))
$sizes = @(16, 24, 32, 48, 64, 128, 256)
$images = @()
try {
    foreach ($size in $sizes) {
        $bitmap = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        try {
            $graphics.Clear([System.Drawing.Color]::Transparent)
            $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
            $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
            $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $graphics.DrawImage($source, 0, 0, $size, $size)
        }
        finally { $graphics.Dispose() }
        $images += ,@{ Size = $size; Bitmap = $bitmap }
    }

    $stream = New-Object IO.MemoryStream
    $writer = New-Object IO.BinaryWriter($stream)
    try {
        $writer.Write([UInt16]0)
        $writer.Write([UInt16]1)
        $writer.Write([UInt16]$images.Count)
        $offset = 6 + (16 * $images.Count)
        foreach ($entry in $images) {
            $size = [int]$entry.Size
            $imageBytes = 40 + ($size * $size * 4) + (((($size + 31) -band -32) / 8) * $size)
            $writer.Write([byte]$(if ($size -eq 256) { 0 } else { $size }))
            $writer.Write([byte]$(if ($size -eq 256) { 0 } else { $size }))
            $writer.Write([byte]0)
            $writer.Write([byte]0)
            $writer.Write([UInt16]1)
            $writer.Write([UInt16]32)
            $writer.Write([UInt32]$imageBytes)
            $writer.Write([UInt32]$offset)
            $offset += $imageBytes
        }
        foreach ($entry in $images) {
            $bitmap = $entry.Bitmap
            $size = [int]$entry.Size
            $writer.Write([UInt32]40)
            $writer.Write([int]$size)
            $writer.Write([int]($size * 2))
            $writer.Write([UInt16]1)
            $writer.Write([UInt16]32)
            $writer.Write([UInt32]0)
            $writer.Write([UInt32]($size * $size * 4))
            $writer.Write([int]0); $writer.Write([int]0)
            $writer.Write([UInt32]0); $writer.Write([UInt32]0)
            for ($y = $size - 1; $y -ge 0; $y--) {
                for ($x = 0; $x -lt $size; $x++) {
                    $pixel = $bitmap.GetPixel($x, $y)
                    $writer.Write([byte]$pixel.B)
                    $writer.Write([byte]$pixel.G)
                    $writer.Write([byte]$pixel.R)
                    $writer.Write([byte]$pixel.A)
                }
            }
            $maskRowBytes = [int]([Math]::Ceiling($size / 32.0) * 4)
            $writer.Write((New-Object byte[] ($maskRowBytes * $size)))
        }
        $writer.Flush()
        [IO.File]::WriteAllBytes([IO.Path]::GetFullPath($outputPath), $stream.ToArray())
    }
    finally { $writer.Dispose(); $stream.Dispose() }
}
finally {
    foreach ($entry in $images) { $entry.Bitmap.Dispose() }
    $source.Dispose()
}
Write-Host "Created $outputPath with sizes: $($sizes -join ', ') px"

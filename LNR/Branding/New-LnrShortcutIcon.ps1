# Generates a Windows multi-resolution ICO from the very same LNR logo
# embedded in the About window. Build-time only; no executable launcher.
param(
    [Parameter(Mandatory=$true)][string]$SourceLogo,
    [Parameter(Mandatory=$true)][string]$OutputIcon
)
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing
if (-not (Test-Path -LiteralPath $SourceLogo -PathType Leaf)) {
    throw "LNR logo not found: $SourceLogo"
}
$logo = [System.Drawing.Image]::FromFile((Resolve-Path -LiteralPath $SourceLogo).Path)
try {
    $frames = [System.Collections.Generic.List[object]]::new()
    foreach ($size in @(16,24,32,48,64,128,256)) {
        $bitmap = [System.Drawing.Bitmap]::new($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        try {
            $g = [System.Drawing.Graphics]::FromImage($bitmap)
            try {
                $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
                $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
                $g.Clear([System.Drawing.Color]::Transparent)
                $bg = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255,21,36,56))
                $fg = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255,239,246,255))
                try {
                    # Square badge suited to the Windows desktop, keeping the LNR identity legible.
                    $g.FillRectangle($bg, 0, 0, $size, $size)
                    $font = [System.Drawing.Font]::new("Segoe UI", [float]($size * 0.36), [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
                    try {
                        $format = [System.Drawing.StringFormat]::new()
                        try {
                            $format.Alignment = [System.Drawing.StringAlignment]::Center
                            $format.LineAlignment = [System.Drawing.StringAlignment]::Center
                            $g.DrawString("LNR", $font, $fg,
                                [System.Drawing.RectangleF]::new(0, [float]($size * 0.05), [float]$size, [float]($size * 0.64)),
                                $format)
                        } finally { $format.Dispose() }
                    } finally { $font.Dispose() }
                    # The exact logo from the About dialog anchors the lower part of the badge.
                    if ($size -ge 32) {
                        $pad = [float]($size * 0.06)
                        $availableW = [float]($size - 2 * $pad)
                        $availableH = [float]($size * 0.24)
                        $scale = [Math]::Min($availableW / $logo.Width, $availableH / $logo.Height)
                        $drawW = [float]($logo.Width * $scale)
                        $drawH = [float]($logo.Height * $scale)
                        $x = [float](($size - $drawW)/2)
                        $y = [float]($size * 0.70 + ($availableH - $drawH)/2)
                        $g.DrawImage($logo, $x, $y, $drawW, $drawH)
                    }
                } finally { $fg.Dispose(); $bg.Dispose() }
            } finally { $g.Dispose() }
            $ms = [System.IO.MemoryStream]::new()
            try {
                $bitmap.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
                $frames.Add([pscustomobject]@{Size=$size; Bytes=$ms.ToArray()})
            } finally { $ms.Dispose() }
        } finally { $bitmap.Dispose() }
    }

    $dest = [System.IO.Path]::GetFullPath($OutputIcon)
    $parent = [System.IO.Path]::GetDirectoryName($dest)
    [System.IO.Directory]::CreateDirectory($parent) | Out-Null
    $stream = [System.IO.File]::Create($dest)
    try {
        $writer = [System.IO.BinaryWriter]::new($stream)
        try {
            $writer.Write([uint16]0)  # Reserved
            $writer.Write([uint16]1)  # ICO
            $writer.Write([uint16]$frames.Count)
            $offset = 6 + 16 * $frames.Count
            foreach ($frame in $frames) {
                $writer.Write([byte]($frame.Size % 256)) # 0 means 256
                $writer.Write([byte]($frame.Size % 256))
                $writer.Write([byte]0)
                $writer.Write([byte]0)
                $writer.Write([uint16]1)
                $writer.Write([uint16]32)
                $writer.Write([uint32]$frame.Bytes.Length)
                $writer.Write([uint32]$offset)
                $offset += $frame.Bytes.Length
            }
            foreach ($frame in $frames) { $writer.Write([byte[]]$frame.Bytes) }
            $writer.Flush()
        } finally { $writer.Dispose() }
    } finally { $stream.Dispose() }
    if ((Get-Item -LiteralPath $dest).Length -lt 1024) { throw "Generated icon is unexpectedly small" }
    Write-Host "LNR shortcut icon created from About-dialog logo: $dest"
} finally { $logo.Dispose() }

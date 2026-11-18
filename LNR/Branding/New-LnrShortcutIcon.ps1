# Convert the approved square LNR MOD icon artwork to a Windows multi-size ICO.
# Build-time only. The source image is retained in this fork's LNR branding folder.
param(
  [Parameter(Mandatory=$true)][string]$SourceImage,
  [Parameter(Mandatory=$true)][string]$OutputIcon
)
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing
if (-not (Test-Path -LiteralPath $SourceImage -PathType Leaf)) { throw "LNR artwork missing: $SourceImage" }
$source = [System.Drawing.Image]::FromFile((Resolve-Path -LiteralPath $SourceImage).Path)
try {
  if ($source.Width -ne $source.Height) { throw "LNR icon artwork must be square" }
  $frames = @()
  foreach ($size in @(16,24,32,48,64,128,256)) {
    $bitmap = [System.Drawing.Bitmap]::new($size,$size,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try {
      $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
      try {
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $graphics.DrawImage($source,0,0,$size,$size)
      } finally { $graphics.Dispose() }
      $buffer = [System.IO.MemoryStream]::new()
      try {
        $bitmap.Save($buffer,[System.Drawing.Imaging.ImageFormat]::Png)
        $frames += [pscustomobject]@{Size=$size;Data=$buffer.ToArray()}
      } finally { $buffer.Dispose() }
    } finally { $bitmap.Dispose() }
  }
  $destination = [System.IO.Path]::GetFullPath($OutputIcon)
  [System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($destination)) | Out-Null
  $output = [System.IO.File]::Create($destination)
  try {
    $writer = [System.IO.BinaryWriter]::new($output)
    try {
      $writer.Write([uint16]0)
      $writer.Write([uint16]1)
      $writer.Write([uint16]$frames.Count)
      $offset = 6+16*$frames.Count
      foreach ($frame in $frames) {
        $writer.Write([byte]($frame.Size % 256))
        $writer.Write([byte]($frame.Size % 256))
        $writer.Write([byte]0)
        $writer.Write([byte]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]32)
        $writer.Write([uint32]$frame.Data.Length)
        $writer.Write([uint32]$offset)
        $offset += $frame.Data.Length
      }
      foreach($frame in $frames) { $writer.Write([byte[]]$frame.Data) }
      $writer.Flush()
    } finally { $writer.Dispose() }
  } finally { $output.Dispose() }
  if ((Get-Item -LiteralPath $destination).Length -lt 1024) { throw "Generated icon is unexpectedly small" }
  Write-Host "Created LNR MOD icon in Resources: $destination"
} finally { $source.Dispose() }

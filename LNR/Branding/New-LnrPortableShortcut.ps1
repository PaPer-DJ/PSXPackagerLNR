# LNR portable shortcut - CONFIRMED on the user's Windows after beta.28.
# Reproduce the successful isolated candidate exactly: use Windows' native
# WScript.Shell.CreateShortcut with an absolute target at BUILD time, an EMPTY
# WorkingDirectory, --lnr-batch, and the standard system icon.
# Do not revert to IShellLink.SetRelativePath or a hand-written binary .lnk.
# This helper runs in GitHub Actions only; no script is shipped to end users.
param(
  [Parameter(Mandatory=$true)][string]$GuiDirectory,
  [Parameter(Mandatory=$true)][string]$ShortcutPath,
  [switch]$VerifyRelocation
)
$ErrorActionPreference = 'Stop'
$guiDir = (Resolve-Path -LiteralPath $GuiDirectory).Path
$exePath = Join-Path $guiDir 'PSXPackagerGUI.exe'
$shortcutPath = [IO.Path]::GetFullPath($ShortcutPath)
if (-not (Test-Path -LiteralPath $exePath -PathType Leaf)) {
  throw "GUI executable missing: $exePath"
}
if (-not [string]::Equals(
    [IO.Path]::GetDirectoryName($shortcutPath),
    $guiDir,
    [StringComparison]::OrdinalIgnoreCase)) {
  throw 'The portable shortcut MUST be beside PSXPackagerGUI.exe'
}

# DO NOT TOUCH: these six assignments are the Windows-tested candidate.
$wsh = New-Object -ComObject WScript.Shell
$shortcut = $wsh.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $exePath
$shortcut.Arguments = '--lnr-batch'
$shortcut.WorkingDirectory = ''
$shortcut.Description = 'PSXPackagerLNR - Conversion por lotes'
$shortcut.IconLocation = "$env:WINDIR\System32\shell32.dll,179"
$shortcut.Save()

if (-not (Test-Path -LiteralPath $shortcutPath -PathType Leaf)) {
  throw 'LNR .lnk was not created'
}
$created = $wsh.CreateShortcut($shortcutPath)
if ($created.Arguments -ne '--lnr-batch') {
  throw 'LNR .lnk did not retain --lnr-batch'
}
if ($created.WorkingDirectory -ne '') {
  throw 'LNR .lnk Start in must remain completely empty'
}
Write-Host "Generated approved LNR shortcut: $shortcutPath"

if ($VerifyRelocation) {
  # The build runs on D:; simulate a user extracting on C:. This must be
  # a real ShellExecute of the UNMODIFIED copied .lnk, not a COM Resolve()
  # or SetRelativePath() call, which previously concealed broken shortcuts.
  $testBase = [Environment]::GetFolderPath(
      [Environment+SpecialFolder]::LocalApplicationData)
  if ([IO.Path]::GetPathRoot($testBase) -eq [IO.Path]::GetPathRoot($guiDir)) {
    throw 'Portability test needs another drive for extraction'
  }
  $dest = Join-Path $testBase ('lnr-shortcut-tested-' + [guid]::NewGuid().ToString('N'))
  New-Item -ItemType Directory -Path $dest -Force | Out-Null
  $hidden = "$exePath.hidden-for-portability-test"
  $process = $null
  $movedOriginal = $false
  try {
    Copy-Item -Path (Join-Path $guiDir '*') -Destination $dest -Recurse -Force
    $relocated = Join-Path $dest ([IO.Path]::GetFileName($shortcutPath))
    $expectedExe = Join-Path $dest 'PSXPackagerGUI.exe'
    if (-not (Test-Path -LiteralPath $expectedExe -PathType Leaf) -or
        -not (Test-Path -LiteralPath $relocated -PathType Leaf)) {
      throw 'Relocation test does not contain the real GUI and shortcut'
    }
    Move-Item -LiteralPath $exePath -Destination $hidden
    $movedOriginal = $true
    Write-Host "Testing REAL shortcut activation: $guiDir => $dest"
    # Direct Process.Start on the .lnk is equivalent to opening it in Explorer.
    $start = [Diagnostics.ProcessStartInfo]::new($relocated)
    $start.UseShellExecute = $true
    $process = [Diagnostics.Process]::Start($start)
    if ($null -eq $process) {
      throw 'ShellExecute failed to start PSXPackagerGUI.exe from the .lnk'
    }
    Start-Sleep -Milliseconds 350
    $launched = $process.MainModule.FileName
    if (-not [string]::Equals(
        $launched, $expectedExe,
        [StringComparison]::OrdinalIgnoreCase)) {
      throw "Shortcut opened wrong executable: $launched (expected $expectedExe)"
    }
    Write-Host "PASS: extracted GUI shortcut launched relocated executable: $launched"
  }
  finally {
    if ($null -ne $process) {
      try {
        if (-not $process.HasExited) {
          $process.Kill($true)
          [void]$process.WaitForExit(5000)
        }
      } finally { $process.Dispose() }
    }
    if ($movedOriginal) {
      Move-Item -LiteralPath $hidden -Destination $exePath
    }
    Remove-Item -LiteralPath $dest -Recurse -Force -ErrorAction SilentlyContinue
  }
}

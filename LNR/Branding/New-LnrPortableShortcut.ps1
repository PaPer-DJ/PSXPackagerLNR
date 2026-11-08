# Windows-only build helper: a portable relative Shell Link to the LNR batch interface.
# End users do not run scripts. The generated .lnk is included in the GUI ZIP.
param(
  [Parameter(Mandatory=$true)][string]$GuiDirectory,
  [Parameter(Mandatory=$true)][string]$ShortcutPath,
  [switch]$VerifyRelocation
)
$ErrorActionPreference = "Stop"
$guiDir = (Resolve-Path -LiteralPath $GuiDirectory).Path
$exePath = Join-Path $guiDir "PSXPackagerGUI.exe"
$iconPath = Join-Path $guiDir "Resources/PSXPackagerLNR.ico"
$shortcutPath = [System.IO.Path]::GetFullPath($ShortcutPath)
if (-not (Test-Path -LiteralPath $exePath)) { throw "GUI executable is missing" }
if (-not (Test-Path -LiteralPath $iconPath)) { throw "LNR icon is missing from Resources" }

Add-Type -TypeDefinition @'
using System;
using System.Text;
using System.Runtime.InteropServices;

[ComImport, Guid("00021401-0000-0000-C000-000000000046")]
public class LnrShellLinkClass : ILnrShellLinkW {}

[ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("000214F9-0000-0000-C000-000000000046")]
public interface ILnrShellLinkW {
  void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder path, int maxPath, IntPtr findData, uint flags);
  void GetIDList(out IntPtr pidl);
  void SetIDList(IntPtr pidl);
  void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder description, int maxLength);
  void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string description);
  void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder dir, int maxLength);
  void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string dir);
  void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder arguments, int maxLength);
  void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string arguments);
  void GetHotkey(out short hotkey);
  void SetHotkey(short hotkey);
  void GetShowCmd(out int showCommand);
  void SetShowCmd(int showCommand);
  void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder iconPath, int maxPath, out int iconIndex);
  void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string iconPath, int iconIndex);
  void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string relativePath, uint reserved);
  void Resolve(IntPtr hwnd, uint flags);
  void SetPath([MarshalAs(UnmanagedType.LPWStr)] string path);
}
'@ -ErrorAction Stop

$link = [ILnrShellLinkW]([LnrShellLinkClass]::new())
try {
  $link.SetPath($exePath)
  $link.SetRelativePath("PSXPackagerGUI.exe", [uint32]0)
  $link.SetArguments("--lnr-batch")
  $link.SetDescription("PSXPackagerLNR - Conversion por lotes")
  $link.SetIconLocation('Resources\PSXPackagerLNR.ico',0)
  $persist = [System.Runtime.InteropServices.ComTypes.IPersistFile]$link
  $persist.Save($shortcutPath,$true)
} finally {
  [void][System.Runtime.InteropServices.Marshal]::ReleaseComObject($link)
}
if (-not (Test-Path -LiteralPath $shortcutPath)) { throw "Portable .lnk not created" }

function Assert-LnrLink([string]$path,[string]$expectedDirectory,[bool]$resolve) {
  $object = [ILnrShellLinkW]([LnrShellLinkClass]::new())
  try {
    $reader = [System.Runtime.InteropServices.ComTypes.IPersistFile]$object
    $reader.Load($path, [int]0)
    $icon = [System.Text.StringBuilder]::new(1024)
    $index = 0
    $object.GetIconLocation($icon,1024,[ref]$index)
    if ($icon.ToString() -ne 'Resources\PSXPackagerLNR.ico' -or $index -ne 0) {
      throw "Shortcut icon reference is not relative to Resources: $icon"
    }
    $arguments = [System.Text.StringBuilder]::new(1024)
    $object.GetArguments($arguments,1024)
    if ($arguments.ToString() -ne "--lnr-batch") { throw "LNR batch arguments missing" }
    if ($resolve) {
      # SLR_NO_UI | SLR_NOSEARCH | SLR_NOTRACK
      $object.Resolve([IntPtr]::Zero, [uint32](0x1 -bor 0x10 -bor 0x20))
    }
    $target = [System.Text.StringBuilder]::new(32768)
    $object.GetPath($target,32768,[IntPtr]::Zero,[uint32]0)
    $expected = Join-Path $expectedDirectory "PSXPackagerGUI.exe"
    if (-not [string]::Equals([System.IO.Path]::GetFullPath($target.ToString()),[System.IO.Path]::GetFullPath($expected),[System.StringComparison]::OrdinalIgnoreCase)) {
      throw "Link target does not resolve to relocated GUI: $target (expected $expected)"
    }
    Write-Host "Link validated: $path"
  } finally { [void][System.Runtime.InteropServices.Marshal]::ReleaseComObject($object) }
}
Assert-LnrLink $shortcutPath $guiDir $false

if ($VerifyRelocation) {
  $testDir = Join-Path $env:RUNNER_TEMP ("lnr-portable-test-" + [guid]::NewGuid().ToString("N"))
  New-Item -ItemType Directory -Path (Join-Path $testDir "Resources") -Force | Out-Null
  Copy-Item -LiteralPath $exePath -Destination (Join-Path $testDir "PSXPackagerGUI.exe")
  Copy-Item -LiteralPath $iconPath -Destination (Join-Path $testDir "Resources/PSXPackagerLNR.ico")
  $testLink = Join-Path $testDir ([System.IO.Path]::GetFileName($shortcutPath))
  Copy-Item -LiteralPath $shortcutPath -Destination $testLink
  # Hide the original executable temporarily: resolution must not use its cached CI path.
  $hiddenExe = "$exePath.lnr-test-hidden"
  Move-Item -LiteralPath $exePath -Destination $hiddenExe
  try {
    Assert-LnrLink $testLink $testDir $true
    Write-Host "LNR shortcut target relocates correctly with its parent folder."
  } finally {
    Move-Item -LiteralPath $hiddenExe -Destination $exePath
    Remove-Item -LiteralPath $testDir -Recurse -Force -ErrorAction SilentlyContinue
  }
}

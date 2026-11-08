# Windows build helper only. Writes a portable Shell Link using a stock Windows system icon.
# This script does not ship to end users: users run the prepared .lnk directly.
param(
  [Parameter(Mandatory=$true)][string]$GuiDirectory,
  [Parameter(Mandatory=$true)][string]$ShortcutPath,
  [switch]$VerifyRelocation
)
$ErrorActionPreference = "Stop"
$guiDir = (Resolve-Path -LiteralPath $GuiDirectory).Path
$exePath = Join-Path $guiDir "PSXPackagerGUI.exe"
# System-wide DLL avoids both relative icon paths and a new unsigned launcher.
$stockIconPath = '%SystemRoot%\System32\shell32.dll'
$stockIconIndex = 179 # Icon with blue arrows (batch/transfer visual)
$resolvedIconPath = [Environment]::ExpandEnvironmentVariables($stockIconPath)
$shortcutPath = [System.IO.Path]::GetFullPath($ShortcutPath)
if (-not (Test-Path -LiteralPath $exePath)) { throw "GUI executable is missing" }
if (-not (Test-Path -LiteralPath $resolvedIconPath -PathType Leaf)) { throw "Windows shell icon DLL missing: $resolvedIconPath" }

Add-Type -TypeDefinition @'
using System;
using System.Text;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

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
public sealed class LnrPortableLink : IDisposable {
  [DllImport("ole32.dll", ExactSpelling=true)]
  static extern int CoCreateInstance(ref Guid clsid, IntPtr outer, uint context, ref Guid iid, out IntPtr ppv);
  readonly ILnrShellLinkW inner;
  public LnrPortableLink() {
    Guid clsid = new Guid("00021401-0000-0000-C000-000000000046");
    Guid iid = typeof(ILnrShellLinkW).GUID;
    IntPtr ptr;
    int result = CoCreateInstance(ref clsid, IntPtr.Zero, 1, ref iid, out ptr);
    Marshal.ThrowExceptionForHR(result);
    try { inner = (ILnrShellLinkW)Marshal.GetObjectForIUnknown(ptr); }
    finally { Marshal.Release(ptr); }
  }
  public void SetPath(string path) { inner.SetPath(path); }
  public void SetArguments(string args) { inner.SetArguments(args); }
  public void SetDescription(string description) { inner.SetDescription(description); }
  public void SetIconLocation(string icon,int index) { inner.SetIconLocation(icon,index); }
  public void Save(string path) { ((IPersistFile)inner).Save(path,true); }
  public void Load(string path) { ((IPersistFile)inner).Load(path,0); }
  public void Resolve() { inner.Resolve(IntPtr.Zero,(uint)0x1); } // SLR_NO_UI
  public string IconLocation(int expectedIndex) {
    var sb = new StringBuilder(1024);
    int index;
    inner.GetIconLocation(sb,sb.Capacity,out index);
    if (index != expectedIndex) throw new InvalidOperationException("Unexpected icon index " + index + " expected " + expectedIndex);
    return sb.ToString();
  }
  public string Arguments() {
    var sb = new StringBuilder(1024);
    inner.GetArguments(sb,sb.Capacity);
    return sb.ToString();
  }
  public string TargetPath() {
    var sb = new StringBuilder(32768);
    inner.GetPath(sb,sb.Capacity,IntPtr.Zero,0);
    return sb.ToString();
  }
  public void Dispose() { if (inner != null) Marshal.ReleaseComObject(inner); }
}
public static class LnrIconProbe {
  [DllImport("shell32.dll", CharSet=CharSet.Unicode)]
  public static extern uint ExtractIconEx(string file, int index, out IntPtr largeIcon, out IntPtr smallIcon, uint count);
  [DllImport("user32.dll")]
  public static extern bool DestroyIcon(IntPtr handle);
}
'@ -ErrorAction Stop
# Validate that the selected index resolves to a real icon in the Windows system DLL.
$largeIcon = [IntPtr]::Zero
$smallIcon = [IntPtr]::Zero
$iconCount = [LnrIconProbe]::ExtractIconEx($resolvedIconPath, $stockIconIndex, [ref]$largeIcon, [ref]$smallIcon, [uint32]1)
try {
  if ($iconCount -lt 1 -or ($largeIcon -eq [IntPtr]::Zero -and $smallIcon -eq [IntPtr]::Zero)) {
    throw "Windows icon #$stockIconIndex could not be extracted from $resolvedIconPath"
  }
  Write-Host "Windows stock icon #$stockIconIndex available from $resolvedIconPath"
} finally {
  if ($largeIcon -ne [IntPtr]::Zero) { [void][LnrIconProbe]::DestroyIcon($largeIcon) }
  if ($smallIcon -ne [IntPtr]::Zero) { [void][LnrIconProbe]::DestroyIcon($smallIcon) }
}

# Create a standard Windows Shell Link with native COM APIs. Unlike the
# previous implementation, do NOT call SetRelativePath when using IPersistFile
# to save to disk: Windows computes it automatically when saving.
$link = [LnrPortableLink]::new()
try {
  $link.SetPath($exePath)
  $link.SetArguments('--lnr-batch')
  $link.SetDescription('PSXPackagerLNR - Conversión por lotes')
  $link.SetIconLocation($stockIconPath,$stockIconIndex)
  $link.Save($shortcutPath)
} finally { $link.Dispose() }
if (-not (Test-Path -LiteralPath $shortcutPath)) { throw 'Shortcut was not created' }
Write-Host 'Created Shell Link without SetRelativePath calls'

function Assert-LnrLink([string]$path,[string]$expectedDirectory,[bool]$resolve) {
  $link = [LnrPortableLink]::new()
  try {
    $link.Load($path)
    $iconLocation = $link.IconLocation($script:stockIconIndex)
    if ($iconLocation -notin @($script:stockIconPath, $script:resolvedIconPath)) {
      throw "Shortcut icon not Windows system DLL: $iconLocation"
    }
    Write-Host "Shortcut uses stock icon #$script:stockIconIndex from $iconLocation"
    if ($link.Arguments() -ne "--lnr-batch") { throw "LNR batch arguments missing" }
    if ($resolve) {
      # IMPORTANT: Do not call SetRelativePath here. Explorer does not call
      # it either. The CI test must reproduce the real user's double-click.
      $link.Resolve()
    }
    $target = $link.TargetPath()
    $expected = Join-Path $expectedDirectory "PSXPackagerGUI.exe"
    if (-not [string]::Equals([System.IO.Path]::GetFullPath($target),[System.IO.Path]::GetFullPath($expected),[System.StringComparison]::OrdinalIgnoreCase)) {
      throw "Link target did not resolve to relocated GUI: $target (expected $expected)"
    }
    Write-Host "Portable shortcut verified: $path"
  } finally { $link.Dispose() }
}
Assert-LnrLink $shortcutPath $guiDir $false

if ($VerifyRelocation) {
  # The published ZIP is built on D:, but most users extract it to C:.
  # A cross-volume relocation test catches absolute cached targets that
  # same-drive tests can miss.
  $testBase = [Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData)
  if ([System.IO.Path]::GetPathRoot($testBase) -eq [System.IO.Path]::GetPathRoot($guiDir)) {
    $testBase = Join-Path $env:SystemDrive "LnrShortcutPortableTests"
  }
  New-Item -ItemType Directory -Path $testBase -Force | Out-Null
  $testDir = Join-Path $testBase ("lnr-portable-" + [guid]::NewGuid().ToString("N"))
  Write-Host "Testing cross-volume relocation: $guiDir => $testDir"
  if ([System.IO.Path]::GetPathRoot($testDir) -eq [System.IO.Path]::GetPathRoot($guiDir)) {
    throw "Relocation test must use a different Windows drive letter"
  }
  New-Item -ItemType Directory -Path (Join-Path $testDir "Resources") -Force | Out-Null
  Copy-Item -LiteralPath $exePath -Destination (Join-Path $testDir "PSXPackagerGUI.exe")
  # The Windows stock icon comes from the OS and must not be copied.
  $testLink = Join-Path $testDir ([System.IO.Path]::GetFileName($shortcutPath))
  Copy-Item -LiteralPath $shortcutPath -Destination $testLink
  $hiddenExe = "$exePath.lnr-test-hidden"
  Move-Item -LiteralPath $exePath -Destination $hiddenExe
  try {
    Assert-LnrLink $testLink $testDir $true

    # ShellExecute is the closest automated equivalent to double-clicking the
    # extracted .lnk in Windows Explorer. Test the UNMODIFIED link on disk,
    # not the in-memory COM object used by Assert-LnrLink.
    $startInfo = [System.Diagnostics.ProcessStartInfo]::new($testLink)
    $startInfo.UseShellExecute = $true
    $started = [System.Diagnostics.Process]::Start($startInfo)
    if ($null -eq $started) { throw 'Windows ShellExecute did not return a process for the shortcut' }
    try {
      $actualExe = $started.MainModule.FileName
      $expectedExe = Join-Path $testDir 'PSXPackagerGUI.exe'
      if (-not [string]::Equals(
          [System.IO.Path]::GetFullPath($actualExe),
          [System.IO.Path]::GetFullPath($expectedExe),
          [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "ShellExecute opened the wrong executable: $actualExe expected $expectedExe"
      }
      Write-Host "Explorer-style activation launched the relocated GUI: $actualExe"
    } finally {
      if (-not $started.HasExited) {
        $started.Kill($true)
        [void]$started.WaitForExit(5000)
      }
      $started.Dispose()
    }
  } finally {
    Move-Item -LiteralPath $hiddenExe -Destination $exePath
    Remove-Item -LiteralPath $testDir -Recurse -Force -ErrorAction SilentlyContinue
  }
  Write-Host "Portable shortcut relocation test passed."
}

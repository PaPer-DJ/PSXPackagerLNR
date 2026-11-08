# Windows build helper only. Writes a portable Shell Link with the LNR MOD ICO in Resources.
# This script does not ship to end users: users run the prepared .lnk directly.
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
if (-not (Test-Path -LiteralPath $iconPath)) { throw "LNR ICO missing from Resources" }

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
  public void SetRelativePath(string relative) { inner.SetRelativePath(relative,0); }
  public void SetArguments(string args) { inner.SetArguments(args); }
  public void SetDescription(string description) { inner.SetDescription(description); }
  public void SetIconLocation(string icon) { inner.SetIconLocation(icon,0); }
  public void Save(string path) { ((IPersistFile)inner).Save(path,true); }
  public void Load(string path) { ((IPersistFile)inner).Load(path,0); }
  public void Resolve() { inner.Resolve(IntPtr.Zero,(uint)0x1); // SLR_NO_UI; allow relative resolution }
  public string IconLocation() {
    var sb = new StringBuilder(1024);
    int index;
    inner.GetIconLocation(sb,sb.Capacity,out index);
    if (index != 0) throw new InvalidOperationException("Unexpected icon index " + index);
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
'@ -ErrorAction Stop

$link = [LnrPortableLink]::new()
try {
  $link.SetPath($exePath)
  $link.SetRelativePath("PSXPackagerGUI.exe")
  $link.SetArguments("--lnr-batch")
  $link.SetDescription("PSXPackagerLNR - Conversion por lotes")
  $link.SetIconLocation('Resources\PSXPackagerLNR.ico')
  $link.Save($shortcutPath)
} finally { $link.Dispose() }
if (-not (Test-Path -LiteralPath $shortcutPath)) { throw "Portable .lnk not created" }

function Assert-LnrLink([string]$path,[string]$expectedDirectory,[bool]$resolve) {
  $link = [LnrPortableLink]::new()
  try {
    $link.Load($path)
    if ($link.IconLocation() -ne 'Resources\PSXPackagerLNR.ico') {
      throw "Shortcut icon not relative to Resources: $($link.IconLocation())"
    }
    if ($link.Arguments() -ne "--lnr-batch") { throw "LNR batch arguments missing" }
    if ($resolve) { $link.Resolve() }
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
  $testDir = Join-Path $env:RUNNER_TEMP ("lnr-portable-" + [guid]::NewGuid().ToString("N"))
  New-Item -ItemType Directory -Path (Join-Path $testDir "Resources") -Force | Out-Null
  Copy-Item -LiteralPath $exePath -Destination (Join-Path $testDir "PSXPackagerGUI.exe")
  Copy-Item -LiteralPath $iconPath -Destination (Join-Path $testDir "Resources/PSXPackagerLNR.ico")
  $testLink = Join-Path $testDir ([System.IO.Path]::GetFileName($shortcutPath))
  Copy-Item -LiteralPath $shortcutPath -Destination $testLink
  $hiddenExe = "$exePath.lnr-test-hidden"
  Move-Item -LiteralPath $exePath -Destination $hiddenExe
  try {
    Assert-LnrLink $testLink $testDir $true
  } finally {
    Move-Item -LiteralPath $hiddenExe -Destination $exePath
    Remove-Item -LiteralPath $testDir -Recurse -Force -ErrorAction SilentlyContinue
  }
  Write-Host "Portable shortcut relocation test passed."
}

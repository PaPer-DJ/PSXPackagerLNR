# Synthetic, game-data-free regression for PSXPackagerLNR Toolkit.
# Windows PowerShell 5.1 on GitHub Actions; not shipped in public Toolkit.
param()
$ErrorActionPreference='Stop'
$script=(Resolve-Path (Join-Path $PSScriptRoot '..\Batch\LNR_CHD_A_PBP.ps1')).Path
$base=Join-Path $env:RUNNER_TEMP ('LNR Toolkit Parallel Smoke '+[guid]::NewGuid().ToString('N'))
$source=Join-Path $base 'ROMs Con Espacios'
$tools=Join-Path $base 'Toolkit Tools'
$trace=Join-Path $tools '_TEMP\TRACE'
$destination=Join-Path $source 'PBP'
foreach($dir in @($source,$tools,$trace,$destination)) {
    [void](New-Item -ItemType Directory -Path $dir -Force)
}
function Assert-Toolkit([bool]$condition,[string]$message) {
    if (-not $condition) { throw "Toolkit smoke test: $message" }
}
$fake=@'
using System;
using System.IO;
using System.Text;
using System.Threading;

public static class ToolkitFakeCli
{
    public static int Main(string[] args)
    {
        string input = null, output = null;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "-i" && i + 1 < args.Length) input = args[++i];
            else if (args[i] == "-o" && i + 1 < args.Length) output = args[++i];
        }
        if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(output)) return 7;
        Directory.CreateDirectory(output);
        string title = Path.GetFileNameWithoutExtension(input);
        long started = DateTime.UtcNow.Ticks;
        Thread.Sleep(1700);
        if (title.IndexOf("Fail", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            File.WriteAllBytes(Path.Combine(output, "partial.pbp"), new byte[] { 0, 1, 2 });
            return 9;
        }
        byte[] pbp = new byte[4096];
        pbp[0] = 0; pbp[1] = 0x50; pbp[2] = 0x42; pbp[3] = 0x50;
        File.WriteAllBytes(Path.Combine(output, title + ".pbp"), pbp);
        long ended = DateTime.UtcNow.Ticks;
        string traceDir = Path.Combine(Path.GetDirectoryName(output), "TRACE");
        Directory.CreateDirectory(traceDir);
        File.WriteAllText(Path.Combine(traceDir, Path.GetFileName(output) + ".txt"),
            started.ToString() + "," + ended.ToString(), Encoding.UTF8);
        Console.WriteLine("Progress: 100 %");
        return 0;
    }
}
'@
Add-Type -TypeDefinition $fake -OutputAssembly (Join-Path $tools 'psxpackager.exe') -OutputType ConsoleApplication
try {
    # Seven concurrency values accepted without executing a real conversion.
    foreach($parallel in @(0,1,2,3,4,6,8)) {
        $single=Join-Path $source 'Demo Inicial.chd'
        if (-not (Test-Path $single)) {
            [System.IO.File]::WriteAllBytes($single,[byte[]]@(1,2,3,4))
        }
        $plan=Join-Path $base ('plan_'+$parallel+'.json')
        & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $script -SourceDir $source -ToolsDir $tools -MultiMode Separados -Level 3 -Parallel $parallel -PlanOnly -PlanJson $plan | Out-Null
        Assert-Toolkit ($LASTEXITCODE -eq 0) "PlanOnly must accept $parallel"
        $parsed=Get-Content -LiteralPath $plan -Raw | ConvertFrom-Json
        Assert-Toolkit ($parsed.parallel -eq $parallel) "Plan JSON must persist selected parallel=$parallel"
    }

    # Parallel real-process smoke: four running slots, six new PBPs, one
    # existing PBP skipped, one external SBI copied. No CHD may be deleted.
    for($n=1;$n -le 6;$n++) {
        [System.IO.File]::WriteAllBytes((Join-Path $source ("Juego Numero $n.chd")),[byte[]]@(1,2,3,4))
    }
    $existing=Join-Path $source 'Existing.chd'
    [System.IO.File]::WriteAllBytes($existing,[byte[]]@(1,2,3,4))
    $sentinel=Join-Path $destination 'Existing.pbp'
    [System.IO.File]::WriteAllText($sentinel,'NO CAMBIAR')
    [System.IO.File]::WriteAllText((Join-Path $source 'Juego Numero 1.sbi'),'SBI-SAMPLE')

    Write-Host 'Test Toolkit -Parallel 4 with independent synthetic CLI processes'
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $script -SourceDir $source -ToolsDir $tools -MultiMode Separados -Level 3 -Parallel 4
    Assert-Toolkit ($LASTEXITCODE -eq 0) 'Parallel Toolkit conversion must return 0'
    Assert-Toolkit ((Get-Content -LiteralPath $sentinel -Raw) -eq 'NO CAMBIAR') 'Existing PBP must be unchanged'
    Assert-Toolkit (Test-Path (Join-Path $destination 'Juego Numero 1.sbi')) 'SBI must be copied'
    for($n=1;$n -le 6;$n++) {
        $name="Juego Numero $n"
        $pbp=Join-Path $destination ($name+'.pbp')
        $chd=Join-Path $source ($name+'.chd')
        Assert-Toolkit (Test-Path $pbp) "Missing $pbp"
        Assert-Toolkit ((Get-Item $pbp).Length -eq 4096) "Bad PBP size for $name"
        Assert-Toolkit (Test-Path $chd) "Original CHD deleted for $name"
    }
    Assert-Toolkit (Test-Path $existing) 'Original CHD with existing PBP deleted'
    $traces=@(Get-ChildItem -LiteralPath $trace -File -Filter '*.txt')
    Assert-Toolkit ($traces.Count -eq 7) "Expected seven converted traces (including demo), got $($traces.Count)"
    $intervals=foreach($traceFile in $traces) {
        $parts=(Get-Content -LiteralPath $traceFile -Raw).Trim().Split(',')
        [pscustomobject]@{ Start=[long]$parts[0]; End=[long]$parts[1] }
    }
    $events=@(
        foreach($interval in $intervals) {
            [pscustomobject]@{ Time=$interval.Start; Delta=1 }
            [pscustomobject]@{ Time=$interval.End; Delta=-1 }
        }
    ) | Sort-Object Time, Delta
    $inFlight=0
    $peak=0
    foreach($event in $events) {
        $inFlight+=$event.Delta
        if ($inFlight -gt $peak) { $peak=$inFlight }
    }
    Assert-Toolkit ($peak -ge 2 -and $peak -le 4) "Expected real overlapping conversions capped at 4; peak=$peak"
    $stages=@(Get-ChildItem -LiteralPath (Join-Path $tools '_TEMP') -Directory -Filter 'LNR_*')
    Assert-Toolkit ($stages.Count -eq 0) 'Stages must be cleaned after successful conversions'
    Write-Host "OK - actual max concurrency: $peak; all original files protected"

    # Grouped CHD remains an indivisible task with two inputs.
    $multi=Join-Path $base 'M3U source'
    [void](New-Item -ItemType Directory -Path $multi -Force)
    foreach($num in 1,2) {
        [System.IO.File]::WriteAllBytes((Join-Path $multi ("Multidisc Example - CD$num.chd")),[byte[]]@(1,2,3,4))
    }
    @('Multidisc Example - CD1.chd','Multidisc Example - CD2.chd') |
        Set-Content -LiteralPath (Join-Path $multi 'Multidisc Example.m3u') -Encoding utf8
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $script -SourceDir $multi -ToolsDir $tools -MultiMode Unidos -Level 3 -Parallel 3
    Assert-Toolkit ($LASTEXITCODE -eq 0) 'M3U conversion must return 0'
    Assert-Toolkit (Test-Path (Join-Path $multi 'PBP\Multidisc Example.pbp')) 'Missing grouped PBP'
    Assert-Toolkit (@(Get-ChildItem -LiteralPath (Join-Path $multi 'PBP') -Filter '*.pbp').Count -eq 1) 'Multidisc must make one PBP'
    Assert-Toolkit (@(Get-ChildItem -LiteralPath $multi -Filter '*.chd').Count -eq 2) 'Multidisc CHDs preserved'

    # Failed conversion must not create destination, must clean temporary
    # PBP and allow other games to succeed.
    $failure=Join-Path $base 'Failures'
    [void](New-Item -ItemType Directory -Path $failure -Force)
    foreach($name in @('Fail Game','Working Game')) {
        [System.IO.File]::WriteAllBytes((Join-Path $failure ($name+'.chd')),[byte[]]@(1,2,3,4))
    }
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $script -SourceDir $failure -ToolsDir $tools -MultiMode Separados -Level 3 -Parallel 2
    Assert-Toolkit ($LASTEXITCODE -eq 1) 'Failed conversion must signal exit 1'
    Assert-Toolkit (-not (Test-Path (Join-Path $failure 'PBP\Fail Game.pbp'))) 'Failed result must not be committed'
    Assert-Toolkit (Test-Path (Join-Path $failure 'PBP\Working Game.pbp')) 'Other job must still finish'
    Assert-Toolkit (@(Get-ChildItem -LiteralPath (Join-Path $tools '_TEMP') -Directory -Filter 'LNR_*').Count -eq 0) 'Partial stages must be removed'
    Write-Host 'Toolkit parallel smoke PASS: options, real overlap, max=4, M3U, SBI, skip, failure and temp cleanup.'
}
finally {
    if (Test-Path -LiteralPath $base) {
        Remove-Item -LiteralPath $base -Recurse -Force -ErrorAction SilentlyContinue
    }
}

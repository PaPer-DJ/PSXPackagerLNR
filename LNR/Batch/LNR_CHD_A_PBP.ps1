# PSXPackagerLNR - conversor CHD -> PBP por lotes (Windows PowerShell 5.1+)
# Copyright de la adaptacion de automatizacion: LaNaveRETRO.
# PSXPackager sigue siendo obra de sus autores originales; consulte LICENSE.
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$SourceDir,
    [Parameter(Mandatory=$true)][string]$ToolsDir,
    [ValidateSet('Separados','Unidos')][string]$MultiMode='Separados',
    [ValidateSet(1,3,5,9)][int]$Level=3,
    [switch]$PlanOnly,
    [string]$PlanJson
)
$ErrorActionPreference='Stop'
$script:Jobs=New-Object 'System.Collections.Generic.List[object]'
$script:LogLines=New-Object 'System.Collections.Generic.List[string]'
$script:Warnings=New-Object 'System.Collections.Generic.List[string]'
$script:OK=0; $script:Skipped=0; $script:Failed=0
$script:InputBytes=[long]0; $script:OutputBytes=[long]0
$culture=[System.Globalization.CultureInfo]::GetCultureInfo('es-ES')

function Log([string]$message,[string]$type='INFO') {
    $line='[{0}] [{1}] {2}' -f (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'),$type,$message
    [void]$script:LogLines.Add($line)
    if ($type -eq 'ERROR') { Write-Host $line -ForegroundColor Red }
    elseif ($type -eq 'AVISO') { Write-Host $line -ForegroundColor Yellow }
    elseif ($type -eq 'OK') { Write-Host $line -ForegroundColor Green }
    else { Write-Host $line }
}
function Warn([string]$message) {
    [void]$script:Warnings.Add($message)
    Log $message 'AVISO'
}
function MB([long]$bytes) {
    return (($bytes / 1MB).ToString('N2',$culture) + ' MB')
}
function Add-Job([string]$name,[string[]]$discs,[string]$origin) {
    [void]$script:Jobs.Add([pscustomobject]@{
        Name=$name; Discs=@($discs); Origin=$origin
    })
}
function Find-Cli([string]$dir) {
    $paths=@(
        (Join-Path $dir 'psxpackager.exe'),
        (Join-Path $dir 'PSXPackagerLNR-CLI-win-x64\psxpackager.exe'),
        (Join-Path $dir 'CLI\psxpackager.exe')
    )
    foreach ($candidate in $paths) {
        if (Test-Path -LiteralPath $candidate -PathType Leaf) { return $candidate }
    }
    return $null
}

try {
    $SourceDir=(Resolve-Path -LiteralPath $SourceDir).Path
    $ToolsDir=(Resolve-Path -LiteralPath $ToolsDir).Path
    $chds=@(Get-ChildItem -LiteralPath $SourceDir -File -Filter '*.chd' | Sort-Object Name)
    if ($chds.Count -eq 0) { throw 'No hay archivos CHD directamente en la carpeta seleccionada.' }
    $byName=@{}
    foreach ($file in $chds) { $byName[$file.Name]=$file.FullName }
    $claimed=@{}; $blocked=@{}
    Log ('ROM: '+$SourceDir)
    Log ('Herramientas: '+$ToolsDir)
    Log ('CHD encontrados: '+$chds.Count+' | Modo: '+$MultiMode+' | Nivel: '+$Level)

    if ($MultiMode -eq 'Unidos') {
        # Los M3U existentes tienen prioridad. Si estan incompletos, se bloquean
        # sus discos presentes para evitar convertir un juego mutilado por error.
        $playlists=@(Get-ChildItem -LiteralPath $SourceDir -File -Filter '*.m3u' | Sort-Object Name)
        foreach ($playlist in $playlists) {
            $found=New-Object 'System.Collections.Generic.List[string]'
            $bad=New-Object 'System.Collections.Generic.List[string]'
            foreach ($raw in [System.IO.File]::ReadAllLines($playlist.FullName)) {
                $entry=$raw.Trim().Trim('"')
                if (-not $entry -or $entry.StartsWith('#')) { continue }
                $leaf=[System.IO.Path]::GetFileName($entry)
                if ($entry -ne $leaf) {
                    [void]$bad.Add('Ruta fuera de esta carpeta: '+$entry); continue
                }
                $extension=[System.IO.Path]::GetExtension($leaf)
                if ($extension -ieq '.cue') { $leaf=[System.IO.Path]::ChangeExtension($leaf,'.chd') }
                elseif ($extension -ine '.chd') {
                    [void]$bad.Add('Extension no compatible: '+$entry); continue
                }
                if (-not $byName.ContainsKey($leaf)) {
                    [void]$bad.Add('Falta '+$leaf); continue
                }
                [void]$found.Add($byName[$leaf])
            }
            $unique=@($found | Select-Object -Unique)
            $overlap=@($found | Where-Object { $claimed.ContainsKey($_) -or $blocked.ContainsKey($_) })
            if ($bad.Count -gt 0 -or $found.Count -lt 2 -or $found.Count -gt 5 -or $unique.Count -ne $found.Count -or $overlap.Count -gt 0) {
                foreach ($p in $found) { $blocked[$p]=$true }
                Warn ('M3U omitido (incompleto, duplicado o no valido): '+$playlist.Name+'; '+($bad -join '; '))
                continue
            }
            foreach ($p in $found) { $claimed[$p]=$true }
            Add-Job $playlist.BaseName ([string[]]$found.ToArray()) 'M3U'
        }

        # Agrupa CD1/CD2 o (Disc 1)/(Disc 2) solamente si la serie esta completa.
        $groups=@{}
        foreach ($file in $chds) {
            if ($claimed.ContainsKey($file.FullName) -or $blocked.ContainsKey($file.FullName)) { continue }
            $match=[regex]::Match($file.BaseName,'^(?<title>.+?)\s*[-_.]?\s*\(?\s*(?:CD|Disc|Disk)\s*[-_.]?\s*(?<n>[1-5])\s*\)?$',[System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
            if (-not $match.Success) { continue }
            $title=$match.Groups['title'].Value.Trim(' ','-','_','.')
            if (-not $title) { continue }
            if (-not $groups.ContainsKey($title)) { $groups[$title]=@() }
            $groups[$title]+=[pscustomobject]@{ Number=[int]$match.Groups['n'].Value; Path=$file.FullName }
        }
        foreach ($title in @($groups.Keys | Sort-Object)) {
            $members=@($groups[$title] | Sort-Object Number)
            if ($members.Count -lt 2 -or $members.Count -gt 5) { continue }
            $complete=$true
            for ($n=0;$n -lt $members.Count;$n++) {
                if ($members[$n].Number -ne ($n+1)) { $complete=$false }
            }
            if (-not $complete) { Warn ('Serie incompleta: '+$title+' (los discos quedan separados)'); continue }
            $files=@($members | ForEach-Object { $_.Path })
            foreach ($p in $files) { $claimed[$p]=$true }
            Add-Job $title ([string[]]$files) 'AUTO'
        }
    }

    # Todos los CHD sueltos, o todos en modo separados.
    foreach ($file in $chds) {
        if ($claimed.ContainsKey($file.FullName) -or $blocked.ContainsKey($file.FullName)) { continue }
        Add-Job $file.BaseName ([string[]]@($file.FullName)) 'INDIVIDUAL'
    }

    # No generar dos veces el mismo nombre, ni sobrescribir por colisiones.
    $used=@{}; $valid=New-Object 'System.Collections.Generic.List[object]'
    foreach ($job in $script:Jobs) {
        $name=$job.Name+'.pbp'
        if ($used.ContainsKey($name)) {
            Warn ('Colision de nombres, omitido: '+$name)
            continue
        }
        $used[$name]=$true
        [void]$valid.Add($job)
    }
    $script:Jobs=$valid

    Log ('PBP previstos: '+$script:Jobs.Count+' | Avisos: '+$script:Warnings.Count)
    if ($PlanJson) {
        $plan=[pscustomobject]@{
            mode=$MultiMode; level=$Level; source=$SourceDir
            jobs=@($script:Jobs.ToArray()); warnings=@($script:Warnings.ToArray())
        }
        $json=$plan | ConvertTo-Json -Depth 8
        [System.IO.File]::WriteAllText($PlanJson,$json,(New-Object System.Text.UTF8Encoding($false)))
    }
    if ($PlanOnly) {
        foreach ($job in $script:Jobs) { Log ('PLAN '+$job.Name+'.pbp | '+$job.Discs.Count+' disco(s) | '+$job.Origin) }
        exit 0
    }

    $cli=Find-Cli $ToolsDir
    if (-not $cli) { throw ('No se encuentra psxpackager.exe en '+$ToolsDir) }
    $destDir=Join-Path $SourceDir 'PBP'
    $tempRoot=Join-Path $ToolsDir '_TEMP'
    $logDir=Join-Path $ToolsDir 'LOGS'
    foreach ($dir in @($destDir,$tempRoot,$logDir)) {
        if (-not (Test-Path -LiteralPath $dir)) { [void](New-Item -ItemType Directory -Path $dir -Force) }
    }
    $report=Join-Path $logDir ('LNR_'+(Get-Date -Format 'yyyyMMdd_HHmmss')+'.txt')
    Log ('CLI: '+$cli)
    Log ('Destino: '+$destDir)
    Log ('Informe: '+$report)
    $i=0
    foreach ($job in $script:Jobs) {
        $i++
        $output=Join-Path $destDir ($job.Name+'.pbp')
        if (Test-Path -LiteralPath $output) {
            $script:Skipped++; Log ('['+$i+'/'+$script:Jobs.Count+'] EXISTE, se omite: '+$job.Name) 'AVISO'
            continue
        }
        $stage=Join-Path $tempRoot ('LNR_'+[guid]::NewGuid().ToString('N'))
        try {
            [void](New-Item -ItemType Directory -Path $stage -Force)
            $inputFile=$job.Discs[0]
            if ($job.Discs.Count -gt 1) {
                # CHD absolutos: el conversor los abre directamente, sin BIN/CUE.
                $inputFile=Join-Path $stage ($job.Name+'.m3u')
                [System.IO.File]::WriteAllLines($inputFile,[string[]]$job.Discs,(New-Object System.Text.UTF8Encoding($false)))
            }
            Log ('['+$i+'/'+$script:Jobs.Count+'] '+$job.Name+' ('+$job.Discs.Count+' disco(s))')
            & $cli -i $inputFile -o $stage -l $Level -s
            if ($LASTEXITCODE -ne 0) { throw ('La CLI devolvio '+$LASTEXITCODE) }
            $candidates=@(Get-ChildItem -LiteralPath $stage -File -Filter '*.pbp')
            if ($candidates.Count -ne 1 -or $candidates[0].Length -lt 4096) {
                throw ('Se esperaba 1 PBP valido, encontrados: '+$candidates.Count)
            }
            $handle=[System.IO.File]::OpenRead($candidates[0].FullName)
            try {
                $header=New-Object byte[] 4
                if ($handle.Read($header,0,4) -ne 4 -or
                    $header[0] -ne 0 -or $header[1] -ne 0x50 -or
                    $header[2] -ne 0x42 -or $header[3] -ne 0x50) {
                    throw 'Cabecera PBP no reconocida'
                }
            } finally { $handle.Dispose() }
            if (Test-Path -LiteralPath $output) { throw 'Otro proceso ya creo el archivo de destino' }
            Move-Item -LiteralPath $candidates[0].FullName -Destination $output -ErrorAction Stop
            $a=[long]0
            foreach ($disc in $job.Discs) { $a+=(Get-Item -LiteralPath $disc).Length }
            $b=(Get-Item -LiteralPath $output).Length
            $script:InputBytes+=$a; $script:OutputBytes+=$b; $script:OK++
            Log ('OK: CHD '+(MB $a)+' -> PBP '+(MB $b)+' | diferencia '+(MB ($a-$b))) 'OK'
            foreach ($disc in $job.Discs) {
                $sbi=[System.IO.Path]::ChangeExtension($disc,'.sbi')
                if (Test-Path -LiteralPath $sbi -PathType Leaf) {
                    $targetSbi=Join-Path $destDir ([System.IO.Path]::GetFileName($sbi))
                    if (-not (Test-Path -LiteralPath $targetSbi)) {
                        Copy-Item -LiteralPath $sbi -Destination $targetSbi
                    }
                    if ($job.Discs.Count -gt 1) {
                        Warn ('SBI del multidisco conservado aparte; compatibilidad depende del emulador: '+$job.Name)
                    }
                }
            }
        } catch {
            $script:Failed++
            Log ('ERROR '+$job.Name+': '+$_.Exception.Message) 'ERROR'
        } finally {
            if (Test-Path -LiteralPath $stage) {
                Remove-Item -LiteralPath $stage -Recurse -Force -ErrorAction SilentlyContinue
            }
        }
    }
    Log ('FIN: OK '+$script:OK+' | Ya existentes '+$script:Skipped+' | Fallos '+$script:Failed+' | Avisos '+$script:Warnings.Count)
    Log ('CHD convertidos: '+(MB $script:InputBytes)+' | PBP generados: '+(MB $script:OutputBytes))
    if ($script:InputBytes -gt 0) {
        Log ('Variacion respecto a CHD: '+(MB ($script:InputBytes-$script:OutputBytes))+' ('+((100*($script:InputBytes-$script:OutputBytes)/$script:InputBytes).ToString('N2',$culture))+' %)')
    }
    [System.IO.File]::WriteAllLines($report,[string[]]$script:LogLines.ToArray(),(New-Object System.Text.UTF8Encoding($true)))
    if ($script:Failed -gt 0) { exit 1 }
    exit 0
} catch {
    Log ('ERROR general: '+$_.Exception.Message) 'ERROR'
    exit 2
}

# PSXPackagerLNR - conversor CHD -> PBP por lotes (Windows PowerShell 5.1+)
# Copyright de la adaptacion de automatizacion: LaNaveRETRO.
# PSXPackager sigue siendo obra de sus autores originales; consulte LICENSE.
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$SourceDir,
    [Parameter(Mandatory=$true)][string]$ToolsDir,
    [ValidateSet('Separados','Unidos')][string]$MultiMode='Separados',
    [ValidateSet(1,3,5,9)][int]$Level=3,
    # 0 = Auto (1..4), otherwise cap simultaneous independent CLI processes.
    [ValidateSet(0,1,2,3,4,6,8)][int]$Parallel=4,
    [switch]$PlanOnly,
    [string]$PlanJson,
    # Optional automation/test command channel; the BAT uses keyboard P/R/C.
    [string]$ControlFile=''
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
    $effectiveParallel=if ($Parallel -eq 0) {
        [int][Math]::Min(4,[Math]::Max(1,[Math]::Floor([Environment]::ProcessorCount/2.0)))
    } else { $Parallel }
    Log ('ROM: '+$SourceDir)
    Log ('Herramientas: '+$ToolsDir)
    Log ('CHD encontrados: '+$chds.Count+' | Modo: '+$MultiMode+' | Nivel: '+$Level+' | Conversiones simultaneas: '+$effectiveParallel+$(if($Parallel -eq 0){' (Auto)'}else{''}))

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
            mode=$MultiMode; level=$Level; parallel=$Parallel; source=$SourceDir
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
    # La GUI LNR y el Toolkit comparten la arquitectura: UN proceso
    # psxpackager.exe aislado y UN staging por juego, nunca varios
    # threads comprimiendo el mismo PBP. Windows PowerShell 5.1 compatible.
    # Leemos stdout y stderr de forma asincrona para evitar deadlocks.
    function Quote-CliArg([string]$value) {
        # Paths de archivos Windows no contienen comillas dobles. Todos los
        # argumentos de ruta usados aqui acaban en nombre de archivo/carpeta,
        # nunca en barra inversa, por lo que son seguros entre comillas.
        if ($value.Contains('"')) { throw 'Ruta invalida para la CLI: contiene comillas dobles.' }
        return ('"' + $value + '"')
    }

    $active=New-Object 'System.Collections.Generic.List[object]'
    $next=0
    $lastStatus=[DateTime]::UtcNow
    $slots=[Math]::Min($effectiveParallel,[Math]::Max(1,$script:Jobs.Count))
    Log ('PLAN DE EJECUCION: '+$slots+' conversion(es) simultanea(s). Los CHD originales se conservan siempre.')

    # Interactive control is non-blocking. PAUSAR is intentionally a drain:
    # active CLI processes finish and commit normally before EN PAUSA is shown.
    $script:PauseRequested=$false
    $script:Paused=$false
    $script:CancelRequested=$false
    $script:ConsoleControlsEnabled=$true
    try { $script:ConsoleControlsEnabled=-not [Console]::IsInputRedirected } catch { $script:ConsoleControlsEnabled=$false }
    Log 'CONTROLES: [P] Pausar con seguridad (termina juegos activos) | [R] Reanudar | [C] Cancelar el lote.'
    if ($ControlFile) { Log 'Control por fichero habilitado para automatizacion avanzada.' }

    function Set-ControlState([string]$state) {
        if (-not $ControlFile) { return }
        try {
            [System.IO.File]::WriteAllText(($ControlFile+'.state'),$state,
                (New-Object System.Text.UTF8Encoding($false)))
        } catch {
            # La escritura del estado auxiliar nunca debe detener conversiones.
        }
    }

    function Receive-ControlCommand([string]$command) {
        if ($script:CancelRequested) { return }
        $normalized=($command.Trim()).ToUpperInvariant()
        switch ($normalized) {
            { $_ -in @('P','PAUSA','PAUSAR') } {
                if (-not $script:PauseRequested) {
                    $script:PauseRequested=$true
                    $script:Paused=$false
                    Set-ControlState 'PAUSANDO'
                    Log ('PAUSANDO: no arrancaran mas juegos. Esperando a que terminen '+$active.Count+' conversion(es) activa(s) sin interrumpir sus PBP.')
                }
                break
            }
            { $_ -in @('R','REANUDAR') } {
                if ($script:Paused) {
                    $script:PauseRequested=$false
                    $script:Paused=$false
                    Set-ControlState 'EN_CURSO'
                    Log 'REANUDADO: se procesaran los juegos pendientes; no se repiten los completados.'
                } elseif ($script:PauseRequested) {
                    Log 'Todavia PAUSANDO: termina primero la conversion de los juegos activos.' 'AVISO'
                }
                break
            }
            { $_ -in @('C','CANCELAR') } {
                $script:CancelRequested=$true
                $script:PauseRequested=$false
                $script:Paused=$false
                Set-ControlState 'CANCELANDO'
                Log ('CANCELANDO: se detendran '+$active.Count+' proceso(s) activos y no se iniciaran mas juegos. CHD originales intactos.') 'AVISO'
                break
            }
        }
    }

    function Poll-Controls {
        if ($ControlFile -and [System.IO.File]::Exists($ControlFile)) {
            try {
                $command=[System.IO.File]::ReadAllText($ControlFile).Trim()
                [System.IO.File]::Delete($ControlFile)
                if ($command) { Receive-ControlCommand $command }
            } catch {
                Log ('AVISO: no se pudo leer la orden externa: '+$_.Exception.Message) 'AVISO'
            }
        }
        if (-not $script:ConsoleControlsEnabled) { return }
        try {
            while ([Console]::KeyAvailable) {
                $key=[Console]::ReadKey($true)
                Receive-ControlCommand ([string]$key.KeyChar)
            }
        } catch {
            # En automatizaciones con stdin redirigido no hay teclado.
            $script:ConsoleControlsEnabled=$false
        }
    }

    function Start-Conversion([object]$job,[int]$ordinal) {
        $output=Join-Path $destDir ($job.Name+'.pbp')
        if ([System.IO.File]::Exists($output)) {
            $script:Skipped++
            Log ('['+$ordinal+'/'+$script:Jobs.Count+'] EXISTE, se omite: '+$job.Name) 'AVISO'
            return $null
        }
        $stage=Join-Path $tempRoot ('LNR_'+[guid]::NewGuid().ToString('N'))
        $process=$null
        try {
            [void](New-Item -ItemType Directory -Path $stage -Force)
            $inputFile=$job.Discs[0]
            if ($job.Discs.Count -gt 1) {
                # CHD absolutos: el lector del motor abre cada disco original.
                $inputFile=Join-Path $stage ($job.Name+'.m3u')
                [System.IO.File]::WriteAllLines($inputFile,[string[]]$job.Discs,(New-Object System.Text.UTF8Encoding($false)))
            }
            $info=New-Object System.Diagnostics.ProcessStartInfo
            $info.FileName=$cli
            $info.WorkingDirectory=$ToolsDir
            $info.UseShellExecute=$false
            $info.CreateNoWindow=$true
            $info.RedirectStandardOutput=$true
            $info.RedirectStandardError=$true
            # No hay expansion de shell ni consola; se citan las rutas.
            $info.Arguments=('-i {0} -o {1} -l {2} -s' -f (Quote-CliArg $inputFile),(Quote-CliArg $stage),$Level)
            $process=New-Object System.Diagnostics.Process
            $process.StartInfo=$info
            if (-not $process.Start()) { throw 'No se pudo iniciar psxpackager.exe' }
            $outTask=$process.StandardOutput.ReadToEndAsync()
            $errTask=$process.StandardError.ReadToEndAsync()
            Log ('['+$ordinal+'/'+$script:Jobs.Count+'] INICIADO: '+$job.Name+' ('+$job.Discs.Count+' disco(s))')
            return [pscustomobject]@{
                Job=$job; Ordinal=$ordinal; Output=$output; Stage=$stage
                Process=$process; StdoutTask=$outTask; StderrTask=$errTask
                Started=[DateTime]::UtcNow
            }
        } catch {
            $script:Failed++
            Log ('ERROR al iniciar '+$job.Name+': '+$_.Exception.Message) 'ERROR'
            if ($null -ne $process) {
                try {
                    if (-not $process.HasExited) {
                        $process.Kill()
                        [void]$process.WaitForExit(15000)
                    }
                } catch {}
                $process.Dispose()
            }
            if (Test-Path -LiteralPath $stage) {
                Remove-Item -LiteralPath $stage -Recurse -Force -ErrorAction SilentlyContinue
            }
            return $null
        }
    }

    function Finish-Conversion([object]$task) {
        $job=$task.Job
        $ordinal=$task.Ordinal
        $process=$task.Process
        try {
            # El proceso ya ha terminado; el final de los dos streams
            # siempre debe consumirse antes de leer ExitCode y limpiar.
            $stdout=$task.StdoutTask.GetAwaiter().GetResult()
            $stderr=$task.StderrTask.GetAwaiter().GetResult()
            $process.WaitForExit()
            if ($process.ExitCode -ne 0) {
                $details=if (-not [string]::IsNullOrWhiteSpace($stderr)) { $stderr.Trim() } else { $stdout.Trim() }
                if ($details.Length -gt 400) { $details=$details.Substring($details.Length-400) }
                throw ('La CLI devolvio '+$process.ExitCode+'. '+$details)
            }
            $candidates=@(Get-ChildItem -LiteralPath $task.Stage -File -Filter '*.pbp')
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
            if ([System.IO.File]::Exists($task.Output)) { throw 'Otro proceso ya creo el archivo de destino' }
            # File.Move falla si aparece un PBP de destino. Evita el
            # comportamiento de sobreescritura potencial de Move-Item.
            [System.IO.File]::Move($candidates[0].FullName,$task.Output)
            $a=[long]0
            foreach ($disc in $job.Discs) { $a+=(Get-Item -LiteralPath $disc).Length }
            $b=(Get-Item -LiteralPath $task.Output).Length
            $script:InputBytes+=$a; $script:OutputBytes+=$b; $script:OK++
            $elapsed=([DateTime]::UtcNow-$task.Started).TotalSeconds
            Log ('['+$ordinal+'/'+$script:Jobs.Count+'] OK: '+$job.Name+' | CHD '+(MB $a)+' -> PBP '+(MB $b)+' | diferencia '+(MB ($a-$b))+' | '+[Math]::Round($elapsed,1)+' s') 'OK'
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
        }
    }

    # El planificador corre solo en el hilo de PowerShell. Las tareas CLI
    # activas terminan siempre sin interrupcion durante PAUSANDO; la opcion C
    # cancela todas las tareas hijas y elimina solo temporales provisionales.
    try {
        while ($next -lt $script:Jobs.Count -or $active.Count -gt 0) {
            Poll-Controls
            if ($script:CancelRequested) { break }

            if (-not $script:PauseRequested) {
                while ($active.Count -lt $slots -and $next -lt $script:Jobs.Count) {
                    Poll-Controls
                    if ($script:PauseRequested -or $script:CancelRequested) { break }
                    $job=$script:Jobs[$next]
                    $next++
                    $task=Start-Conversion $job $next
                    if ($null -ne $task) { [void]$active.Add($task) }
                }
            }
            if ($script:CancelRequested) { break }

            # Incluso durante PAUSANDO finalizamos las tareas ya abiertas,
            # validamos los nuevos PBP, copiamos SBI y limpiamos el staging.
            $finished=@($active.ToArray() | Where-Object { $_.Process.HasExited })
            foreach ($task in $finished) {
                try { Finish-Conversion $task }
                finally {
                    [void]$active.Remove($task)
                    $task.Process.Dispose()
                    if (Test-Path -LiteralPath $task.Stage) {
                        Remove-Item -LiteralPath $task.Stage -Recurse -Force -ErrorAction SilentlyContinue
                    }
                }
            }

            if ($script:PauseRequested -and -not $script:Paused -and
                $active.Count -eq 0 -and $next -lt $script:Jobs.Count) {
                $script:Paused=$true
                Set-ControlState 'EN_PAUSA'
                Log ('EN PAUSA: 0 conversiones activas. '+($script:Jobs.Count-$next)+' pendiente(s). [R] Reanudar | [C] Cancelar.')
            }
            if (-not $script:PauseRequested -and $active.Count -gt 0 -and
                ([DateTime]::UtcNow-$lastStatus).TotalSeconds -ge 15) {
                Log ('EN CURSO: '+$active.Count+' conversion(es); pendientes de iniciar: '+($script:Jobs.Count-$next))
                $lastStatus=[DateTime]::UtcNow
            }
            if ($next -lt $script:Jobs.Count -or $active.Count -gt 0) {
                Start-Sleep -Milliseconds 150
            }
        }
    } finally {
        # Ctrl+C, excepcion o C: jamas borrar un staging si aun lo esta
        # escribiendo un proceso. Los CHD originales NO se tocan.
        foreach ($task in @($active.ToArray())) {
            $process=$task.Process
            $stopped=$false
            try {
                if (-not $process.HasExited) { $process.Kill() }
                $stopped=$process.WaitForExit(15000)
            } catch {
                Warn ('No se pudo confirmar la detencion de '+$task.Job.Name)
            }
            if ($stopped) {
                try {
                    [void]$task.StdoutTask.GetAwaiter().GetResult()
                    [void]$task.StderrTask.GetAwaiter().GetResult()
                } catch {}
                if (Test-Path -LiteralPath $task.Stage) {
                    Remove-Item -LiteralPath $task.Stage -Recurse -Force -ErrorAction SilentlyContinue
                }
            }
            $process.Dispose()
        }
        $active.Clear()
    }
    if ($script:CancelRequested) {
        Log ('CANCELADO POR EL USUARIO: '+$script:OK+' correctos; '+$next+' asignados; '+($script:Jobs.Count-$next)+' sin iniciar. No se borraron CHD de origen.') 'AVISO'
        Set-ControlState 'CANCELADO'
    } else {
        Set-ControlState 'TERMINADO'
    }
    Log ('FIN: OK '+$script:OK+' | Ya existentes '+$script:Skipped+' | Fallos '+$script:Failed+' | Avisos '+$script:Warnings.Count)
    Log ('CHD convertidos: '+(MB $script:InputBytes)+' | PBP generados: '+(MB $script:OutputBytes))
    if ($script:InputBytes -gt 0) {
        Log ('Variacion respecto a CHD: '+(MB ($script:InputBytes-$script:OutputBytes))+' ('+((100*($script:InputBytes-$script:OutputBytes)/$script:InputBytes).ToString('N2',$culture))+' %)')
    }
    [System.IO.File]::WriteAllLines($report,[string[]]$script:LogLines.ToArray(),(New-Object System.Text.UTF8Encoding($true)))
    if ($script:CancelRequested) { exit 3 }
    if ($script:Failed -gt 0) { exit 1 }
    exit 0
} catch {
    Log ('ERROR general: '+$_.Exception.Message) 'ERROR'
    exit 2
}

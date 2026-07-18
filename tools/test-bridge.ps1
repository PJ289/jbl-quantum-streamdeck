# Prueba el bridge contra Quantum Engine (requiere QuantumService + Q810 conectados).
param(
    [string]$BridgeExe = "",
    [switch]$Interactive,
    [switch]$SkipWrites
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path $PSScriptRoot -Parent
$QuantumPath = "C:\Program Files\JBL\QuantumENGINE"

if (-not $BridgeExe) {
    $candidates = @(
        "$ProjectRoot\bridge\bin\Release\net8.0-windows\win-x64\QuantumBridge.exe",
        "$ProjectRoot\bridge\bin\Release\net8.0-windows\QuantumBridge.exe",
        "$ProjectRoot\com.pj289.jbl-quantum.sdPlugin\bin\QuantumBridge.exe"
    )
    $BridgeExe = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
}

if (-not $BridgeExe -or -not (Test-Path $BridgeExe)) {
    Write-Host "QuantumBridge.exe no encontrado. Compila primero:" -ForegroundColor Yellow
    Write-Host "  dotnet build bridge/QuantumBridge.csproj -c Release"
    exit 1
}

function Invoke-BridgeCommand {
    param(
        [System.Diagnostics.Process]$Process,
        [string]$CommandJson,
        [string]$Label
    )

    Write-Host "`n>> $Label" -ForegroundColor Cyan
    Write-Host "   $CommandJson" -ForegroundColor DarkGray
    $Process.StandardInput.WriteLine($CommandJson)
    $Process.StandardInput.Flush()
    $line = $Process.StandardOutput.ReadLine()
    if (-not $line) {
        throw "Sin respuesta del bridge para: $Label"
    }
    Write-Host "   $line" -ForegroundColor Green
    $parsed = $line | ConvertFrom-Json
    if ($parsed.ok -eq $false) {
        throw $parsed.error
    }
    return $parsed
}

$service = Get-Service QuantumService -ErrorAction SilentlyContinue
if (-not $service -or $service.Status -ne "Running") {
    Write-Host "AVISO: QuantumService no está en ejecución." -ForegroundColor Yellow
}

Write-Host "Bridge: $BridgeExe"
Write-Host "Quantum Engine: $QuantumPath"

$psi = New-Object System.Diagnostics.ProcessStartInfo
$psi.FileName = $BridgeExe
$psi.WorkingDirectory = $QuantumPath
$psi.UseShellExecute = $false
$psi.RedirectStandardInput = $true
$psi.RedirectStandardOutput = $true
$psi.RedirectStandardError = $true
$psi.CreateNoWindow = $true

$proc = [System.Diagnostics.Process]::Start($psi)
if (-not $proc) {
    throw "No se pudo iniciar QuantumBridge.exe"
}

try {
    $ready = $proc.StandardOutput.ReadLine()
    if (-not $ready) {
        $stderr = $proc.StandardError.ReadToEnd()
        if ($stderr) {
            Write-Host $stderr -ForegroundColor Red
        }
        throw "El bridge terminó sin enviar 'ready'. ¿QuantumService en ejecución? ¿Cascos conectados?"
    }
    Write-Host "`nReady: $ready" -ForegroundColor Green
    $readyParsed = $ready | ConvertFrom-Json
    if ($readyParsed.ok -eq $false) {
        throw $readyParsed.error
    }

    Invoke-BridgeCommand $proc '{"cmd":"ping"}' "Ping"
    Invoke-BridgeCommand $proc '{"cmd":"get-status"}' "Estado (ANC + batería)"
    Invoke-BridgeCommand $proc '{"cmd":"get-anc"}' "Leer ANC"
    Invoke-BridgeCommand $proc '{"cmd":"get-battery"}' "Leer batería"

    if (-not $SkipWrites) {
        $originalAnc = (Invoke-BridgeCommand $proc '{"cmd":"get-anc"}' "ANC actual").anc
        Invoke-BridgeCommand $proc '{"cmd":"cycle-anc"}' "Cycle ANC"
        Start-Sleep -Seconds 2
        Invoke-BridgeCommand $proc '{"cmd":"set-anc","value":' + $originalAnc + '}' "Restaurar ANC original"
        Invoke-BridgeCommand $proc '{"cmd":"get-status"}' "Estado final"
    } else {
        Write-Host "`nOmitidas pruebas de escritura (-SkipWrites)." -ForegroundColor Yellow
    }

    if ($Interactive) {
        Write-Host "`nModo interactivo. Escribe JSON (cmd/value) o 'quit':" -ForegroundColor Cyan
        while ($true) {
            $input = Read-Host "bridge"
            if ($input -eq "quit") { break }
            if ([string]::IsNullOrWhiteSpace($input)) { continue }
            $proc.StandardInput.WriteLine($input)
            $proc.StandardInput.Flush()
            Write-Host $proc.StandardOutput.ReadLine()
        }
    }
}
finally {
    if (-not $proc.HasExited) {
        $proc.Kill()
        $proc.WaitForExit()
    }
}

Write-Host "`nPruebas completadas." -ForegroundColor Green

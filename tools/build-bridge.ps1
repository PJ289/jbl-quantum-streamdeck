# Compila QuantumBridge y copia SOLO el bridge al plugin (sin DLL de JBL).
#
# Local (por defecto): REQUIERE JBL Quantum Engine instalado (referencia de compilacion).
# CI / GitHub Actions: usa -AllowStubs (o env CI=true) para compilar sin Engine.
# En runtime SIEMPRE se cargan las DLL reales desde Quantum Engine instalado.
param(
    [string]$QuantumEnginePath = "",
    [switch]$AllowStubs
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path $PSScriptRoot -Parent
$PluginBin = Join-Path $ProjectRoot "com.pj289.jbl-quantum.sdPlugin\bin"
$BridgeOut = Join-Path $ProjectRoot "bridge\bin\Release\net8.0-windows\win-x64"

function Resolve-QuantumEnginePath {
    param([string]$Preferred)

    $candidates = @()
    if ($Preferred) { $candidates += $Preferred }
    if ($env:QUANTUM_ENGINE_PATH) { $candidates += $env:QUANTUM_ENGINE_PATH.Trim() }
    $candidates += @(
        "C:\Program Files\JBL\QuantumENGINE",
        "C:\Program Files (x86)\JBL\QuantumENGINE",
        "${env:ProgramFiles}\JBL\QuantumENGINE",
        "${env:ProgramFiles(x86)}\JBL\QuantumENGINE"
    )

    foreach ($path in $candidates) {
        if ([string]::IsNullOrWhiteSpace($path)) { continue }
        $normalized = $path.Trim().TrimEnd('\', '/')
        $dll = Join-Path $normalized "QuantumServer.dll"
        if (Test-Path $dll) {
            return $normalized
        }
    }

    return $null
}

$useStubs = $AllowStubs -or ($env:CI -eq "true") -or ($env:USE_QUANTUM_STUBS -eq "1")
$enginePath = Resolve-QuantumEnginePath -Preferred $QuantumEnginePath

Push-Location $ProjectRoot
try {
    if ($enginePath) {
        Write-Host "Compilando bridge con Quantum Engine: $enginePath" -ForegroundColor Cyan
        dotnet build bridge/QuantumBridge.csproj -c Release "-p:QuantumEnginePath=$enginePath" "-p:UseQuantumStubs=false"
    }
    elseif ($useStubs) {
        Write-Host "CI/AllowStubs: compilando bridge contra stubs (sin Quantum Engine en esta maquina)." -ForegroundColor Yellow
        Write-Host "El plugin publicado seguira necesitando Quantum Engine instalado en el PC del usuario." -ForegroundColor Yellow
        dotnet build bridge/stubs/QuantumServer.Stubs.csproj -c Release
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
        dotnet build bridge/QuantumBridge.csproj -c Release "-p:QuantumEnginePath=C:\__no_quantum_engine__" "-p:UseQuantumStubs=true"
    }
    else {
        Write-Host ""
        Write-Host "ERROR: No se encontro JBL Quantum Engine (QuantumServer.dll)." -ForegroundColor Red
        Write-Host "Para compilar en local necesitas Quantum Engine instalado." -ForegroundColor Red
        Write-Host ""
        Write-Host "Rutas buscadas:"
        Write-Host "  - `$env:QUANTUM_ENGINE_PATH"
        Write-Host "  - C:\Program Files\JBL\QuantumENGINE"
        Write-Host "  - C:\Program Files (x86)\JBL\QuantumENGINE"
        Write-Host ""
        Write-Host "Opciones:"
        Write-Host "  1) Instala Quantum Engine y vuelve a ejecutar."
        Write-Host "  2) Pasa la ruta:  .\tools\build-bridge.ps1 -QuantumEnginePath 'D:\ruta\QuantumENGINE'"
        Write-Host "  3) Solo CI:       .\tools\build-bridge.ps1 -AllowStubs"
        Write-Host ""
        exit 1
    }

    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    New-Item -ItemType Directory -Force -Path $PluginBin | Out-Null

    $files = @(
        "QuantumBridge.exe",
        "QuantumBridge.dll",
        "QuantumBridge.deps.json",
        "QuantumBridge.runtimeconfig.json"
    )

    foreach ($name in $files) {
        $src = Join-Path $BridgeOut $name
        if (-not (Test-Path $src)) {
            throw "Falta en build: $src"
        }
        Copy-Item $src $PluginBin -Force
    }

    $keep = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($name in $files) { [void]$keep.Add($name) }
    [void]$keep.Add("plugin.js")
    [void]$keep.Add("plugin.js.map")
    [void]$keep.Add("QuantumBridge.pdb")

    Get-ChildItem $PluginBin -File | ForEach-Object {
        if (-not $keep.Contains($_.Name)) {
            Remove-Item $_.FullName -Force
            Write-Host "Eliminado del paquete: $($_.Name)" -ForegroundColor Yellow
        }
    }

    Write-Host "Bridge copiado a $PluginBin (sin DLL de Quantum Engine)" -ForegroundColor Green
}
finally {
    Pop-Location
}

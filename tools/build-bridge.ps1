# Compila QuantumBridge y copia SOLO el bridge al plugin (sin DLL de JBL).
# Si Quantum Engine no esta instalado, compila contra stubs (CI).
# En runtime, QuantumBridge carga las DLL reales desde Quantum Engine.
param(
    [string]$QuantumEnginePath = "C:\Program Files\JBL\QuantumENGINE"
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path $PSScriptRoot -Parent
$PluginBin = Join-Path $ProjectRoot "com.pj289.jbl-quantum.sdPlugin\bin"
$BridgeOut = Join-Path $ProjectRoot "bridge\bin\Release\net8.0-windows\win-x64"

$QuantumEnginePath = $QuantumEnginePath.TrimEnd('\', '/')
$hasEngine = Test-Path (Join-Path $QuantumEnginePath "QuantumServer.dll")

Push-Location $ProjectRoot
try {
    if ($hasEngine) {
        Write-Host "Compilando bridge con Quantum Engine: $QuantumEnginePath" -ForegroundColor Cyan
        dotnet build bridge/QuantumBridge.csproj -c Release "-p:QuantumEnginePath=$QuantumEnginePath"
    }
    else {
        Write-Host "Quantum Engine no encontrado - compilando contra stubs (CI)." -ForegroundColor Yellow
        dotnet build bridge/stubs/QuantumServer.Stubs.csproj -c Release
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
        dotnet build bridge/QuantumBridge.csproj -c Release "-p:QuantumEnginePath=C:\__no_quantum_engine__" "-p:UseQuantumStubs=true"
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

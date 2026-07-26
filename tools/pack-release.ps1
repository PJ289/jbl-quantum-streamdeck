# Empaqueta el plugin listo para descargar (sin DLL de JBL).
# Salida: dist/com.pj289.jbl-quantum.sdPlugin.zip  y  .streamDeckPlugin
param(
    [string]$QuantumEnginePath = "",
    [string]$Version = "",
    [switch]$AllowStubs
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path $PSScriptRoot -Parent
$PluginName = "com.pj289.jbl-quantum.sdPlugin"
$PluginSrc = Join-Path $ProjectRoot $PluginName
$Dist = Join-Path $ProjectRoot "dist"
$Stage = Join-Path $Dist "stage\$PluginName"

Push-Location $ProjectRoot
try {
    if (-not (Test-Path (Join-Path $ProjectRoot "node_modules"))) {
        npm install
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }

    $bridgeArgs = @()
    if ($QuantumEnginePath) { $bridgeArgs += @("-QuantumEnginePath", $QuantumEnginePath) }
    if ($AllowStubs -or $env:CI -eq "true") { $bridgeArgs += "-AllowStubs" }

    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot "build-bridge.ps1") @bridgeArgs
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    npm run build
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot "ensure-icons.ps1")

    if (-not $Version) {
        $manifest = Get-Content (Join-Path $PluginSrc "manifest.json") -Raw | ConvertFrom-Json
        $Version = [string]$manifest.Version
    }

    if (Test-Path (Join-Path $Dist "stage")) {
        Remove-Item (Join-Path $Dist "stage") -Recurse -Force
    }
    New-Item -ItemType Directory -Force -Path $Stage | Out-Null

    # Copiar plugin limpio
    Copy-Item (Join-Path $PluginSrc "manifest.json") $Stage -Force
    Copy-Item (Join-Path $PluginSrc "ui") (Join-Path $Stage "ui") -Recurse -Force
    Copy-Item (Join-Path $PluginSrc "imgs") (Join-Path $Stage "imgs") -Recurse -Force
    New-Item -ItemType Directory -Force -Path (Join-Path $Stage "bin") | Out-Null

    $binKeep = @(
        "plugin.js",
        "QuantumBridge.exe",
        "QuantumBridge.dll",
        "QuantumBridge.deps.json",
        "QuantumBridge.runtimeconfig.json"
    )
    foreach ($name in $binKeep) {
        $src = Join-Path $PluginSrc "bin\$name"
        if (-not (Test-Path $src)) {
            throw "Falta en el plugin: bin\$name"
        }
        Copy-Item $src (Join-Path $Stage "bin\$name") -Force
    }

    # Seguridad: nunca incluir DLL de Quantum Engine
    foreach ($name in @("QuantumServer.dll", "IPC.dll", "ShareMemory.dll")) {
        $bad = Join-Path $Stage "bin\$name"
        if (Test-Path $bad) {
            Remove-Item $bad -Force
            Write-Host "Eliminado del paquete: $name" -ForegroundColor Yellow
        }
    }

    New-Item -ItemType Directory -Force -Path $Dist | Out-Null
    $zipName = "jbl-quantum-streamdeck-$Version.zip"
    $pluginName = "jbl-quantum-streamdeck-$Version.streamDeckPlugin"
    $zipPath = Join-Path $Dist $zipName
    $sdPluginPath = Join-Path $Dist $pluginName

    if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
    if (Test-Path $sdPluginPath) { Remove-Item $sdPluginPath -Force }

    # ZIP con la carpeta .sdPlugin en la raíz (instalación manual)
    Compress-Archive -Path $Stage -DestinationPath $zipPath -Force

    # .streamDeckPlugin = mismo zip (doble clic en Stream Deck / instalar)
    Copy-Item $zipPath $sdPluginPath -Force

    Write-Host ""
    Write-Host "Paquetes listos:" -ForegroundColor Green
    Write-Host "  $zipPath"
    Write-Host "  $sdPluginPath"
    Write-Host ""
    Write-Host "Instalación manual: descomprime el ZIP en:"
    Write-Host "  %APPDATA%\Elgato\StreamDeck\Plugins\"
    Write-Host "Requisito: JBL Quantum Engine + .NET 8 Desktop Runtime."
}
finally {
    Pop-Location
}

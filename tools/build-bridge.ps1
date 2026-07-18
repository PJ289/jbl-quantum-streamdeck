# Compila QuantumBridge y copia SOLO el bridge al plugin (sin DLL de JBL).
# En runtime, QuantumBridge carga QuantumServer.dll / IPC.dll / ShareMemory.dll
# desde la instalación de Quantum Engine.
param(
    [string]$QuantumEnginePath = "C:\Program Files\JBL\QuantumENGINE"
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path $PSScriptRoot -Parent
$PluginBin = Join-Path $ProjectRoot "com.pj289.jbl-quantum.sdPlugin\bin"
$BridgeOut = Join-Path $ProjectRoot "bridge\bin\Release\net8.0-windows\win-x64"

$QuantumEnginePath = $QuantumEnginePath.TrimEnd('\', '/')

if (-not (Test-Path (Join-Path $QuantumEnginePath "QuantumServer.dll"))) {
    throw "QuantumServer.dll no encontrado en: $QuantumEnginePath`nInstala JBL Quantum Engine o pasa -QuantumEnginePath."
}

Push-Location $ProjectRoot
try {
    # Quote path for spaces (Program Files). No trailing backslash (MSBuild strips it).
    dotnet build bridge/QuantumBridge.csproj -c Release "-p:QuantumEnginePath=$QuantumEnginePath"
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    New-Item -ItemType Directory -Force -Path $PluginBin | Out-Null

    # Solo nuestro bridge. Las DLL de JBL/Harman NO se empaquetan.
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

    # Quitar basura / DLL ajenas (builds antiguos, dependencias copiadas por error).
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

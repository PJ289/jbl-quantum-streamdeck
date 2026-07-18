# Compila e instala el plugin en Stream Deck (desarrollo local).
$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path $PSScriptRoot -Parent
$PluginName = "com.pj289.jbl-quantum.sdPlugin"
$Src = Join-Path $ProjectRoot $PluginName
$Dest = Join-Path $env:APPDATA "Elgato\StreamDeck\Plugins\$PluginName"

Push-Location $ProjectRoot
try {
    if (-not (Test-Path (Join-Path $ProjectRoot "node_modules"))) {
        npm install
    }

    npm run build:bridge
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    npm run build
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot "ensure-icons.ps1")

    New-Item -ItemType Directory -Force -Path $Dest | Out-Null

    # Prefer in-place copy: Stream Deck may lock the plugin folder while running.
    $robocopy = Get-Command robocopy -ErrorAction SilentlyContinue
    if ($robocopy) {
        & robocopy $Src $Dest /E /IS /IT /NFL /NDL /NJH /NJS /nc /ns /np | Out-Null
        # robocopy exit codes 0-7 are success
        if ($LASTEXITCODE -ge 8) {
            throw "robocopy failed with exit code $LASTEXITCODE"
        }
        $global:LASTEXITCODE = 0
    }
    else {
        Copy-Item -Path (Join-Path $Src "*") -Destination $Dest -Recurse -Force
    }

    # Nunca dejar DLL de JBL/Harman en la carpeta instalada del plugin.
    $vendorDlls = @("QuantumServer.dll", "IPC.dll", "ShareMemory.dll")
    foreach ($name in $vendorDlls) {
        $stale = Join-Path $Dest "bin\$name"
        if (Test-Path $stale) {
            Remove-Item $stale -Force -ErrorAction SilentlyContinue
            Write-Host "Eliminado de la instalación (no redistribuir): $name" -ForegroundColor Yellow
        }
    }

    Write-Host ""
    Write-Host "Plugin instalado/actualizado en:" -ForegroundColor Green
    Write-Host "  $Dest"
    Write-Host ""
    Write-Host "Requisito: JBL Quantum Engine instalado (las DLL se leen de ahí en runtime)."
    Write-Host "Reinicia Stream Deck para cargar los cambios."
    Write-Host "Si un archivo sigue bloqueado, cierra Stream Deck y vuelve a ejecutar: npm run install:plugin"
}
finally {
    Pop-Location
}

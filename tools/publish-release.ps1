# Publica una release REAL en GitHub (compilada contra JBL Quantum Engine real).
#
# CI (GitHub Actions) NO puede hacer esto: no puede tener instalado el software
# propietario de JBL, así que solo compila contra stubs (chequeo de sintaxis).
# Un binario compilado contra los stubs falla en tiempo de ejecución
# (TypeLoadException) al cargar la DLL real, porque .NET exige que el layout
# de tipos coincida exactamente para las llamadas resueltas en compilación.
#
# Por eso las releases publicadas para los usuarios se generan SIEMPRE en una
# máquina real con Quantum Engine instalado (este script), nunca en CI.
#
# Requisitos: JBL Quantum Engine instalado, .NET 8 SDK, GitHub CLI autenticado
# (`gh auth login`).
param(
    [Parameter(Mandatory = $true)]
    [string]$Version,
    [string]$QuantumEnginePath = "",
    [switch]$Draft
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path $PSScriptRoot -Parent

Push-Location $ProjectRoot
try {
    $gh = Get-Command gh -ErrorAction SilentlyContinue
    if (-not $gh) {
        throw "GitHub CLI (gh) no encontrado. Instálalo o publica manualmente los archivos de dist/."
    }

    $bridgeArgs = @()
    if ($QuantumEnginePath) { $bridgeArgs += @("-QuantumEnginePath", $QuantumEnginePath) }

    Write-Host "Compilando bridge contra Quantum Engine REAL (no stubs)..." -ForegroundColor Cyan
    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot "pack-release.ps1") -Version $Version @bridgeArgs
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    $zip = Join-Path $ProjectRoot "dist\jbl-quantum-streamdeck-$Version.zip"
    $sdp = Join-Path $ProjectRoot "dist\jbl-quantum-streamdeck-$Version.streamDeckPlugin"
    if (-not (Test-Path $zip) -or -not (Test-Path $sdp)) {
        throw "No se generaron los artefactos esperados en dist/."
    }

    # Sanity check: el .exe recién compilado debe arrancar y conectar de verdad.
    Write-Host "Verificando que el bridge conecta con Quantum Engine..." -ForegroundColor Cyan
    $bridgeExe = Join-Path $ProjectRoot "com.pj289.jbl-quantum.sdPlugin\bin\QuantumBridge.exe"
    $enginePath = if ($QuantumEnginePath) { $QuantumEnginePath } else { "C:\Program Files\JBL\QuantumENGINE" }
    $probe = Start-Process -FilePath $bridgeExe -WorkingDirectory $enginePath -RedirectStandardOutput "$env:TEMP\qe-bridge-probe.json" -PassThru -WindowStyle Hidden
    Start-Sleep -Seconds 3
    if (-not $probe.HasExited) { Stop-Process -Id $probe.Id -Force -ErrorAction SilentlyContinue }
    $probeOutput = Get-Content "$env:TEMP\qe-bridge-probe.json" -Raw -ErrorAction SilentlyContinue
    if ($probeOutput -notmatch '"ok"\s*:\s*true') {
        Write-Host $probeOutput -ForegroundColor Red
        throw "El bridge no respondio ok:true. Revisa Quantum Engine / el Q810 antes de publicar."
    }
    Write-Host "OK: $probeOutput" -ForegroundColor Green

    $tag = "v$Version"
    $notes = @"
## Instalación (usuarios)

1. Instala **JBL Quantum Engine** y el [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0).
2. Descarga ``*.streamDeckPlugin`` **o** el ``.zip``.
3. Opción A: abre el ``.streamDeckPlugin`` (Stream Deck lo instala).
4. Opción B: descomprime el ZIP en ``%APPDATA%\Elgato\StreamDeck\Plugins\``.
5. Reinicia Stream Deck.

**No necesitas Node.js** para usar el plugin.

Plugin **no oficial** — no afiliado a JBL/Harman/Elgato. No incluye DLL de Quantum Engine; las carga de tu instalación.
"@

    $ghArgs = @("release", "create", $tag, $zip, $sdp, "--title", $tag, "--notes", $notes)
    if ($Draft) { $ghArgs += "--draft" }

    Write-Host "Publicando release $tag en GitHub..." -ForegroundColor Cyan
    & gh @ghArgs
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    Write-Host "Release publicada: $tag" -ForegroundColor Green
}
finally {
    Pop-Location
}

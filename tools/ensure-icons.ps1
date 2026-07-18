# Genera PNGs 72x72 limpios para el plugin (sin texto largo del nombre de archivo).
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$ProjectRoot = Split-Path $PSScriptRoot -Parent
$Root = Join-Path $ProjectRoot "com.pj289.jbl-quantum.sdPlugin\imgs"

function New-Icon([string]$rel, [Drawing.Color]$bg, [string]$label, [Drawing.Color]$fg) {
    $path = Join-Path $Root $rel
    $dir = Split-Path $path -Parent
    New-Item -ItemType Directory -Force -Path $dir | Out-Null

    $bmp = New-Object Drawing.Bitmap 72, 72
    $g = [Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear($bg)

    # Soft inner panel
    $panel = New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(40, 255, 255, 255))
    $g.FillEllipse($panel, 8, 8, 56, 56)
    $panel.Dispose()

    $fontSize = if ($label.Length -le 3) { 14 } else { 11 }
    $font = New-Object Drawing.Font "Segoe UI", $fontSize, ([Drawing.FontStyle]::Bold)
    $brush = New-Object Drawing.SolidBrush $fg
    $sf = New-Object Drawing.StringFormat
    $sf.Alignment = [Drawing.StringAlignment]::Center
    $sf.LineAlignment = [Drawing.StringAlignment]::Center
    $g.DrawString($label, $font, $brush, (New-Object Drawing.RectangleF 0, 0, 72, 72), $sf)

    $bmp.Save($path, [Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose(); $font.Dispose(); $brush.Dispose(); $sf.Dispose()
    Write-Host "Icono: $rel ($label)"
}

# Always regenerate so updates apply.
New-Icon "plugin\category.png"     ([Drawing.Color]::FromArgb(255, 28, 28, 30))  "JBL"  ([Drawing.Color]::White)
New-Icon "plugin\marketplace.png"  ([Drawing.Color]::FromArgb(255, 180, 30, 40))  "Q"    ([Drawing.Color]::White)
New-Icon "actions\cycle-anc.png"   ([Drawing.Color]::FromArgb(255, 35, 100, 200)) "ANC"  ([Drawing.Color]::White)
New-Icon "actions\toggle-anc.png"  ([Drawing.Color]::FromArgb(255, 40, 130, 170)) "A/O"  ([Drawing.Color]::White)
New-Icon "actions\anc-off.png"     ([Drawing.Color]::FromArgb(255, 70, 70, 75))   "OFF"  ([Drawing.Color]::White)
New-Icon "actions\anc-on.png"      ([Drawing.Color]::FromArgb(255, 30, 150, 80))  "ON"   ([Drawing.Color]::White)
New-Icon "actions\anc-ambient.png" ([Drawing.Color]::FromArgb(255, 200, 140, 30)) "AMB"  ([Drawing.Color]::White)
New-Icon "actions\vol-up.png"      ([Drawing.Color]::FromArgb(255, 45, 120, 180)) "MIC+" ([Drawing.Color]::White)
New-Icon "actions\vol-down.png"    ([Drawing.Color]::FromArgb(255, 40, 90, 150))  "MIC-" ([Drawing.Color]::White)
New-Icon "actions\game.png"          ([Drawing.Color]::FromArgb(255, 120, 50, 180)) "GAME" ([Drawing.Color]::White)
New-Icon "actions\chat.png"          ([Drawing.Color]::FromArgb(255, 25, 150, 160)) "CHAT" ([Drawing.Color]::White)
New-Icon "actions\profile-cycle.png" ([Drawing.Color]::FromArgb(255, 90, 60, 160))  "PROF" ([Drawing.Color]::White)
New-Icon "actions\profile-set.png"   ([Drawing.Color]::FromArgb(255, 60, 80, 170))  "SET"  ([Drawing.Color]::White)

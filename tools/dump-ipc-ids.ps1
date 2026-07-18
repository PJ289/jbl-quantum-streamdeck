# Extrae la tabla IPC_MSG_ID_LIST de QuantumServer.dll
param(
    [string]$DllPath = "C:\Program Files\JBL\QuantumENGINE\QuantumServer.dll",
    [string]$OutJson = "$PSScriptRoot\..\docs\ipc-message-ids.json"
)

$ErrorActionPreference = "Stop"
$asm = [Reflection.Assembly]::LoadFrom($DllPath)
$field = $asm.GetType("QECommon.IPCMSGDebugInfo").GetField(
    "IPC_MSG_ID_LIST",
    [Reflection.BindingFlags]"Public,NonPublic,Static"
)
$list = $field.GetValue($null)
$entries = foreach ($key in $list.Keys) {
    [PSCustomObject]@{ id = [int]$key; name = [string]$list[$key] }
}
$entries | Sort-Object id | ConvertTo-Json -Depth 2 | Set-Content $OutJson -Encoding UTF8
Write-Host "Exported $($entries.Count) message IDs to $OutJson"

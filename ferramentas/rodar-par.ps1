# Compila, instala numa copia do jogo e abre host + cliente (UDP local) com auto-inicio; espera os dois fantasmas.
#   rodar-par.ps1 [-Copia <pasta do jogo>] [-Porta 27450] [-SemBuild]
# So fecha processos do jogo que rodam a partir de -Copia (outras sessoes podem estar usando outra copia).
param([string]$Copia = "", [int]$Porta = 27450, [switch]$SemBuild, [string]$ClasseHost = "", [string]$ClasseCliente = "")
$WS = Split-Path $PSScriptRoot
$T = if ($Copia) { $Copia } else { "$WS\trabalho\Spark in the Dark" }
$R = "$WS\reimpl\SparkCoop"; $L = Join-Path $T "testes-logs"
Get-Process "Spark in the Dark" -ErrorAction SilentlyContinue | ? { $_.Path -and $_.Path.StartsWith($T, [StringComparison]::OrdinalIgnoreCase) } | Stop-Process -Force
Start-Sleep 3
if (-not $SemBuild) {
    $dll = Join-Path $T "BepInEx\plugins\SparkCoop\SparkCoop.dll"
    & powershell -NoProfile -ExecutionPolicy Bypass -File "$R\build.ps1" -Jogo $T -Saida "$T\..\SparkCoop-build\SparkCoop.dll" | Out-Null
    if ($LASTEXITCODE) { "BUILD FALHOU"; & powershell -NoProfile -ExecutionPolicy Bypass -File "$R\build.ps1" -Jogo $T -Saida "$T\..\SparkCoop-build\SparkCoop.dll"; exit 1 }
    New-Item -ItemType Directory -Force (Split-Path $dll) | Out-Null
    Copy-Item "$T\..\SparkCoop-build\SparkCoop.dll" $dll -Force
}
Remove-Item "$T\BepInEx\SparkCoop-*.cmd", "$T\BepInEx\SparkCoop-*.log" -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $L | Out-Null
$common = "-screen-fullscreen 0 -screen-width 1280 -screen-height 720 -coop-autostart 1 -coop-nosave 1 -coop-port $Porta"
Start-Process -FilePath "$T\Spark in the Dark.exe" -WorkingDirectory $T -ArgumentList "$common -coop-role host $(if ($ClasseHost) { "-coop-classe $ClasseHost" }) -logFile `"$L\player-host.log`"" | Out-Null
Start-Sleep 6
Start-Process -FilePath "$T\Spark in the Dark.exe" -WorkingDirectory $T -ArgumentList "$common -coop-role client -coop-host 127.0.0.1 $(if ($ClasseCliente) { "-coop-classe $ClasseCliente" }) -logFile `"$L\player-client.log`"" | Out-Null
$d = (Get-Date).AddSeconds(240)
$logs = "$T\BepInEx\SparkCoop-client.log", "$T\BepInEx\SparkCoop-host.log"
function Contar { @(Get-Content $logs -ErrorAction SilentlyContinue | Select-String "fantasma do outro").Count }
while ((Get-Date) -lt $d -and (Contar) -lt 2) { Start-Sleep 3 }
"pronto: " + (Contar) + " fantasmas"

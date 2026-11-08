# Envia um comando de teste para uma instancia e mostra as linhas novas do log dela (e do outro lado).
#   cmd.ps1 -Papel host|client -Comando "die corpo" [-Espera 4] [-Copia <pasta do jogo>]
param([string]$Papel, [string]$Comando, [double]$Espera = 4, [string]$Copia = "")
$WS = Split-Path $PSScriptRoot
$T = if ($Copia) { $Copia } else { "$WS\trabalho\Spark in the Dark" }
$logs = @{ host = "$T\BepInEx\SparkCoop-host.log"; client = "$T\BepInEx\SparkCoop-client.log" }
$antes = @{}; foreach ($k in $logs.Keys) { $antes[$k] = @(Get-Content $logs[$k] -ErrorAction SilentlyContinue).Count }
Set-Content "$T\BepInEx\SparkCoop-$Papel.cmd" $Comando
Start-Sleep -Milliseconds ([int]($Espera * 1000))
foreach ($k in @("host", "client")) {
    $novas = @(Get-Content $logs[$k] -ErrorAction SilentlyContinue | Select-Object -Skip $antes[$k] | ? { $_ -notmatch 'fantasma: alvo' })
    foreach ($l in $novas) { "[$k] $l" }
}

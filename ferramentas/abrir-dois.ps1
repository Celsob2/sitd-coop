# Abre duas janelas do jogo (host + cliente) na copia de trabalho, ligadas pelo SparkCoop via rede local.
# Antes, faz um backup datado dos seus saves. Uso:
#   abrir-dois.ps1            saves funcionam normalmente
#   abrir-dois.ps1 -SemSalvar o jogo nao grava saves nem sincroniza a Steam Cloud (recomendado para gravar video)
param([switch]$SemSalvar, [int]$Largura = 1280, [int]$Altura = 720)
$WS = Split-Path $PSScriptRoot
$T = "$WS\trabalho\Spark in the Dark"
$P = "$env:USERPROFILE\AppData\LocalLow\Stellar Fish\Spark in the Dark"
$bk = "$WS\backup\saves-" + (Get-Date -Format "yyyyMMdd-HHmmss")
robocopy $P $bk /E /NFL /NDL /NJH /NJS /NP | Out-Null
"backup dos saves: $bk"
$extra = "-screen-fullscreen 0 -screen-width $Largura -screen-height $Altura" + $(if ($SemSalvar) { " -coop-nosave 1" } else { "" })
Start-Process -FilePath "$T\Spark in the Dark.exe" -WorkingDirectory $T -ArgumentList "$extra -coop-role host"
Start-Sleep 6
Start-Process -FilePath "$T\Spark in the Dark.exe" -WorkingDirectory $T -ArgumentList "$extra -coop-role client -coop-host 127.0.0.1"
"abertas: host e cliente. Espere 'conectado a ...' no painel do canto superior esquerdo das duas janelas."

# Instala ou remove o SparkCoop de forma reversivel.
#   instalar.ps1 -Jogo "<pasta do jogo>"            instala (exige BepInEx 6 Mono ja presente)
#   instalar.ps1 -Jogo "<pasta do jogo>" -Remover   remove tudo o que a instalacao criou
# A instalacao so ADICIONA arquivos; nada do jogo e sobrescrito. Um registro em
# BepInEx\plugins\SparkCoop\instalacao.json lista o que foi criado, para a remocao.
param([Parameter(Mandatory = $true)][string]$Jogo, [switch]$Remover)
$ErrorActionPreference = "Stop"
$dest = Join-Path $Jogo "BepInEx\plugins\SparkCoop"
$cfg = Join-Path $Jogo "BepInEx\config\local.sparkcoop.cfg"
$registro = Join-Path $dest "instalacao.json"

if ($Remover) {
    if (Test-Path $dest) { Remove-Item -Recurse -Force $dest; "removido: $dest" }
    if (Test-Path $cfg) { Remove-Item -Force $cfg; "removido: $cfg" }
    Get-ChildItem (Join-Path $Jogo "BepInEx") -Filter "SparkCoop-*.log" -ErrorAction SilentlyContinue | % { Remove-Item -Force $_.FullName; "removido: $($_.FullName)" }
    "SparkCoop removido. O BepInEx e os demais mods ficaram como estavam."
    return
}

if (-not (Test-Path (Join-Path $Jogo "BepInEx\core\BepInEx.Unity.Mono.dll"))) {
    throw "BepInEx 6 (Unity Mono) nao encontrado em $Jogo. Instale o BepInEx 6 primeiro (ver LEIA-ME.md)."
}
$dll = Join-Path $PSScriptRoot "bin\SparkCoop.dll"
if (-not (Test-Path $dll)) { throw "bin\SparkCoop.dll nao existe; rode build.ps1 antes." }
New-Item -ItemType Directory -Force $dest | Out-Null
Copy-Item $dll $dest -Force
$sha = (Get-FileHash $dll -Algorithm SHA256).Hash
@{ instalado_em = (Get-Date).ToString("s"); arquivos = @("BepInEx\plugins\SparkCoop\SparkCoop.dll", "BepInEx\config\local.sparkcoop.cfg (criado no 1o uso)"); sha256 = $sha } |
    ConvertTo-Json | Set-Content -Encoding UTF8 $registro
"instalado em $dest (SHA-256 $sha)"

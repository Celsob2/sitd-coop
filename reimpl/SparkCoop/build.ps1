# Compila SparkCoop.dll (todos os *.cs desta pasta) com o csc do .NET Framework 4 contra os assemblies de uma copia do jogo.
#   build.ps1 [-Jogo <pasta do jogo>] [-Saida <dll>]
param([string]$Jogo = "$PSScriptRoot\..\..\trabalho\Spark in the Dark", [string]$Saida = "$PSScriptRoot\bin\SparkCoop.dll")
$ErrorActionPreference = "Stop"
$csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$M = Join-Path $Jogo "Spark in the Dark_Data\Managed"
$B = Join-Path $Jogo "BepInEx\core"
$refs = @("mscorlib","System","System.Core","netstandard","UnityEngine","UnityEngine.CoreModule","UnityEngine.AnimationModule",
          "UnityEngine.IMGUIModule","UnityEngine.PhysicsModule","UnityEngine.UI","UnityEngine.TextRenderingModule",
          "UnityEngine.InputLegacyModule","UnityEngine.JSONSerializeModule","UnityEngine.AIModule","Unity.InputSystem","com.rlabrecque.steamworks.net","Assembly-CSharp") | % { "/r:$M\$_.dll" }
$refs += @("BepInEx.Core","BepInEx.Unity.Mono","0Harmony") | % { "/r:$B\$_.dll" }
New-Item -ItemType Directory -Force (Split-Path $Saida) | Out-Null
$fontes = @(Get-ChildItem $PSScriptRoot -Filter *.cs | % { $_.FullName })
& $csc /nologo /noconfig /nostdlib+ /target:library /optimize+ /langversion:5 "/out:$Saida" @refs @fontes
if ($LASTEXITCODE -ne 0) { throw "falha na compilacao" }
Get-Item $Saida | Select-Object FullName, Length

# Coloca as janelas do jogo da copia indicada lado a lado (so move; o tamanho vem de -coop-janela no jogo).
#   lado-a-lado.ps1 [-Copia <pasta do jogo>]
param([string]$Copia = "")
$WS = Split-Path $PSScriptRoot
$T = if ($Copia) { $Copia } else { "$WS\trabalho-steam\Spark in the Dark" }
Add-Type @"
using System; using System.Runtime.InteropServices;
public class J { [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
 [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int w, int hh, uint f); }
"@
[J]::SetProcessDPIAware() | Out-Null
$ps = @(Get-CimInstance Win32_Process -Filter "Name='Spark in the Dark.exe'" | ? { $_.ExecutablePath -like "$T*" })
# host primeiro (esquerda), cliente depois (direita)
$ps = $ps | Sort-Object { if ($_.CommandLine -like '*coop-role host*') { 0 } else { 1 } }
$x = 0
foreach ($p in $ps) {
    $h = (Get-Process -Id $p.ProcessId).MainWindowHandle
    if ($h -eq [IntPtr]::Zero) { continue }
    [J]::SetWindowPos($h, [IntPtr]::Zero, $x, 0, 0, 0, 0x0001 -bor 0x0004) | Out-Null   # SWP_NOSIZE | SWP_NOZORDER
    "janela $($p.ProcessId) em x=$x"
    $x += 1940
}

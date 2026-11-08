# Captura cada janela do jogo em PNG via PrintWindow (funciona mesmo com janelas sobrepostas).
# Uso: captura.ps1 -Saida <pasta> [-Prefixo nome]
param([string]$Saida, [string]$Prefixo = "tela")
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System; using System.Runtime.InteropServices;
public class W { [StructLayout(LayoutKind.Sequential)] public struct R { public int L, T, Ri, B; }
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out R r);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags); }
"@
New-Item -ItemType Directory -Force $Saida | Out-Null
foreach ($p in Get-Process "Spark in the Dark" -ErrorAction SilentlyContinue) {
    $h = $p.MainWindowHandle; if ($h -eq [IntPtr]::Zero) { continue }
    $r = New-Object W+R; [W]::GetWindowRect($h, [ref]$r) | Out-Null
    $w = $r.Ri - $r.L; $hh = $r.B - $r.T; if ($w -le 0) { continue }
    $bmp = New-Object System.Drawing.Bitmap $w, $hh
    $g = [System.Drawing.Graphics]::FromImage($bmp); $hdc = $g.GetHdc()
    [W]::PrintWindow($h, $hdc, 2) | Out-Null   # PW_RENDERFULLCONTENT
    $g.ReleaseHdc($hdc)
    $f = Join-Path $Saida ("{0}-{1}.png" -f $Prefixo, $p.Id); $bmp.Save($f); $g.Dispose(); $bmp.Dispose(); $f
}

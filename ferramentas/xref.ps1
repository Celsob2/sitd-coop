# Referencias cruzadas por IL (Mono.Cecil): quem usa um membro, e IL de metodos escolhidos.
# Uso: xref.ps1 -Cecil <dll> -Asm <dll> [-Membro <regex do operando>] [-Metodo <regex Tipo::Metodo para despejar IL>]
param([string]$Cecil, [string]$Asm, [string]$Membro = "", [string]$Metodo = "")
Add-Type -Path $Cecil
$res = New-Object Mono.Cecil.DefaultAssemblyResolver
$res.AddSearchDirectory((Split-Path $Asm))
$rp = New-Object Mono.Cecil.ReaderParameters
$rp.AssemblyResolver = $res
$mod = [Mono.Cecil.ModuleDefinition]::ReadModule($Asm, $rp)
$hits = @{}
foreach ($t in $mod.GetTypes()) {
    foreach ($m in $t.Methods) {
        if (-not $m.HasBody) { continue }
        $nome = "$($t.FullName)::$($m.Name)"
        if ($Metodo -and $nome -match $Metodo) {
            "=== $nome"
            foreach ($i in $m.Body.Instructions) { "  $i" }
        }
        if ($Membro) {
            foreach ($i in $m.Body.Instructions) {
                if ($i.Operand -ne $null -and "$($i.Operand)" -match $Membro) { $hits[$nome] = 1 + [int]$hits[$nome] }
            }
        }
    }
}
if ($Membro) {
    "--- $($hits.Count) metodos referenciam /$Membro/ ($(($hits.Values | Measure-Object -Sum).Sum) ocorrencias)"
    $porTipo = $hits.Keys | Group-Object { $_.Split(':')[0] } | Sort-Object Count -Descending
    "--- $($porTipo.Count) tipos distintos; top:"
    $porTipo | Select-Object -First 25 | % { "  $($_.Count)  $($_.Name)" }
    "--- metodos:"
    $hits.Keys | Sort-Object | Select-Object -First 60 | % { "  $_" }
}

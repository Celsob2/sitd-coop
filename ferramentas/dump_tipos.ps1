# Lista tipos, campos e metodos de um assembly .NET usando Mono.Cecil (sem executar o alvo).
# Uso: powershell -File dump_tipos.ps1 -Cecil <Mono.Cecil.dll> -Asm <assembly> -Saida <arquivo> [-Il <regex de tipo para despejar IL>]
param([string]$Cecil, [string]$Asm, [string]$Saida, [string]$Il = "")
Add-Type -Path $Cecil
$rp = New-Object Mono.Cecil.ReaderParameters
$res = New-Object Mono.Cecil.DefaultAssemblyResolver
$res.AddSearchDirectory((Split-Path $Asm))
$rp.AssemblyResolver = $res
$mod = [Mono.Cecil.ModuleDefinition]::ReadModule($Asm, $rp)
$sb = New-Object System.Text.StringBuilder
function Walk($t) {
    $base = if ($t.BaseType) { $t.BaseType.FullName } else { "" }
    [void]$sb.AppendLine("TYPE $($t.FullName) : $base")
    foreach ($f in $t.Fields) { [void]$sb.AppendLine("  F $(if($f.IsStatic){'static '})$($f.FieldType.Name) $($f.Name)") }
    foreach ($m in $t.Methods) {
        $ps = ($m.Parameters | % { "$($_.ParameterType.Name) $($_.Name)" }) -join ", "
        [void]$sb.AppendLine("  M $(if($m.IsStatic){'static '})$($m.ReturnType.Name) $($m.Name)($ps) [IL=$(if($m.HasBody){$m.Body.CodeSize}else{0})]")
        if ($Il -and $t.FullName -match $Il -and $m.HasBody) {
            foreach ($i in $m.Body.Instructions) { [void]$sb.AppendLine("      $i") }
        }
    }
    foreach ($n in $t.NestedTypes) { Walk $n }
}
foreach ($t in $mod.Types) { Walk $t }
[IO.File]::WriteAllText($Saida, $sb.ToString())
"ok: $($mod.Types.Count) tipos -> $Saida"

param([string]$Compilador = $env:LEGENDALIVRE_LLVM_MINGW)
$ErrorActionPreference = 'Stop'
$projeto = Split-Path $PSScriptRoot -Parent
$versao = '20261006'
if (!$Compilador) { $Compilador = Join-Path $projeto "_scratch/compilador-nativo/llvm-mingw-$versao-ucrt-x86_64/bin" }
if (!(Test-Path -LiteralPath (Join-Path $Compilador 'x86_64-w64-mingw32-clang++.exe'))) {
    $pasta = Join-Path $projeto '_scratch/compilador-nativo'
    New-Item -ItemType Directory -Path $pasta -Force | Out-Null
    $zip = Join-Path $pasta "llvm-mingw-$versao-ucrt-x86_64.zip"
    if (!(Test-Path -LiteralPath $zip)) {
        Invoke-WebRequest "https://github.com/mstorsjo/llvm-mingw/releases/download/$versao/llvm-mingw-$versao-ucrt-x86_64.zip" -OutFile $zip
    }
    if ((Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant() -ne '317492c456aa27ee607a5919f1d2d38dcdc1112516a24d0bf4b00d078f52d17a') {
        throw 'O checksum do compilador nativo diverge do publicado.'
    }
    Expand-Archive -LiteralPath $zip -DestinationPath $pasta -Force
}
$origem = Join-Path $projeto 'src/LegendaLivre.MenuExplorer'
$destino = Join-Path $projeto '_scratch/menu-nativo-build'
New-Item -ItemType Directory -Path $destino -Force | Out-Null
foreach ($alvo in @(@('x64','x86_64','DllMain'), @('x86','i686','DllMain@12'), @('arm64','aarch64','DllMain'))) {
    $saida = Join-Path $destino "LegendaLivre.MenuExplorer.$($alvo[0]).dll"
    $fontes = @(Get-ChildItem -LiteralPath $origem -File) + @(Get-Item -LiteralPath $PSCommandPath)
    if ((Test-Path -LiteralPath $saida) -and !(($fontes | Where-Object LastWriteTimeUtc -gt (Get-Item -LiteralPath $saida).LastWriteTimeUtc))) { continue }
    $clang = Join-Path $Compilador "$($alvo[1])-w64-mingw32-clang++.exe"
    & $clang -std=c++17 -shared -Os -fno-exceptions -fno-rtti -ffunction-sections -fdata-sections -nostdlib "-Wl,--entry,$($alvo[2]),--strip-all,--kill-at,--gc-sections,--no-insert-timestamp" -DUNICODE -D_UNICODE -D_WIN32_WINNT=0x0A00 (Join-Path $origem 'MenuExplorer.cpp') (Join-Path $origem 'Renderizacao.cpp') (Join-Path $origem 'MenuExplorer.def') -o $saida -luser32 -lgdi32 -lshell32 -lole32 -lcomctl32 -ldwmapi -ladvapi32 -lkernel32 -lucrtbase
    if ($LASTEXITCODE -ne 0) { throw "Falha na compilação nativa: $($alvo[0])" }
    Write-Output "Menu nativo $($alvo[0]): $((Get-Item -LiteralPath $saida).Length) bytes"
}

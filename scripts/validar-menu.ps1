$ErrorActionPreference = 'Stop'
$projeto = Split-Path $PSScriptRoot -Parent
& (Join-Path $PSScriptRoot 'compilar-menu.ps1')
$compilador = $env:LEGENDALIVRE_LLVM_MINGW
if (!$compilador) { $compilador = Join-Path $projeto '_scratch/compilador-nativo/llvm-mingw-20261006-ucrt-x86_64/bin' }
$csc = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
if (!(Test-Path -LiteralPath $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET/Framework/v4.0.30319/csc.exe' }
$arquiteturas = if ([Environment]::Is64BitOperatingSystem) { @(@('x64','x86_64'),@('x86','i686')) } else { @(@('x86','i686')) }
foreach ($alvo in $arquiteturas) {
    $pasta = Join-Path $projeto "_scratch/validacao-menu-nativo/$($alvo[0])-$(Get-Date -Format yyyyMMdd-HHmmss)-$([Guid]::NewGuid().ToString('N').Substring(0,8))"
    New-Item -ItemType Directory -Path $pasta -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $projeto "_scratch/menu-nativo-build/LegendaLivre.MenuExplorer.$($alvo[0]).dll") -Destination (Join-Path $pasta 'MenuExplorer.dll')
    & $csc /nologo /target:exe "/platform:$($alvo[0])" "/out:$pasta/LegendaLivre.exe" "/win32icon:$projeto/src/LegendaLivre/Assets/LegendaLivre.ico" (Join-Path $projeto 'tests/menu-explorer/LegendaLivreFalso.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Falha ao compilar o gravador de argumentos.' }
    & (Join-Path $compilador "$($alvo[1])-w64-mingw32-clang++.exe") -std=c++17 -Os -static -municode -DUNICODE -D_UNICODE (Join-Path $projeto 'tests/menu-explorer/ValidarMenu.cpp') -o (Join-Path $pasta 'ValidarMenu.exe') -luser32 -lgdi32 -lshell32 -lole32 -lcomctl32
    if ($LASTEXITCODE -ne 0) { throw 'Falha ao compilar a validação nativa.' }
    & (Join-Path $pasta 'ValidarMenu.exe') $pasta | Tee-Object (Join-Path $pasta 'validacao.log')
    if ($LASTEXITCODE -ne 0) { throw "A validação nativa falhou: $($alvo[0])" }
    $invocacoes = @(Get-ChildItem (Join-Path $pasta 'invocacoes') -File)
    if ($invocacoes.Count -ne 9) { throw "Número inesperado de chamadas: $($invocacoes.Count)" }
    $chamadas = foreach ($invocacao in $invocacoes) {
        $argumentos = @(Get-Content -LiteralPath $invocacao.FullName | ForEach-Object { [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($_)) })
        if ($argumentos.Count -ne 3 -or $argumentos[0] -notin 'melhor','top' -or $argumentos[1] -notin '6','36','14') { throw 'Argumentos inválidos.' }
        if ($argumentos[2] -notin (Join-Path $pasta 'Video de teste.mkv'), (Join-Path $pasta 'Outro vídeo.mkv')) { throw 'O caminho do vídeo foi alterado.' }
        "$($argumentos[0]) $($argumentos[1]) $($argumentos[2])"
    }
    $chamadas | Set-Content (Join-Path $pasta 'comandos.txt')
    Write-Output "Aprovado: $($alvo[0]), 9 chamadas, 50 ciclos de abertura/fechamento. Evidências: $pasta"
}

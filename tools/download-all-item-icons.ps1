param(
    [string]$ProjectRoot = (Resolve-Path "$PSScriptRoot\..").Path,
    [int]$Size = 96
)

$ErrorActionPreference = 'Stop'
$source = Join-Path $ProjectRoot 'src\AlbionMarket.Application\Features\Market\Catalog\Source\items.txt'
$target = Join-Path $ProjectRoot 'src\AlbionMarket.Mobile\Resources\Images\ItemIcons'
New-Item -ItemType Directory -Force -Path $target | Out-Null

if (!(Test-Path $source)) {
    throw "items.txt não encontrado em $source"
}

$itemIds = Get-Content $source |
    ForEach-Object {
        if ($_ -match '^[\s\d:]*([A-Z0-9_@]+)\s*:') { $matches[1] }
    } |
    Where-Object { $_ -and $_ -notmatch 'NONTRADABLE' } |
    Sort-Object -Unique

$qualities = 1..5
$total = $itemIds.Count * $qualities.Count
$done = 0

foreach ($id in $itemIds) {
    foreach ($q in $qualities) {
        $safeId = $id.ToLowerInvariant().Replace('@','_')
        $file = Join-Path $target "$($safeId)_$($q)_$Size.png"
        if (Test-Path $file) {
            $done++
            continue
        }

        $encoded = [System.Uri]::EscapeDataString($id)
        $url = "https://render.albiononline.com/v1/item/$encoded.png?quality=$q&size=$Size&locale=en"
        try {
            Invoke-WebRequest -Uri $url -OutFile $file -UseBasicParsing -TimeoutSec 20 | Out-Null
        } catch {
            if (Test-Path $file) { Remove-Item $file -Force }
        }

        $done++
        if ($done % 100 -eq 0) {
            Write-Host "Ícones: $done / $total"
        }
    }
}

Write-Host "Concluído. Ícones salvos em: $target"
Write-Host "Depois rode o build do MAUI normalmente para embutir os PNGs no app."

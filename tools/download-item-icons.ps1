param(
    [string]$CatalogTxt = "..\src\AlbionMarket.Application\Features\Market\Catalog\Source\items.txt",
    [string]$Output = "..\item-icon-cache",
    [int[]]$Qualities = @(1,2,3,4,5),
    [int]$Size = 96
)

$ErrorActionPreference = "Stop"
New-Item -ItemType Directory -Force -Path $Output | Out-Null

$ids = Get-Content $CatalogTxt | ForEach-Object {
    if ($_ -match "^\s*\d+:\s*([^:]+)\s*:") { $matches[1].Trim() }
} | Where-Object { $_ -and ($_ -match "^T[2-8]_") } | Sort-Object -Unique

Write-Host "Baixando icones para $($ids.Count) itens em $Output"
foreach ($id in $ids) {
    foreach ($q in $Qualities) {
        $safe = ($id.ToUpperInvariant() -replace "@", "_")
        $file = Join-Path $Output "$safe`_q$q`_$Size.png"
        if (Test-Path $file) { continue }
        $urlId = [System.Uri]::EscapeDataString($id.ToUpperInvariant()).Replace("%40", "@")
        $url = "https://render.albiononline.com/v1/item/$urlId.png?quality=$q&size=$Size&locale=en"
        try {
            Invoke-WebRequest -Uri $url -OutFile $file -UseBasicParsing -TimeoutSec 20 | Out-Null
            Write-Host "OK $id q$q"
        } catch {
            Write-Warning "Falhou $id q$q: $($_.Exception.Message)"
        }
    }
}

Write-Host "Concluido. Para usar tudo offline no APK, copie esses arquivos para Resources\Images com nomes compativeis ou mantenha como cache externo."

# Baixa bases oficiais/comunitárias do ao-data/ao-bin-dumps para substituir tradução manual.
# Observação: localization.json é grande. A conversão final depende do formato atual do dump.
# Saída esperada pelo app/API: src/AlbionMarket.Application/Features/Market/Catalog/Source/items.pt-BR.txt
# Formato de saída:
#   1: T4_BAG : Bolsa do adepto
#   2: T5_BAG : Bolsa do especialista

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$source = Join-Path $root "src\AlbionMarket.Application\Features\Market\Catalog\Source"
$tmp = Join-Path $root ".tmp\albion-localization"
New-Item -ItemType Directory -Force -Path $tmp | Out-Null

$itemsTxt = Join-Path $source "items.txt"
$ptOut = Join-Path $source "items.pt-BR.txt"
$localization = Join-Path $tmp "localization.json"

Write-Host "Baixando localization.json do ao-data/ao-bin-dumps..."
Invoke-WebRequest "https://raw.githubusercontent.com/ao-data/ao-bin-dumps/master/localization.json" -OutFile $localization

Write-Host "Arquivo baixado em: $localization"
Write-Host "IMPORTANTE: este script prepara a base. Se o formato do localization.json mudar, ajuste o parser abaixo."
Write-Host "O app/API já estão prontos para carregar: $ptOut"

# Parser conservador: não tenta adivinhar estrutura se o arquivo vier em formato inesperado.
# A forma segura é mapear UniqueName -> locatag pelo items.json/items.xml e locatag -> pt-BR pelo localization.json.
# Próximo passo técnico: completar este parser quando a estrutura atual do dump for validada localmente.
if (!(Test-Path $itemsTxt)) {
    throw "items.txt não encontrado em $itemsTxt"
}

Write-Host "Base inglesa atual: $itemsTxt"
Write-Host "Crie/gere $ptOut no mesmo formato do items.txt. Quando existir, o backend usará automaticamente os nomes PT-BR."

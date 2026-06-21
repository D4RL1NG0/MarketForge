# Localização PT-BR de itens

O backend agora procura automaticamente por:

`src/AlbionMarket.Application/Features/Market/Catalog/Source/items.pt-BR.txt`

Formato esperado:

```txt
1: T4_BAG : Bolsa do adepto
2: T5_BAG : Bolsa do especialista
```

Quando esse arquivo existir, a API passa a devolver `namePtBr`, e o app usa esse nome antes de qualquer tradução manual.

Fonte recomendada: `ao-data/ao-bin-dumps`, especialmente `localization.json` + `items.json`/`items.xml`, ou a página de itens do Albion Online Data Project que já pesquisa nomes localizados.

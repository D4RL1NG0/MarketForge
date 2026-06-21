# MarketForge v1.2.0 - Prewarm cache Neon

Esta versão adiciona um agendador de prewarm para popular o Neon antes do usuário precisar clicar no app.

## Como funciona

- A API enfileira itens principais de mercado/refino/craft no Neon.
- O worker sincroniza aos poucos, em lote, respeitando throttle e backoff.
- A cada 15 minutos, a API re-enfileira os itens do escopo configurado.
- Se o preço ainda estiver válido no Neon, o worker limpa a fila e não chama a Albion Data.

## Endpoints

### Status do prewarm

```text
/api/market/cache/prewarm-status
```

### Rodar prewarm manual

```text
/api/market/cache/prewarm?server=west&scope=core
```

Para tentar um lote maior:

```text
/api/market/cache/prewarm?server=west&scope=all&limit=1200
```

## Variáveis opcionais no Render

```text
MARKETFORGE_PREWARM_SCOPE=core
MARKETFORGE_PREWARM_SERVER=west
MARKETFORGE_PREWARM_LIMIT=300
```

Para grátis, recomendado:

```text
MARKETFORGE_PREWARM_SCOPE=core
MARKETFORGE_PREWARM_SERVER=west
```

Use `all` com cuidado, porque todos os itens do Albion podem gerar muitas chamadas e bater rate-limit da Albion Data.

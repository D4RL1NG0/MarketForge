# MarketForge v1.3.0 - Neon-only West launch mode

Esta versão foi travada para lançamento rápido e estável:

- O usuário nunca chama a Albion Data API diretamente.
- Todos os endpoints do app leem primeiro e somente o Neon/Postgres.
- O servidor é fixo em West, mesmo que o app mande Europe/East.
- Preços com mais de 60 minutos de cache não são retornados.
- Se um preço não existir no Neon ou estiver velho demais, a API enfileira sincronização e retorna vazio/sincronizando.
- Apenas o worker em segundo plano chama a Albion Data API.
- O worker usa lote pequeno, espera entre lotes e dorme 10 minutos se detectar rate-limit.

## Antes de testar a versão nova

No Neon SQL Editor, limpe a fila antiga para remover tarefas de Europe/East/all criadas por versões anteriores:

```sql
delete from price_sync_queue;
```

Opcional, para limpar preços de servidores que não serão usados no lançamento:

```sql
delete from market_price_cache where server <> 'west';
delete from gold_price_cache where server <> 'west';
```

## Prewarm recomendado

Depois do deploy, rode apenas West/core:

```text
https://marketforge-api-d4rl1ng0.onrender.com/api/market/cache/prewarm?server=west&scope=core
```

Mesmo que envie outro server/scope, esta versão força West/core para evitar rate limit.

## Verificar modo ativo

```text
https://marketforge-api-d4rl1ng0.onrender.com/api/market/cache-status
```

Esperado:

```json
{
  "persistentCacheEnabled": true,
  "mode": "neon-only-west",
  "server": "west",
  "maxVisibleAgeMinutes": 60,
  "userRequestsHitAlbionData": false
}
```

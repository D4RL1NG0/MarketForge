# MarketForge V1.1.2 — Cache persistente gratuito com Neon Postgres

Esta versão deixa o app mais estável contra rate limit da Albion Data API.

## O que mudou

- A API lê preços primeiro do Postgres.
- Se o preço ainda está válido, não chama a Albion Data API.
- Se o preço está velho, a API devolve o último preço salvo e enfileira uma sincronização.
- A sincronização em background usa lotes de item IDs numa única chamada.
- Se a Albion Data responder 429, a fila espera e tenta depois.

## Criar banco grátis no Neon

1. Acesse https://neon.com
2. Crie uma conta grátis.
3. Crie um projeto chamado `marketforge`.
4. Copie a connection string do banco.
5. No Render, abra o serviço `marketforge-api-d4rl1ng0`.
6. Vá em Environment.
7. Adicione:

```text
DATABASE_URL=<connection string do Neon>
```

Pode ser no formato:

```text
postgresql://usuario:senha@host.neon.tech/dbname?sslmode=require
```

A API cria as tabelas automaticamente ao iniciar.

## Redeploy no Render

Depois de adicionar `DATABASE_URL`, clique em:

```text
Manual Deploy → Deploy latest commit
```

No log, procure:

```text
✅ MarketForge persistent price cache is ready.
✅ MarketForge background price sync started.
```

## Teste rápido

Abra:

```text
https://marketforge-api-d4rl1ng0.onrender.com/health
```

Depois teste o app normalmente. Na primeira consulta de um item novo pode haver uma chamada externa; nas próximas, o preço vem do Postgres.

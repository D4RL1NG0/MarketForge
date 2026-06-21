-- MarketForge V1.1.0 persistent price cache schema.
-- The API also creates these tables automatically at startup when DATABASE_URL is configured.

create table if not exists market_price_cache (
    server text not null,
    item_id text not null,
    quality integer not null,
    city text not null,
    sell_price_min bigint not null default 0,
    sell_price_max bigint not null default 0,
    buy_price_min bigint not null default 0,
    buy_price_max bigint not null default 0,
    updated_at_utc timestamptz null,
    synced_at_utc timestamptz not null default now(),
    valid_until_utc timestamptz not null,
    primary key(server, item_id, quality, city)
);

create index if not exists ix_market_price_cache_valid_until on market_price_cache(valid_until_utc);
create index if not exists ix_market_price_cache_item on market_price_cache(server, item_id, quality);

create table if not exists price_sync_queue (
    server text not null,
    item_id text not null,
    quality integer not null,
    priority integer not null default 0,
    requested_at_utc timestamptz not null default now(),
    last_attempt_at_utc timestamptz null,
    next_attempt_at_utc timestamptz not null default now(),
    attempts integer not null default 0,
    status text not null default 'queued',
    primary key(server, item_id, quality)
);

create index if not exists ix_price_sync_queue_ready on price_sync_queue(status, next_attempt_at_utc, priority, requested_at_utc);

create table if not exists gold_price_cache (
    server text primary key,
    price bigint not null default 0,
    updated_at_utc timestamptz null,
    synced_at_utc timestamptz not null default now(),
    valid_until_utc timestamptz not null
);

using System.Data;
using AlbionMarket.Application.Features.Market.Models;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace AlbionMarket.Application.Features.Market.Cache;

public sealed class PostgresMarketPriceCacheStore : IMarketPriceCacheStore
{
    private readonly string? _connectionString;
    private readonly SemaphoreSlim _initGate = new(1, 1);
    private volatile bool _initialized;

    public PostgresMarketPriceCacheStore(IConfiguration configuration)
    {
        _connectionString = ResolveConnectionString(configuration);
    }

    public bool IsEnabled => !string.IsNullOrWhiteSpace(_connectionString);

    public async Task<IReadOnlyList<MarketPrice>> GetPricesAsync(
        string server,
        string itemId,
        int quality,
        IReadOnlyCollection<string> locations,
        bool allowStale,
        CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
            return Array.Empty<MarketPrice>();

        try
        {
            await EnsureCreatedAsync(cancellationToken);
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var sql = @"
select item_id, city, sell_price_min, sell_price_max, buy_price_min, buy_price_max, updated_at_utc
from market_price_cache
where server = @server
  and item_id = @item_id
  and quality = @quality
  and city = any(@locations)
  and (
      valid_until_utc > now()
      or (@allow_stale and synced_at_utc >= now() - interval '60 minutes')
  )
order by city;";

            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("server", NormalizeServer(server));
            command.Parameters.AddWithValue("item_id", itemId.Trim().ToUpperInvariant());
            command.Parameters.AddWithValue("quality", quality);
            command.Parameters.AddWithValue("locations", locations.Select(x => x.Trim()).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
            command.Parameters.AddWithValue("allow_stale", allowStale);

            var result = new List<MarketPrice>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                result.Add(new MarketPrice
                {
                    ItemId = reader.GetString(0),
                    City = reader.GetString(1),
                    SellPriceMin = reader.GetInt64(2),
                    SellPriceMax = reader.GetInt64(3),
                    BuyPriceMin = reader.GetInt64(4),
                    BuyPriceMax = reader.GetInt64(5),
                    UpdatedAtUtc = reader.IsDBNull(6) ? null : reader.GetFieldValue<DateTimeOffset>(6)
                });
            }

            return result;
        }
        catch (Exception ex)
        {
            MarkFailed(ex, "read price cache");
            return Array.Empty<MarketPrice>();
        }
    }

    public async Task UpsertPricesAsync(
        string server,
        string itemId,
        int quality,
        IReadOnlyCollection<MarketPrice> prices,
        DateTimeOffset validUntilUtc,
        CancellationToken cancellationToken = default)
    {
        if (!IsEnabled || prices.Count == 0)
            return;

        try
        {
            await EnsureCreatedAsync(cancellationToken);
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

            var sql = @"
insert into market_price_cache
(server, item_id, quality, city, sell_price_min, sell_price_max, buy_price_min, buy_price_max, updated_at_utc, synced_at_utc, valid_until_utc)
values
(@server, @item_id, @quality, @city, @sell_price_min, @sell_price_max, @buy_price_min, @buy_price_max, @updated_at_utc, now(), @valid_until_utc)
on conflict (server, item_id, quality, city) do update set
  sell_price_min = excluded.sell_price_min,
  sell_price_max = excluded.sell_price_max,
  buy_price_min = excluded.buy_price_min,
  buy_price_max = excluded.buy_price_max,
  updated_at_utc = excluded.updated_at_utc,
  synced_at_utc = now(),
  valid_until_utc = excluded.valid_until_utc;";

            foreach (var price in prices)
            {
                await using var command = new NpgsqlCommand(sql, connection, transaction);
                command.Parameters.AddWithValue("server", NormalizeServer(server));
                command.Parameters.AddWithValue("item_id", itemId.Trim().ToUpperInvariant());
                command.Parameters.AddWithValue("quality", quality);
                command.Parameters.AddWithValue("city", price.City);
                command.Parameters.AddWithValue("sell_price_min", price.SellPriceMin);
                command.Parameters.AddWithValue("sell_price_max", price.SellPriceMax);
                command.Parameters.AddWithValue("buy_price_min", price.BuyPriceMin);
                command.Parameters.AddWithValue("buy_price_max", price.BuyPriceMax);
                command.Parameters.AddWithValue("updated_at_utc", price.UpdatedAtUtc is null ? DBNull.Value : price.UpdatedAtUtc.Value);
                command.Parameters.AddWithValue("valid_until_utc", validUntilUtc);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            MarkFailed(ex, "upsert price cache");
        }
    }

    public async Task EnqueuePriceSyncAsync(string server, string itemId, int quality, int priority = 0, CancellationToken cancellationToken = default)
    {
        if (!IsEnabled || string.IsNullOrWhiteSpace(itemId))
            return;

        try
        {
            await EnsureCreatedAsync(cancellationToken);
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var sql = @"
insert into price_sync_queue(server, item_id, quality, priority, requested_at_utc, next_attempt_at_utc, attempts, status)
values (@server, @item_id, @quality, @priority, now(), now(), 0, 'queued')
on conflict (server, item_id, quality) do update set
  priority = greatest(price_sync_queue.priority, excluded.priority),
  requested_at_utc = least(price_sync_queue.requested_at_utc, excluded.requested_at_utc),
  next_attempt_at_utc = least(price_sync_queue.next_attempt_at_utc, excluded.next_attempt_at_utc),
  status = case when price_sync_queue.status = 'processing' then 'processing' else 'queued' end;";

            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("server", NormalizeServer(server));
            command.Parameters.AddWithValue("item_id", itemId.Trim().ToUpperInvariant());
            command.Parameters.AddWithValue("quality", quality);
            command.Parameters.AddWithValue("priority", priority);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            MarkFailed(ex, "enqueue price sync");
        }
    }

    public async Task<IReadOnlyList<PriceSyncRequest>> ClaimPriceSyncBatchAsync(int limit, CancellationToken cancellationToken = default)
    {
        if (!IsEnabled || limit <= 0)
            return Array.Empty<PriceSyncRequest>();

        try
        {
            await EnsureCreatedAsync(cancellationToken);
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var sql = @"
with picked as (
    select server, item_id, quality
    from price_sync_queue
    where (status <> 'processing' or last_attempt_at_utc < now() - interval '15 minutes')
      and server = 'west'
      and next_attempt_at_utc <= now()
    order by priority desc, requested_at_utc asc
    limit @limit
    for update skip locked
)
update price_sync_queue q
set status = 'processing', last_attempt_at_utc = now(), attempts = attempts + 1
from picked
where q.server = picked.server and q.item_id = picked.item_id and q.quality = picked.quality
returning q.server, q.item_id, q.quality, q.priority;";

            await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
            await using var command = new NpgsqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue("limit", limit);

            var result = new List<PriceSyncRequest>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                result.Add(new PriceSyncRequest(reader.GetString(0), reader.GetString(1), reader.GetInt32(2), reader.GetInt32(3)));
            await reader.CloseAsync();
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch (Exception ex)
        {
            MarkFailed(ex, "claim price sync");
            return Array.Empty<PriceSyncRequest>();
        }
    }

    public async Task CompletePriceSyncAsync(IEnumerable<PriceSyncRequest> requests, CancellationToken cancellationToken = default)
    {
        var rows = requests.ToList();
        if (!IsEnabled || rows.Count == 0)
            return;

        try
        {
            await EnsureCreatedAsync(cancellationToken);
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

            foreach (var row in rows)
            {
                await using var command = new NpgsqlCommand("delete from price_sync_queue where server=@server and item_id=@item_id and quality=@quality;", connection, transaction);
                command.Parameters.AddWithValue("server", NormalizeServer(row.Server));
                command.Parameters.AddWithValue("item_id", row.ItemId.Trim().ToUpperInvariant());
                command.Parameters.AddWithValue("quality", row.Quality);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            MarkFailed(ex, "complete price sync");
        }
    }

    public async Task RequeuePriceSyncAsync(IEnumerable<PriceSyncRequest> requests, TimeSpan delay, CancellationToken cancellationToken = default)
    {
        var rows = requests.ToList();
        if (!IsEnabled || rows.Count == 0)
            return;

        try
        {
            await EnsureCreatedAsync(cancellationToken);
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

            foreach (var row in rows)
            {
                await using var command = new NpgsqlCommand(@"
update price_sync_queue
set status = 'queued', next_attempt_at_utc = now() + (@delay_seconds * interval '1 second')
where server=@server and item_id=@item_id and quality=@quality;", connection, transaction);
                command.Parameters.AddWithValue("delay_seconds", Math.Max(30, (int)delay.TotalSeconds));
                command.Parameters.AddWithValue("server", NormalizeServer(row.Server));
                command.Parameters.AddWithValue("item_id", row.ItemId.Trim().ToUpperInvariant());
                command.Parameters.AddWithValue("quality", row.Quality);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            MarkFailed(ex, "requeue price sync");
        }
    }

    public async Task<GoldPrice?> GetGoldAsync(string server, bool allowStale, CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
            return null;

        try
        {
            await EnsureCreatedAsync(cancellationToken);
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var sql = @"
select server, price, updated_at_utc
from gold_price_cache
where server = @server
  and (valid_until_utc > now() or (@allow_stale and synced_at_utc >= now() - interval '60 minutes'));";
            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("server", NormalizeServer(server));
            command.Parameters.AddWithValue("allow_stale", allowStale);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                return null;

            return new GoldPrice
            {
                Server = reader.GetString(0),
                Price = reader.GetInt64(1),
                UpdatedAtUtc = reader.IsDBNull(2) ? null : reader.GetFieldValue<DateTimeOffset>(2)
            };
        }
        catch (Exception ex)
        {
            MarkFailed(ex, "read gold cache");
            return null;
        }
    }

    public async Task UpsertGoldAsync(string server, GoldPrice gold, DateTimeOffset validUntilUtc, CancellationToken cancellationToken = default)
    {
        if (!IsEnabled || gold.Price <= 0)
            return;

        try
        {
            await EnsureCreatedAsync(cancellationToken);
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var sql = @"
insert into gold_price_cache(server, price, updated_at_utc, synced_at_utc, valid_until_utc)
values(@server, @price, @updated_at_utc, now(), @valid_until_utc)
on conflict(server) do update set
  price = excluded.price,
  updated_at_utc = excluded.updated_at_utc,
  synced_at_utc = now(),
  valid_until_utc = excluded.valid_until_utc;";
            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("server", NormalizeServer(server));
            command.Parameters.AddWithValue("price", gold.Price);
            command.Parameters.AddWithValue("updated_at_utc", gold.UpdatedAtUtc is null ? DBNull.Value : gold.UpdatedAtUtc.Value);
            command.Parameters.AddWithValue("valid_until_utc", validUntilUtc);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            MarkFailed(ex, "upsert gold cache");
        }
    }

    private async Task EnsureCreatedAsync(CancellationToken cancellationToken)
    {
        if (_initialized || !IsEnabled)
            return;

        await _initGate.WaitAsync(cancellationToken);
        try
        {
            if (_initialized || !IsEnabled)
                return;

            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var sql = @"
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
);";

            await using var command = new NpgsqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync(cancellationToken);
            _initialized = true;
            Console.WriteLine("✅ MarketForge persistent price cache is ready.");
        }
        catch (Exception ex)
        {
            MarkFailed(ex, "initialize price cache");
        }
        finally
        {
            _initGate.Release();
        }
    }

    private static void MarkFailed(Exception ex, string operation)
    {
        Console.WriteLine($"⚠️ Persistent price cache failure during {operation}: {ex.Message}");
    }

    private static string? ResolveConnectionString(IConfiguration configuration)
    {
        var raw = configuration.GetConnectionString("MarketForgeDb")
            ?? configuration["MARKETFORGE_DATABASE_URL"]
            ?? configuration["DATABASE_URL"]
            ?? configuration["POSTGRES_CONNECTION_STRING"];

        if (string.IsNullOrWhiteSpace(raw))
            return null;

        raw = raw.Trim();
        if (!raw.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
            && !raw.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
            return raw;

        var uri = new Uri(raw);
        var userInfo = uri.UserInfo.Split(':', 2);
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Database = uri.AbsolutePath.TrimStart('/'),
            Username = Uri.UnescapeDataString(userInfo.ElementAtOrDefault(0) ?? string.Empty),
            Password = Uri.UnescapeDataString(userInfo.ElementAtOrDefault(1) ?? string.Empty),
            SslMode = SslMode.Require,
            Pooling = true,
            MaxPoolSize = 5,
            Timeout = 15,
            CommandTimeout = 30
        };

        return builder.ConnectionString;
    }

    private static string NormalizeServer(string? server)
    {
        return "west";
    }
}

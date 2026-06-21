using Microsoft.Extensions.DependencyInjection;
using AlbionMarket.Application.Features.Market.Interfaces;
using AlbionMarket.Application.Features.Market.Services;
using AlbionMarket.Application.Features.Market.Catalog;
using AlbionMarket.Application.Features.Market.Cache;

namespace AlbionMarket.Application.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IMarketService, MarketService>();
        services.AddSingleton<IMarketPriceCacheStore, PostgresMarketPriceCacheStore>();
        services.AddHostedService<PriceSyncHostedService>();
        services.AddSingleton<MarketCacheWarmupService>();
        services.AddHostedService(sp => sp.GetRequiredService<MarketCacheWarmupService>());

        services.AddSingleton<ItemCatalogService>(sp =>
        {
            var service = new ItemCatalogService();
            service.Load();
            return service;
        });

        return services;
    }
}

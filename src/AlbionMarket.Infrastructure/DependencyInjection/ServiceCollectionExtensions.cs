using AlbionMarket.Infrastructure.AlbionDataApi.Clients;
using AlbionMarket.Infrastructure.AlbionDataApi.Constants;
using Microsoft.Extensions.DependencyInjection;

namespace AlbionMarket.Infrastructure.DependencyInjection;


public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services)
    {
        services.AddHttpClient<IAlbionDataClient, AlbionDataClient>(client =>
        {
            client.BaseAddress = new Uri(AlbionDataConstants.BaseUrl);
        });

        return services;
    }
}
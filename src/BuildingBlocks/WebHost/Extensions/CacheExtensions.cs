using Microsoft.Extensions.DependencyInjection;

namespace WebHost.Extensions;

public static class CacheExtensions
{
    public static IServiceCollection AddRedisCache(
        this IServiceCollection services,
        string connectionString,
        string instanceName)
    {
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = connectionString;
            options.InstanceName = instanceName;
        });

        return services;
    }

    public static IServiceCollection AddCaching(
        this IServiceCollection services,
        string? redisConnectionString = null,
        string instanceName = "App:")
    {
        services.AddMemoryCache();

        if (!string.IsNullOrEmpty(redisConnectionString))
        {
            services.AddRedisCache(redisConnectionString, instanceName);
        }
        else
        {
            services.AddDistributedMemoryCache();
        }

        services.AddResponseCaching();

        return services;
    }
}

using Microsoft.Extensions.DependencyInjection;
using Rebus.Config;
using Rebus.Routing.TypeBased;

namespace WebHost.Extensions;

public class RebusConfiguration
{
    public required string Host { get; set; }
    public int Port { get; set; } = 5672;
    public required string Username { get; set; }
    public required string Password { get; set; }
    public required string QueueName { get; set; }
    public int Workers { get; set; } = 1;
    public int MaxParallelism { get; set; } = 10;
    public bool UseSerilog { get; set; } = true;

    public string ConnectionString =>
        $"amqp://{Username}:{Password}@{Host}:{Port}";
}

public static class RebusExtensions
{
    public static IServiceCollection AddRebusWithRabbitMq(
        this IServiceCollection services,
        RebusConfiguration config,
        Action<TypeBasedRouterConfigurationExtensions.TypeBasedRouterConfigurationBuilder>? configureRouting = null,
        Func<Rebus.Bus.IBus, Task>? onCreated = null)
    {
        services.AddRebus((configure, provider) =>
        {
            var rebusConfig = configure
                .Transport(t => t.UseRabbitMq(config.ConnectionString, config.QueueName))
                .Options(o =>
                {
                    o.SetNumberOfWorkers(config.Workers);
                    o.SetMaxParallelism(config.MaxParallelism);
                });

            if (config.UseSerilog)
            {
                rebusConfig.Logging(l => l.Serilog());
            }

            if (configureRouting != null)
            {
                rebusConfig.Routing(r => configureRouting(r.TypeBased()));
            }

            return rebusConfig;
        }, onCreated: onCreated);

        return services;
    }
}

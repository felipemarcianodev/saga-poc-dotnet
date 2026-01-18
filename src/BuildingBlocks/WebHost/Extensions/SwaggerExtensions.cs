using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Reflection;

namespace WebHost.Extensions;

/// <summary>
/// Configurações para o Swagger
/// </summary>
public class SwaggerConfiguration
{
    public required string Title { get; set; }
    public string Version { get; set; } = "v1";
    public string? Description { get; set; }
    public bool EnableAnnotations { get; set; } = false;
    public OpenApiContact? Contact { get; set; }
    public Assembly? XmlCommentsAssembly { get; set; }
}

/// <summary>
/// Extension methods para configuração do Swagger
/// </summary>
public static class SwaggerExtensions
{
    /// <summary>
    /// Adiciona configuração do Swagger com parâmetros personalizados
    /// </summary>
    public static IServiceCollection AddSwaggerConfiguration(
        this IServiceCollection services,
        SwaggerConfiguration config)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            if (config.EnableAnnotations)
            {
                options.EnableAnnotations();
            }

            options.SwaggerDoc(config.Version, new OpenApiInfo
            {
                Title = config.Title,
                Version = config.Version,
                Description = config.Description,
                Contact = config.Contact
            });

            IncludeXmlComments(options, config.XmlCommentsAssembly);
        });

        return services;
    }

    /// <summary>
    /// Adiciona configuração do Swagger com parâmetros simplificados
    /// </summary>
    public static IServiceCollection AddSwaggerConfiguration(
        this IServiceCollection services,
        string title,
        string version = "v1",
        string? description = null,
        bool enableAnnotations = false)
    {
        return services.AddSwaggerConfiguration(new SwaggerConfiguration
        {
            Title = title,
            Version = version,
            Description = description,
            EnableAnnotations = enableAnnotations,
            XmlCommentsAssembly = Assembly.GetCallingAssembly()
        });
    }

    private static void IncludeXmlComments(SwaggerGenOptions options, Assembly? assembly)
    {
        assembly ??= Assembly.GetEntryAssembly();

        if (assembly == null) return;

        var xmlFile = $"{assembly.GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);

        if (File.Exists(xmlPath))
        {
            options.IncludeXmlComments(xmlPath);
        }
    }
}

using Microsoft.AspNetCore.Builder;
using WebHost.Middlewares;

namespace WebHost.Extensions;

public static class ExceptionHandlerExtensions
{
    /// <summary>
    /// Adiciona o middleware de tratamento global de excecoes.
    /// Deve ser adicionado no inicio do pipeline para capturar todas as excecoes.
    /// </summary>
    public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder app)
    {
        return app.UseMiddleware<GlobalExceptionHandlerMiddleware>();
    }
}

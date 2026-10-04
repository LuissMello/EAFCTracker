using System.Data.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;

namespace EAFCMatchTracker.Api.Infrastructure;

/// <summary>
/// Modo somente leitura (Runtime:ReadOnly=true; env Runtime__ReadOnly=true): permite apontar uma instância LOCAL
/// para o banco real só para analisar os dados, sem risco de gravar nele. Camadas de proteção:
///  1) não sobe o serviço de busca na EA, não aplica migrations e não semeia configurações;
///  2) <see cref="ReadOnlyGuardMiddleware"/> responde 403 a tudo que não for leitura (GET/HEAD/OPTIONS);
///  3) <see cref="ReadOnlyCommandInterceptor"/> recusa, no nível do comando SQL, qualquer INSERT/UPDATE/DELETE/DDL
///     (cobre também ExecuteDelete/ExecuteUpdate, que não passam pelo SaveChanges).
/// </summary>
public static class ReadOnlyMode
{
    public const string ConfigKey = "Runtime:ReadOnly";

    public static bool IsEnabled(IConfiguration config) => config.GetValue<bool>(ConfigKey);

    public const string Message = "Modo somente leitura: esta instância não grava no banco de dados.";
}

public sealed class ReadOnlyGuardMiddleware
{
    private readonly RequestDelegate _next;

    public ReadOnlyGuardMiddleware(RequestDelegate next) => _next = next;

    public Task InvokeAsync(HttpContext context)
    {
        var method = context.Request.Method;
        if (HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method))
            return _next(context);

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "application/problem+json";
        return context.Response.WriteAsJsonAsync(new
        {
            type = "https://tools.ietf.org/html/rfc9110#section-15.5.4",
            title = "Somente leitura",
            status = StatusCodes.Status403Forbidden,
            detail = ReadOnlyMode.Message,
            code = "read_only_mode"
        });
    }
}

public sealed class ReadOnlyCommandInterceptor : DbCommandInterceptor
{
    private static readonly string[] WriteVerbs =
        { "INSERT", "UPDATE", "DELETE", "ALTER", "CREATE", "DROP", "TRUNCATE", "GRANT", "REVOKE", "MERGE", "COPY", "VACUUM" };

    /// <summary>True se o comando começa com um verbo de escrita (ignora espaços e comentários iniciais).</summary>
    public static bool IsWrite(string? commandText)
    {
        if (string.IsNullOrWhiteSpace(commandText)) return false;
        var text = commandText.AsSpan().TrimStart();
        while (text.StartsWith("--"))
        {
            var nl = text.IndexOf('\n');
            if (nl < 0) return false;
            text = text[(nl + 1)..].TrimStart();
        }
        foreach (var verb in WriteVerbs)
            if (text.StartsWith(verb, StringComparison.OrdinalIgnoreCase)
                && (text.Length == verb.Length || !char.IsLetterOrDigit(text[verb.Length])))
                return true;
        return false;
    }

    private static void Guard(DbCommand command)
    {
        if (IsWrite(command.CommandText))
            throw new InvalidOperationException(ReadOnlyMode.Message);
    }

    public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    { Guard(command); return result; }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    { Guard(command); return ValueTask.FromResult(result); }

    public override InterceptionResult<int> NonQueryExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
    { Guard(command); return result; }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    { Guard(command); return ValueTask.FromResult(result); }

    public override InterceptionResult<object> ScalarExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
    { Guard(command); return result; }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<object> result, CancellationToken cancellationToken = default)
    { Guard(command); return ValueTask.FromResult(result); }
}

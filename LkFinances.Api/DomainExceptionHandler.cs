using LkFinances.Core.Domain;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LkFinances.Api;

public sealed class DomainExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception exception, CancellationToken ct)
    {
        if (exception is not DomainException domain)
            return false;

        var problem = TypedResults.ValidationProblem(new Dictionary<string, string[]> { [domain.Field] = [domain.Message] });
        await problem.ExecuteAsync(http);
        return true;
    }
}

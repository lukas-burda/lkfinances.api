using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace LkFinances.Core.Data;

public static class DbUpdateExceptionExtensions
{
    public static bool IsUniqueViolation(this DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}

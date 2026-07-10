using Microsoft.EntityFrameworkCore;

namespace LexFlow.Infrastructure.Persistence;

/// <summary>
/// Sets the Postgres session variable `app.tenant_id` that every Row-Level Security
/// policy reads (PRD §14: "API sets app.tenant_id per connection from JWT"; §20(5):
/// tenant isolation = EF global filter + PG RLS). Must run once per request, before
/// any query, on the same pooled connection the rest of the request's queries reuse.
/// </summary>
public static class DbContextTenantExtensions
{
    public static async Task SetTenantIdAsync(this DbContext context, Guid tenantId, CancellationToken cancellationToken = default)
    {
        await context.Database.OpenConnectionAsync(cancellationToken);

        var connection = context.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT set_config('app.tenant_id', @tenant_id, false)";

        var parameter = command.CreateParameter();
        parameter.ParameterName = "tenant_id";
        parameter.Value = tenantId.ToString();
        command.Parameters.Add(parameter);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}

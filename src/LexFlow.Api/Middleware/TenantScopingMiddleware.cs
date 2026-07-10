using LexFlow.Application.Common.Interfaces;
using LexFlow.Infrastructure.Persistence;

namespace LexFlow.Api.Middleware;

/// <summary>
/// Runs after authentication so the tenant claim is available, and before any
/// controller/handler touches the database: executes `SET app.tenant_id` for this
/// request's DbContext so PostgreSQL RLS policies apply (PRD §14, §20(5)).
/// </summary>
public sealed class TenantScopingMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ICurrentUserService currentUser, LexFlowDbContext dbContext)
    {
        if (currentUser.TenantId is { } tenantId)
        {
            await dbContext.SetTenantIdAsync(tenantId, context.RequestAborted);
        }

        await next(context);
    }
}

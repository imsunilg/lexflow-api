using LexFlow.Application.Common.Interfaces;
using StackExchange.Redis;

namespace LexFlow.Infrastructure.Security;

/// <summary>
/// AC-U1 backing store: a Redis key with a TTL slightly longer than the access-token
/// lifetime (PRD §20(3): 15 min) — long enough that no token issued before
/// deactivation can outlive the denylist entry, short enough not to grow unbounded.
/// </summary>
public sealed class RedisUserDenylistService(IConnectionMultiplexer redis) : IUserDenylistService
{
    private static readonly TimeSpan DenylistTtl = TimeSpan.FromMinutes(20);

    public async Task DenylistAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var cache = redis.GetDatabase();
        await cache.StringSetAsync(CacheKey(userId), "1", DenylistTtl);
    }

    public async Task<bool> IsDenylistedAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var cache = redis.GetDatabase();
        return await cache.KeyExistsAsync(CacheKey(userId));
    }

    private static string CacheKey(Guid userId) => $"user-denylist:{userId:N}";
}

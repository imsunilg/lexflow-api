using System.Text.Json;
using System.Text.Json.Nodes;

namespace LexFlow.Domain.Entities;

/// <summary>
/// Maps column-for-column to fin.running_timers (lexflow-database Scripts/06_Fin/RunningTimers).
/// Single-column PK (user_id) join-style table — see TaskAssignee for why this does not derive
/// from Entity/AuditableEntity. No soft-delete columns on this table (a stopped timer is deleted
/// outright, not soft-deleted — it becomes a fin.time_entries Draft row instead).
/// Module 9: "Only one running timer per user... server-anchored start timestamp; heartbeat".
/// The table has no dedicated "paused since" column, so a running-vs-paused timer's pause
/// instant is stashed as a reserved "__pausedAt" key inside the existing context jsonb column
/// alongside the caller's own contextRef payload — documented reuse rather than a schema change.
/// </summary>
public sealed class RunningTimer
{
    private const string PausedAtKey = "__pausedAt";

    private RunningTimer()
    {
    }

    public RunningTimer(Guid tenantId, Guid userId, Guid? matterId, string contextJson)
    {
        TenantId = tenantId;
        UserId = userId;
        MatterId = matterId;
        StartedAt = DateTimeOffset.UtcNow;
        PausedMs = 0;
        ContextJson = contextJson;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid UserId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid? MatterId { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public long PausedMs { get; private set; }
    public string ContextJson { get; private set; } = "{}";
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public Guid? UpdatedBy { get; private set; }

    public bool IsPaused => ReadPausedAt() is not null;

    /// <summary>AC-T1: elapsed = now - started_at - total paused duration, computed server-side only (device clock skew is irrelevant).</summary>
    public TimeSpan Elapsed(DateTimeOffset asOf)
    {
        var pausedAt = ReadPausedAt();
        var stillPausedMs = pausedAt.HasValue ? (asOf - pausedAt.Value).TotalMilliseconds : 0;
        return asOf - StartedAt - TimeSpan.FromMilliseconds(PausedMs) - TimeSpan.FromMilliseconds(stillPausedMs);
    }

    public void Pause(DateTimeOffset asOf)
    {
        if (ReadPausedAt() is null)
        {
            WritePausedAt(asOf);
        }
    }

    public void Resume(DateTimeOffset asOf)
    {
        var pausedAt = ReadPausedAt();
        if (pausedAt.HasValue)
        {
            PausedMs += (long)(asOf - pausedAt.Value).TotalMilliseconds;
            WritePausedAt(null);
        }
    }

    private DateTimeOffset? ReadPausedAt()
    {
        var node = JsonNode.Parse(ContextJson)?.AsObject();
        if (node is not null && node.TryGetPropertyValue(PausedAtKey, out var value) && value is not null)
        {
            return value.GetValue<DateTimeOffset>();
        }

        return null;
    }

    private void WritePausedAt(DateTimeOffset? value)
    {
        var node = JsonNode.Parse(ContextJson)?.AsObject() ?? [];
        if (value.HasValue)
        {
            node[PausedAtKey] = value.Value;
        }
        else
        {
            node.Remove(PausedAtKey);
        }

        ContextJson = node.ToJsonString();
    }
}

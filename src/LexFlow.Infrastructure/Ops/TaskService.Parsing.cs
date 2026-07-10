using System.Globalization;
using System.Text.RegularExpressions;
using LexFlow.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Infrastructure.Ops;

/// <summary>
/// AC-TK1 smart parse: "File written statement for MAT-042 by next Friday @Aditi !high"
/// -> {title, matter, assignee, due date, priority}. Regex-based (no NLP dependency) —
/// covers the exact token shapes the PRD's own example and UI copy use: `!priority`,
/// `@mention`, an all-caps hyphenated matter code, and "today/tomorrow/next &lt;weekday&gt;/in N days".
/// </summary>
public sealed partial class TaskService
{
    [GeneratedRegex(@"!(low|med(?:ium)?|high|urgent)\b", RegexOptions.IgnoreCase)]
    private static partial Regex PriorityRegex();

    [GeneratedRegex(@"@(\S+)")]
    private static partial Regex MentionRegex();

    [GeneratedRegex(@"\b([A-Z]{2,6}-\d{1,6})\b")]
    private static partial Regex MatterCodeRegex();

    [GeneratedRegex(@"\b(today|tomorrow|next\s+(monday|tuesday|wednesday|thursday|friday|saturday|sunday)|in\s+(\d+)\s+days?)\b", RegexOptions.IgnoreCase)]
    private static partial Regex DatePhraseRegex();

    public async Task<ParsedTaskDraft> ParseAsync(Guid tenantId, string text, CancellationToken cancellationToken = default)
    {
        var priority = "Medium";
        var priorityMatch = PriorityRegex().Match(text);
        if (priorityMatch.Success)
        {
            priority = priorityMatch.Groups[1].Value.ToLowerInvariant() switch
            {
                "low" => "Low",
                "high" => "High",
                "urgent" => "Urgent",
                _ => "Medium",
            };
        }

        string? mention = null;
        Guid? assigneeUserId = null;
        var mentionMatch = MentionRegex().Match(text);
        if (mentionMatch.Success)
        {
            mention = mentionMatch.Groups[1].Value;
            var tenantUsers = await db.Users.Where(u => u.TenantId == tenantId).OrderBy(u => u.Name).ToListAsync(cancellationToken);
            var user = tenantUsers.FirstOrDefault(u => u.Name.Contains(mention, StringComparison.OrdinalIgnoreCase));
            assigneeUserId = user?.Id;
        }

        string? matterNumber = null;
        Guid? matterId = null;
        var matterMatch = MatterCodeRegex().Match(text);
        if (matterMatch.Success)
        {
            matterNumber = matterMatch.Groups[1].Value;
            var matter = await db.Matters.SingleOrDefaultAsync(m => m.TenantId == tenantId && m.Number == matterNumber, cancellationToken);
            matterId = matter?.Id;
        }

        DateTimeOffset? dueAt = null;
        var dateMatch = DatePhraseRegex().Match(text);
        if (dateMatch.Success)
        {
            dueAt = ResolveRelativeDate(dateMatch.Value, DateTimeOffset.UtcNow);
        }

        var title = text;
        foreach (Match m in new[] { priorityMatch, mentionMatch, matterMatch, dateMatch }.Where(m => m.Success))
        {
            title = title.Replace(m.Value, string.Empty);
        }

        title = CleanupTitleRegex().Replace(title, " ").Trim(' ', '-', ',');

        return new ParsedTaskDraft(title, matterId, matterNumber, assigneeUserId, mention, dueAt, priority);
    }

    [GeneratedRegex(@"\b(for|by|on)\s*$|\s{2,}")]
    private static partial Regex CleanupTitleRegex();

    private static readonly string[] Weekdays = ["sunday", "monday", "tuesday", "wednesday", "thursday", "friday", "saturday"];

    private static DateTimeOffset ResolveRelativeDate(string phrase, DateTimeOffset now)
    {
        var lower = phrase.ToLowerInvariant().Trim();
        var today = now.Date;

        if (lower == "today")
        {
            return new DateTimeOffset(today, now.Offset);
        }

        if (lower == "tomorrow")
        {
            return new DateTimeOffset(today.AddDays(1), now.Offset);
        }

        var inDaysMatch = Regex.Match(lower, @"in\s+(\d+)\s+days?");
        if (inDaysMatch.Success)
        {
            return new DateTimeOffset(today.AddDays(int.Parse(inDaysMatch.Groups[1].Value, CultureInfo.InvariantCulture)), now.Offset);
        }

        var weekdayMatch = Regex.Match(lower, @"next\s+(\w+)");
        if (weekdayMatch.Success)
        {
            var targetIndex = Array.IndexOf(Weekdays, weekdayMatch.Groups[1].Value);
            if (targetIndex >= 0)
            {
                var currentIndex = (int)now.DayOfWeek;
                var daysAhead = ((targetIndex - currentIndex + 7 - 1) % 7) + 1;
                return new DateTimeOffset(today.AddDays(daysAhead), now.Offset);
            }
        }

        return now;
    }
}

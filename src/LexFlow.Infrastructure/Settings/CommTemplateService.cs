using System.Text.Json;
using System.Text.RegularExpressions;
using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Infrastructure.Settings;

/// <summary>
/// Module 15 §11 Email/SMS/WhatsApp Templates CRUD. Placeholder validation is a
/// warning, not a hard rejection (PRD Module 15 Validation: "unknown → warning, save
/// allowed with flag") — UnknownPlaceholders on the DTO carries that flag.
/// </summary>
public sealed partial class CommTemplateService(LexFlowDbContext db) : ICommTemplateService
{
    public async Task<IReadOnlyList<CommTemplateDto>> GetAllAsync(Guid tenantId, string? channel, CancellationToken cancellationToken = default)
    {
        var query = db.CommTemplates.Where(t => t.TenantId == tenantId);
        if (!string.IsNullOrWhiteSpace(channel))
        {
            query = query.Where(t => t.Channel == channel);
        }

        var templates = await query.OrderBy(t => t.Name).ToListAsync(cancellationToken);
        return templates.Select(ToDto).ToList();
    }

    public async Task<CommTemplateDto> CreateAsync(Guid tenantId, string channel, string name, string body, string variablesJson, CancellationToken cancellationToken = default)
    {
        var template = new CommTemplate(tenantId, channel, name, body, variablesJson);
        await db.CommTemplates.AddAsync(template, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(template);
    }

    public async Task<CommTemplateDto> UpdateAsync(Guid tenantId, Guid id, string body, string variablesJson, bool isActive, CancellationToken cancellationToken = default)
    {
        var template = await db.CommTemplates.SingleOrDefaultAsync(t => t.TenantId == tenantId && t.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(CommTemplate), id);

        template.UpdateBody(body, variablesJson);
        template.SetActive(isActive);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(template);
    }

    public async Task DeleteAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
    {
        var template = await db.CommTemplates.SingleOrDefaultAsync(t => t.TenantId == tenantId && t.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(CommTemplate), id);

        db.CommTemplates.Remove(template);
        await db.SaveChangesAsync(cancellationToken);
    }

    [GeneratedRegex(@"\{\{(?<name>[a-zA-Z0-9_]+)\}\}")]
    private static partial Regex PlaceholderRegex();

    private static CommTemplateDto ToDto(CommTemplate template)
    {
        var declaredVariables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var element in JsonDocument.Parse(template.VariablesJson).RootElement.EnumerateArray())
            {
                if (element.GetString() is { } name)
                {
                    declaredVariables.Add(name);
                }
            }
        }
        catch (JsonException)
        {
            // Malformed variables catalog — treat as empty rather than fail the whole read.
        }

        var usedPlaceholders = PlaceholderRegex().Matches(template.Body).Select(m => m.Groups["name"].Value).Distinct();
        var unknown = usedPlaceholders.Where(p => !declaredVariables.Contains(p)).ToList();

        return new CommTemplateDto(
            template.Id,
            template.Channel,
            template.Name,
            template.Body,
            template.VariablesJson,
            template.DltTemplateId,
            template.WaHsmName,
            template.IsActive,
            unknown);
    }
}

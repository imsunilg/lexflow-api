using FluentAssertions;
using LexFlow.Application.Common.Exceptions;
using LexFlow.Infrastructure.Persistence;
using LexFlow.Infrastructure.Settings;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.UnitTests.Settings;

/// <summary>Module 15 Validation: "Section JSON-schema validated" — and collection-shaped sections reject PUT.</summary>
public sealed class SettingsServiceTests
{
    private static (SettingsService Service, Guid TenantId) CreateService(string dbName)
    {
        var db = new LexFlowDbContext(new DbContextOptionsBuilder<LexFlowDbContext>().UseInMemoryDatabase(dbName).Options);
        var gatewayConfigService = new GatewayConfigService(db);
        return (new SettingsService(db, gatewayConfigService), Guid.NewGuid());
    }

    [Fact]
    public async Task UpdateSectionAsync_accepts_a_schema_conformant_theme_section()
    {
        var (service, tenantId) = CreateService(nameof(UpdateSectionAsync_accepts_a_schema_conformant_theme_section));

        var result = await service.UpdateSectionAsync(tenantId, Guid.NewGuid(), "theme", """{"default":"Dark","allowUserOverride":true}""");

        result.Should().Contain("Dark");
    }

    [Fact]
    public async Task UpdateSectionAsync_rejects_a_theme_section_missing_the_required_default_field()
    {
        var (service, tenantId) = CreateService(nameof(UpdateSectionAsync_rejects_a_theme_section_missing_the_required_default_field));

        var act = () => service.UpdateSectionAsync(tenantId, Guid.NewGuid(), "theme", """{"allowUserOverride":true}""");

        await act.Should().ThrowAsync<Application.Common.Exceptions.ValidationException>();
    }

    [Fact]
    public async Task UpdateSectionAsync_rejects_a_security_section_below_the_minimum_password_length()
    {
        var (service, tenantId) = CreateService(nameof(UpdateSectionAsync_rejects_a_security_section_below_the_minimum_password_length));

        var act = () => service.UpdateSectionAsync(
            tenantId,
            Guid.NewGuid(),
            "security",
            """{"passwordMinLength":6,"twoFactorEnforcement":"optional","sessionTimeoutMinutes":30}""");

        await act.Should().ThrowAsync<Application.Common.Exceptions.ValidationException>();
    }

    [Fact]
    public async Task UpdateSectionAsync_rejects_writes_to_collection_shaped_sections()
    {
        var (service, tenantId) = CreateService(nameof(UpdateSectionAsync_rejects_writes_to_collection_shaped_sections));

        var act = () => service.UpdateSectionAsync(tenantId, Guid.NewGuid(), "taxes", "[]");

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task UpdateSectionAsync_never_persists_a_gateway_secret_into_config_json()
    {
        var (service, tenantId) = CreateService(nameof(UpdateSectionAsync_never_persists_a_gateway_secret_into_config_json));

        var result = await service.UpdateSectionAsync(
            tenantId,
            Guid.NewGuid(),
            "smtp",
            """{"host":"smtp.example.com","port":587,"tlsMode":"StartTls","fromAddress":"noreply@example.com","secret":"super-secret-password"}""");

        result.Should().NotContain("super-secret-password");
    }
}

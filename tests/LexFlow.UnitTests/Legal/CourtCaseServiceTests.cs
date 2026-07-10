using FluentAssertions;
using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Legal;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.UnitTests.Legal;

/// <summary>Case-duplicate guard and AC-CC4 appeal-hierarchy validation, against EF InMemory.</summary>
public sealed class CourtCaseServiceTests
{
    private static LexFlowDbContext CreateContext(string dbName) => new(
        new DbContextOptionsBuilder<LexFlowDbContext>().UseInMemoryDatabase(dbName).Options);

    private sealed class NoOpConflictCheckService : IConflictCheckService
    {
        public Task<IReadOnlyList<ConflictMatch>> CheckAsync(Guid tenantId, IReadOnlyList<string> partyNames, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ConflictMatch>>([]);

        public Task IndexPartyAsync(Guid tenantId, string name, string sourceType, Guid sourceId, Guid? matterId, string? matterNumber, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private static async Task<(LexFlowDbContext Db, CourtCaseService Service, Guid TenantId, Matter Matter)> SetupAsync(string dbName)
    {
        var db = CreateContext(dbName);
        var tenantId = Guid.NewGuid();
        var client = new Client(tenantId, "CL-1", "Individual", "A", "B", null, null, null, null, null, null, null, null);
        var matter = new Matter(tenantId, "MAT-1", "Test matter", client.Id, "Litigation", null, null, null, "Medium", null, DateOnly.FromDateTime(DateTime.UtcNow), false, null, "{}");
        db.AddRange(client, matter);
        await db.SaveChangesAsync();

        return (db, new CourtCaseService(db, new NoOpConflictCheckService()), tenantId, matter);
    }

    [Fact]
    public async Task CreateAsync_rejects_a_duplicate_court_type_number_year_combination()
    {
        var (db, service, tenantId, matter) = await SetupAsync(nameof(CreateAsync_rejects_a_duplicate_court_type_number_year_combination));
        var court = new Court(tenantId, "District Court", "District", null, null, null);
        await db.Courts.AddAsync(court);
        await db.SaveChangesAsync();

        await service.CreateAsync(tenantId, matter.Id, new CreateCourtCaseInput(court.Id, "CS", "100", 2026, null, null, null, null, null), CancellationToken.None);

        var act = () => service.CreateAsync(tenantId, matter.Id, new CreateCourtCaseInput(court.Id, "CS", "100", 2026, null, null, null, null, null), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().Where(e => e.Code == "CASE_DUPLICATE");
    }

    [Fact]
    public async Task FileAppealAsync_rejects_a_target_court_whose_level_does_not_exceed_the_current_one()
    {
        var (db, service, tenantId, matter) = await SetupAsync(nameof(FileAppealAsync_rejects_a_target_court_whose_level_does_not_exceed_the_current_one));
        var highCourt = new Court(tenantId, "High Court", "High", null, null, null);
        var districtCourt = new Court(tenantId, "District Court", "District", null, null, null);
        db.AddRange(highCourt, districtCourt);
        await db.SaveChangesAsync();

        var courtCase = await service.CreateAsync(tenantId, matter.Id, new CreateCourtCaseInput(highCourt.Id, "CS", "1", 2026, null, null, null, null, null), CancellationToken.None);

        var act = () => service.FileAppealAsync(tenantId, null, courtCase.Id, districtCourt.Id, [], CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().Where(e => e.Code == "APPEAL_TARGET_LEVEL_TOO_LOW");
    }

    [Fact]
    public async Task FileAppealAsync_creates_a_linked_case_in_a_higher_court()
    {
        var (db, service, tenantId, matter) = await SetupAsync(nameof(FileAppealAsync_creates_a_linked_case_in_a_higher_court));
        var districtCourt = new Court(tenantId, "District Court", "District", null, null, null);
        var highCourt = new Court(tenantId, "High Court", "High", null, null, null);
        db.AddRange(districtCourt, highCourt);
        await db.SaveChangesAsync();

        var courtCase = await service.CreateAsync(tenantId, matter.Id, new CreateCourtCaseInput(districtCourt.Id, "CS", "1", 2026, null, null, null, null, null), CancellationToken.None);

        var appeal = await service.FileAppealAsync(tenantId, null, courtCase.Id, highCourt.Id, [], CancellationToken.None);

        appeal.AppealOfCaseId.Should().Be(courtCase.Id);
        appeal.CourtId.Should().Be(highCourt.Id);
    }
}

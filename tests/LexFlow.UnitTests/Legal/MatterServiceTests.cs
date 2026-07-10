using FluentAssertions;
using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Infrastructure.Legal;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.UnitTests.Legal;

/// <summary>AC-M1 (conflict-check gate), AC-M3 (closing checklist / reopen permission), against EF InMemory.</summary>
public sealed class MatterServiceTests
{
    private static LexFlowDbContext CreateContext(string dbName) => new(
        new DbContextOptionsBuilder<LexFlowDbContext>().UseInMemoryDatabase(dbName).Options);

    private static CreateMatterInput Input(bool overrideConflict = false, string? reason = null, IReadOnlyList<string>? opposite = null) => new(
        "Test matter", Guid.NewGuid(), "Litigation", null, null, Guid.NewGuid(), "Medium", null,
        DateOnly.FromDateTime(DateTime.UtcNow), false, null, "{}", opposite, overrideConflict, reason);

    [Fact]
    public async Task CreateAsync_blocks_on_a_conflict_hit_unless_overridden()
    {
        await using var db = CreateContext(nameof(CreateAsync_blocks_on_a_conflict_hit_unless_overridden));
        var conflictCheck = new FakeConflictCheckService(hasMatch: true);
        var service = new MatterService(db, conflictCheck);
        var tenantId = Guid.NewGuid();

        var act = () => service.CreateAsync(tenantId, null, Input(opposite: ["Acme Corp"]), CancellationToken.None);

        await act.Should().ThrowAsync<DomainRuleException>().Where(e => e.SubCode == "CONFLICT_OF_INTEREST_SUSPECTED");
    }

    [Fact]
    public async Task CreateAsync_allows_the_same_conflict_hit_when_overridden_with_a_reason()
    {
        await using var db = CreateContext(nameof(CreateAsync_allows_the_same_conflict_hit_when_overridden_with_a_reason));
        var conflictCheck = new FakeConflictCheckService(hasMatch: true);
        var service = new MatterService(db, conflictCheck);
        var tenantId = Guid.NewGuid();

        var matter = await service.CreateAsync(tenantId, null, Input(overrideConflict: true, reason: "Reviewed — different Acme entity, no actual conflict.", opposite: ["Acme Corp"]), CancellationToken.None);

        matter.Id.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ChangeStatusAsync_requires_a_closure_note_to_close()
    {
        await using var db = CreateContext(nameof(ChangeStatusAsync_requires_a_closure_note_to_close));
        var service = new MatterService(db, new FakeConflictCheckService(hasMatch: false));
        var tenantId = Guid.NewGuid();
        var matter = await service.CreateAsync(tenantId, null, Input(), CancellationToken.None);

        var act = () => service.ChangeStatusAsync(tenantId, null, matter.Id, "Closed", "Won", closureNote: null, allowReopen: false, CancellationToken.None);

        await act.Should().ThrowAsync<Application.Common.Exceptions.ValidationException>();
    }

    [Fact]
    public async Task ChangeStatusAsync_blocks_reopen_without_the_reopen_permission()
    {
        await using var db = CreateContext(nameof(ChangeStatusAsync_blocks_reopen_without_the_reopen_permission));
        var service = new MatterService(db, new FakeConflictCheckService(hasMatch: false));
        var tenantId = Guid.NewGuid();
        var matter = await service.CreateAsync(tenantId, null, Input(), CancellationToken.None);
        await service.ChangeStatusAsync(tenantId, null, matter.Id, "Closed", "Won", "All done.", allowReopen: false, CancellationToken.None);

        var act = () => service.ChangeStatusAsync(tenantId, null, matter.Id, "Reopened", null, "Client disputes settlement.", allowReopen: false, CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public async Task ChangeStatusAsync_allows_reopen_with_the_reopen_permission_and_a_reason()
    {
        await using var db = CreateContext(nameof(ChangeStatusAsync_allows_reopen_with_the_reopen_permission_and_a_reason));
        var service = new MatterService(db, new FakeConflictCheckService(hasMatch: false));
        var tenantId = Guid.NewGuid();
        var matter = await service.CreateAsync(tenantId, null, Input(), CancellationToken.None);
        await service.ChangeStatusAsync(tenantId, null, matter.Id, "Closed", "Won", "All done.", allowReopen: false, CancellationToken.None);

        var reopened = await service.ChangeStatusAsync(tenantId, null, matter.Id, "Reopened", null, "Client disputes settlement.", allowReopen: true, CancellationToken.None);

        reopened.Status.Should().Be("Reopened");
    }

    private sealed class FakeConflictCheckService(bool hasMatch) : IConflictCheckService
    {
        public Task<IReadOnlyList<ConflictMatch>> CheckAsync(Guid tenantId, IReadOnlyList<string> partyNames, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ConflictMatch>>(hasMatch ? [new ConflictMatch("Acme Corp", 0.9, "matter_party", Guid.NewGuid(), Guid.NewGuid(), "MAT-0001")] : []);

        public Task IndexPartyAsync(Guid tenantId, string name, string sourceType, Guid sourceId, Guid? matterId, string? matterNumber, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}

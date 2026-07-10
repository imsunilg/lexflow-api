using LexFlow.Application.Common.Interfaces;

namespace LexFlow.Infrastructure.Reporting;

/// <summary>Shared row-level scope predicate used by both <c>StandardReportService</c> and <c>CustomReportService</c> — the one place PRD Module 13 Security's "injecting scope predicates into every report query" is actually implemented.</summary>
internal static class ReportScopeMatcher
{
    public static bool Matches(ReportScope scope, Guid? lawyerId, Guid? branchId) => scope.Kind switch
    {
        "all" => true,
        "branch" => branchId.HasValue && branchId == scope.BranchId,
        "team" or "own" => lawyerId.HasValue && (scope.LawyerKeys?.Contains(lawyerId.Value) ?? false),
        _ => false,
    };
}

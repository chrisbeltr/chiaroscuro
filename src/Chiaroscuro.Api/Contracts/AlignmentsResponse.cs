namespace Chiaroscuro.Api.Contracts;

public sealed record AlignmentsResponse(IReadOnlyList<AlignmentMatchDto> Matches);

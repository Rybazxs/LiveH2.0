namespace LiveH.Contracts.Today;

public sealed record MissionSummary(int Total, int Completed, int Overdue)
{
    public int Remaining => Math.Max(0, Total - Completed);
}

public sealed record BioNotice(string Id, string Title, string Message);

// Immutable projection for the Today UI. No health or mission business logic here.
public sealed record TodayReadModel(
    DateOnly Date,
    MissionSummary Missions,
    IReadOnlyList<BioNotice> BioNotices);

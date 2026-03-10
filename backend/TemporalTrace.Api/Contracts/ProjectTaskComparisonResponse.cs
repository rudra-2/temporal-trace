namespace TemporalTrace.Api.Contracts;

public class ProjectTaskComparisonResponse
{
    public required ProjectTaskResponse Historical { get; set; }
    public required ProjectTaskResponse Current { get; set; }
    public required IReadOnlyList<string> ChangedFields { get; set; }
}

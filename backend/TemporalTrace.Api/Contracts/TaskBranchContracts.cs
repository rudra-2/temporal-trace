namespace TemporalTrace.Api.Contracts;

public class CreateTaskBranchRequest
{
    public DateTime TargetTime { get; set; }
    public string BranchName { get; set; } = string.Empty;
}

public class TaskBranchResponse
{
    public Guid BranchId { get; set; }
    public int TaskId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public DateTime CreatedFromTime { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsMainTimeline { get; set; }
    public bool HasOverrides { get; set; }
}

public class UpdateTaskBranchOverrideRequest
{
    public string? OverrideTitle { get; set; }
    public string? OverrideDescription { get; set; }
    public string? OverrideStatus { get; set; }
    public int? OverridePriority { get; set; }
}

public class BranchTimelineResponse
{
    public Guid BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public bool IsMainTimeline { get; set; }
    public ProjectTaskResponse? MainTaskSnapshot { get; set; }
    public ProjectTaskResponse? BranchTaskSnapshot { get; set; }
    public List<string> ChangedFields { get; set; } = [];
}

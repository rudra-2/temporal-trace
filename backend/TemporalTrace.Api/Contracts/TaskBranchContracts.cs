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
}

public class BranchTimelineResponse
{
    public Guid BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public bool IsMainTimeline { get; set; }
    public ProjectTaskResponse? TaskSnapshot { get; set; }
}

namespace TemporalTrace.Api.Models;

public class TaskBranch
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int TaskId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public DateTime CreatedFromTime { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsMainTimeline { get; set; } = false;
}

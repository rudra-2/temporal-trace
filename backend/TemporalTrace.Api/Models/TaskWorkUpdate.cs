namespace TemporalTrace.Api.Models;

public class TaskWorkUpdate
{
    public int Id { get; set; }
    public int TaskId { get; set; }
    public string Note { get; set; } = string.Empty;
    public string? StatusAfter { get; set; }
    public int? MinutesSpent { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

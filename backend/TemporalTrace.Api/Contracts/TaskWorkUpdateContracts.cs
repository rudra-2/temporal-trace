using System.ComponentModel.DataAnnotations;

namespace TemporalTrace.Api.Contracts;

public class CreateTaskWorkUpdateRequest
{
    [Required]
    [StringLength(1000, MinimumLength = 1)]
    public string Note { get; set; } = string.Empty;

    [StringLength(50)]
    public string? StatusAfter { get; set; }

    [Range(1, 1440)]
    public int? MinutesSpent { get; set; }
}

public class TaskWorkUpdateResponse
{
    public int Id { get; set; }
    public int TaskId { get; set; }
    public string Note { get; set; } = string.Empty;
    public string? StatusAfter { get; set; }
    public int? MinutesSpent { get; set; }
    public DateTime CreatedAt { get; set; }
}

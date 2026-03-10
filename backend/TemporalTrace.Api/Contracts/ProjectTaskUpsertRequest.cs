using System.ComponentModel.DataAnnotations;

namespace TemporalTrace.Api.Contracts;

public class ProjectTaskUpsertRequest
{
    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string Title { get; set; } = string.Empty;

    [StringLength(2000)]
    public string Description { get; set; } = string.Empty;

    [Required]
    [StringLength(50, MinimumLength = 1)]
    public string Status { get; set; } = string.Empty;

    [Range(1, 5)]
    public int Priority { get; set; }
}

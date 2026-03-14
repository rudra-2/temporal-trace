using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TemporalTrace.Api.Contracts;
using TemporalTrace.Api.Data;
using TemporalTrace.Api.Hubs;
using TemporalTrace.Api.Models;

namespace TemporalTrace.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TaskController(AppDbContext dbContext, IHubContext<TemporalHub> hubContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ProjectTaskResponse>>> GetTasks()
    {
        var tasks = await dbContext.ProjectTasks
            .AsNoTracking()
            .OrderBy(t => t.Id)
            .Select(t => ToResponse(t))
            .ToListAsync();

        return Ok(tasks);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ProjectTaskResponse>> GetTask(int id)
    {
        var task = await dbContext.ProjectTasks
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id);

        return task is null ? NotFound() : Ok(ToResponse(task));
    }

    [HttpGet("{id:int}/at")]
    public async Task<ActionResult<ProjectTaskResponse>> GetTaskAtTime(int id, [FromQuery] DateTime targetTime)
    {
        if (targetTime == default)
        {
            return BadRequest("targetTime query parameter is required.");
        }

        if (targetTime.Kind == DateTimeKind.Unspecified)
        {
            return BadRequest("targetTime must include timezone information. Use UTC (e.g., 2026-03-10T18:00:00Z).");
        }

        var asOfUtc = targetTime.Kind switch
        {
            DateTimeKind.Utc => targetTime,
            DateTimeKind.Local => targetTime.ToUniversalTime(),
            _ => targetTime
        };

        if (asOfUtc > DateTime.UtcNow)
        {
            return BadRequest("targetTime cannot be in the future.");
        }

        var taskAtTime = await dbContext.ProjectTasks
            .TemporalAsOf(asOfUtc)
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id);

        return taskAtTime is null ? NotFound() : Ok(ToResponse(taskAtTime));
    }

    [HttpGet("{id:int}/compare")]
    public async Task<ActionResult<ProjectTaskComparisonResponse>> CompareTaskAtTime(int id, [FromQuery] DateTime targetTime)
    {
        if (targetTime == default)
        {
            return BadRequest("targetTime query parameter is required.");
        }

        if (targetTime.Kind == DateTimeKind.Unspecified)
        {
            return BadRequest("targetTime must include timezone information. Use UTC (e.g., 2026-03-10T18:00:00Z).");
        }

        var asOfUtc = targetTime.Kind switch
        {
            DateTimeKind.Utc => targetTime,
            DateTimeKind.Local => targetTime.ToUniversalTime(),
            _ => targetTime
        };

        if (asOfUtc > DateTime.UtcNow)
        {
            return BadRequest("targetTime cannot be in the future.");
        }

        var historical = await dbContext.ProjectTasks
            .TemporalAsOf(asOfUtc)
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id);

        if (historical is null)
        {
            return NotFound();
        }

        var current = await dbContext.ProjectTasks
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id);

        if (current is null)
        {
            return NotFound();
        }

        var changedFields = new List<string>();
        if (!string.Equals(historical.Title, current.Title, StringComparison.Ordinal)) changedFields.Add("title");
        if (!string.Equals(historical.Description, current.Description, StringComparison.Ordinal)) changedFields.Add("description");
        if (!string.Equals(historical.Status, current.Status, StringComparison.Ordinal)) changedFields.Add("status");
        if (historical.Priority != current.Priority) changedFields.Add("priority");

        var response = new ProjectTaskComparisonResponse
        {
            Historical = ToResponse(historical),
            Current = ToResponse(current),
            ChangedFields = changedFields
        };

        return Ok(response);
    }

    [HttpGet("at")]
    public async Task<ActionResult<IEnumerable<ProjectTaskResponse>>> GetTasksAtTime([FromQuery] DateTime targetTime)
    {
        if (targetTime == default)
        {
            return BadRequest("targetTime query parameter is required.");
        }

        if (targetTime.Kind == DateTimeKind.Unspecified)
        {
            return BadRequest("targetTime must include timezone information. Use UTC (e.g., 2026-03-10T18:00:00Z).");
        }

        var asOfUtc = targetTime.Kind switch
        {
            DateTimeKind.Utc => targetTime,
            DateTimeKind.Local => targetTime.ToUniversalTime(),
            _ => targetTime
        };

        if (asOfUtc > DateTime.UtcNow)
        {
            return BadRequest("targetTime cannot be in the future.");
        }

        var tasksAtTime = await dbContext.ProjectTasks
            .TemporalAsOf(asOfUtc)
            .AsNoTracking()
            .OrderBy(t => t.Id)
            .Select(t => ToResponse(t))
            .ToListAsync();

        return Ok(tasksAtTime);
    }

    [HttpPost]
    public async Task<ActionResult<ProjectTaskResponse>> CreateTask([FromBody] ProjectTaskUpsertRequest request)
    {
        var task = new ProjectTask
        {
            Title = request.Title,
            Description = request.Description,
            Status = request.Status,
            Priority = request.Priority,
            UpdatedAt = DateTime.UtcNow,
            CompletedAt = IsDoneStatus(request.Status) ? DateTime.UtcNow : null
        };

        dbContext.ProjectTasks.Add(task);
        await dbContext.SaveChangesAsync();

        var response = ToResponse(task);
        await hubContext.Clients.All.SendAsync("taskUpdated", response);
        return CreatedAtAction(nameof(GetTask), new { id = task.Id }, response);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ProjectTaskResponse>> UpdateTask(int id, [FromBody] ProjectTaskUpsertRequest request)
    {
        var existing = await dbContext.ProjectTasks.FirstOrDefaultAsync(t => t.Id == id);
        if (existing is null)
        {
            return NotFound();
        }

        existing.Title = request.Title;
        existing.Description = request.Description;
        existing.Status = request.Status;
        existing.Priority = request.Priority;
        existing.UpdatedAt = DateTime.UtcNow;
        existing.CompletedAt = IsDoneStatus(existing.Status) ? DateTime.UtcNow : null;

        await dbContext.SaveChangesAsync();
        var response = ToResponse(existing);
        await hubContext.Clients.All.SendAsync("taskUpdated", response);
        return Ok(response);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteTask(int id)
    {
        var existing = await dbContext.ProjectTasks.FirstOrDefaultAsync(t => t.Id == id);
        if (existing is null)
        {
            return NotFound();
        }

        var deletedSnapshot = ToResponse(existing);
        dbContext.ProjectTasks.Remove(existing);
        await dbContext.SaveChangesAsync();

        await hubContext.Clients.All.SendAsync("taskDeleted", deletedSnapshot);

        return NoContent();
    }

    [HttpGet("{id:int}/updates")]
    public async Task<ActionResult<IEnumerable<TaskWorkUpdateResponse>>> GetTaskUpdates(int id)
    {
        var taskExists = await dbContext.ProjectTasks.AnyAsync(t => t.Id == id);
        if (!taskExists)
        {
            return NotFound("Task not found");
        }

        var updates = await dbContext.TaskWorkUpdates
            .AsNoTracking()
            .Where(u => u.TaskId == id)
            .OrderByDescending(u => u.CreatedAt)
            .Select(u => ToTaskWorkUpdateResponse(u))
            .ToListAsync();

        return Ok(updates);
    }

    [HttpPost("{id:int}/updates")]
    public async Task<ActionResult<TaskWorkUpdateResponse>> AddTaskUpdate(int id, [FromBody] CreateTaskWorkUpdateRequest request)
    {
        var task = await dbContext.ProjectTasks.FirstOrDefaultAsync(t => t.Id == id);
        if (task is null)
        {
            return NotFound("Task not found");
        }

        var trimmedStatus = string.IsNullOrWhiteSpace(request.StatusAfter)
            ? null
            : request.StatusAfter.Trim();

        var update = new TaskWorkUpdate
        {
            TaskId = id,
            Note = request.Note.Trim(),
            StatusAfter = trimmedStatus,
            MinutesSpent = request.MinutesSpent,
            CreatedAt = DateTime.UtcNow
        };

        dbContext.TaskWorkUpdates.Add(update);

        if (trimmedStatus is not null)
        {
            task.Status = trimmedStatus;
            task.CompletedAt = IsDoneStatus(trimmedStatus) ? DateTime.UtcNow : null;
        }

        task.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync();

        var taskResponse = ToResponse(task);
        await hubContext.Clients.All.SendAsync("taskUpdated", taskResponse);

        return Ok(ToTaskWorkUpdateResponse(update));
    }

    [HttpPost("{id:int}/branch")]
    public async Task<ActionResult<TaskBranchResponse>> CreateBranch(int id, [FromBody] CreateTaskBranchRequest request)
    {
        var taskExists = await dbContext.ProjectTasks.AnyAsync(t => t.Id == id);
        if (!taskExists)
        {
            return NotFound("Task not found");
        }

        var branch = new TaskBranch
        {
            TaskId = id,
            BranchName = request.BranchName,
            CreatedFromTime = request.TargetTime,
            CreatedAt = DateTime.UtcNow,
            IsMainTimeline = false
        };

        dbContext.TaskBranches.Add(branch);
        await dbContext.SaveChangesAsync();

        var response = new TaskBranchResponse
        {
            BranchId = branch.Id,
            TaskId = branch.TaskId,
            BranchName = branch.BranchName,
            CreatedFromTime = branch.CreatedFromTime,
            CreatedAt = branch.CreatedAt,
            IsMainTimeline = branch.IsMainTimeline,
            HasOverrides = false
        };

        return CreatedAtAction(nameof(GetBranch), new { taskId = id, branchId = branch.Id }, response);
    }

    [HttpGet("{id:int}/branches")]
    public async Task<ActionResult<IEnumerable<TaskBranchResponse>>> GetBranches(int id)
    {
        var taskExists = await dbContext.ProjectTasks.AnyAsync(t => t.Id == id);
        if (!taskExists)
        {
            return NotFound("Task not found");
        }

        var branches = await dbContext.TaskBranches
            .AsNoTracking()
            .Where(b => b.TaskId == id)
            .OrderBy(b => b.CreatedAt)
            .Select(b => new TaskBranchResponse
            {
                BranchId = b.Id,
                TaskId = b.TaskId,
                BranchName = b.BranchName,
                CreatedFromTime = b.CreatedFromTime,
                CreatedAt = b.CreatedAt,
                IsMainTimeline = b.IsMainTimeline,
                HasOverrides = b.OverrideTitle != null || b.OverrideDescription != null || b.OverrideStatus != null || b.OverridePriority != null
            })
            .ToListAsync();

        return Ok(branches);
    }

    [HttpGet("{taskId:int}/branch/{branchId:guid}")]
    public async Task<ActionResult<TaskBranchResponse>> GetBranch(int taskId, Guid branchId)
    {
        var branch = await dbContext.TaskBranches
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == branchId && b.TaskId == taskId);

        if (branch is null)
        {
            return NotFound("Branch not found");
        }

        var response = new TaskBranchResponse
        {
            BranchId = branch.Id,
            TaskId = branch.TaskId,
            BranchName = branch.BranchName,
            CreatedFromTime = branch.CreatedFromTime,
            CreatedAt = branch.CreatedAt,
            IsMainTimeline = branch.IsMainTimeline,
            HasOverrides = branch.OverrideTitle != null || branch.OverrideDescription != null || branch.OverrideStatus != null || branch.OverridePriority != null
        };

        return Ok(response);
    }

    [HttpGet("branch/{branchId:guid}/timeline")]
    public async Task<ActionResult<BranchTimelineResponse>> GetBranchTimeline(Guid branchId, [FromQuery] DateTime targetTime)
    {
        var branch = await dbContext.TaskBranches
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == branchId);

        if (branch is null)
        {
            return NotFound("Branch not found");
        }

        if (targetTime == default)
        {
            return BadRequest("targetTime query parameter is required.");
        }

        if (targetTime.Kind == DateTimeKind.Unspecified)
        {
            return BadRequest("targetTime must include timezone information. Use UTC (e.g., 2026-03-10T18:00:00Z).");
        }

        var asOfUtc = targetTime.Kind switch
        {
            DateTimeKind.Utc => targetTime,
            DateTimeKind.Local => targetTime.ToUniversalTime(),
            _ => targetTime
        };

        if (asOfUtc > DateTime.UtcNow)
        {
            return BadRequest("targetTime cannot be in the future.");
        }

        // Return base task snapshot from temporal query and overlay branch overrides.
        var mainTask = await dbContext.ProjectTasks
            .TemporalAsOf(asOfUtc)
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == branch.TaskId);

        if (mainTask is null)
        {
            return NotFound("Task snapshot not found for selected timestamp.");
        }

        var branchTaskSnapshot = new ProjectTaskResponse
        {
            Id = mainTask.Id,
            Title = branch.OverrideTitle ?? mainTask.Title,
            Description = branch.OverrideDescription ?? mainTask.Description,
            Status = branch.OverrideStatus ?? mainTask.Status,
            Priority = branch.OverridePriority ?? mainTask.Priority
        };

        var mainTaskSnapshot = ToResponse(mainTask);
        var changedFields = new List<string>();
        if (!string.Equals(mainTaskSnapshot.Title, branchTaskSnapshot.Title, StringComparison.Ordinal)) changedFields.Add("title");
        if (!string.Equals(mainTaskSnapshot.Description, branchTaskSnapshot.Description, StringComparison.Ordinal)) changedFields.Add("description");
        if (!string.Equals(mainTaskSnapshot.Status, branchTaskSnapshot.Status, StringComparison.Ordinal)) changedFields.Add("status");
        if (mainTaskSnapshot.Priority != branchTaskSnapshot.Priority) changedFields.Add("priority");

        var response = new BranchTimelineResponse
        {
            BranchId = branch.Id,
            BranchName = branch.BranchName,
            IsMainTimeline = branch.IsMainTimeline,
            MainTaskSnapshot = mainTaskSnapshot,
            BranchTaskSnapshot = branchTaskSnapshot,
            ChangedFields = changedFields
        };

        return Ok(response);
    }

    [HttpPut("branch/{branchId:guid}/override")]
    public async Task<ActionResult<TaskBranchResponse>> UpdateBranchOverride(Guid branchId, [FromBody] UpdateTaskBranchOverrideRequest request)
    {
        var branch = await dbContext.TaskBranches.FirstOrDefaultAsync(b => b.Id == branchId);
        if (branch is null)
        {
            return NotFound("Branch not found");
        }

        if (request.OverrideTitle is { Length: > 200 })
        {
            return BadRequest("overrideTitle must be 200 characters or fewer.");
        }

        if (request.OverrideDescription is { Length: > 2000 })
        {
            return BadRequest("overrideDescription must be 2000 characters or fewer.");
        }

        if (request.OverrideStatus is { Length: > 50 })
        {
            return BadRequest("overrideStatus must be 50 characters or fewer.");
        }

        branch.OverrideTitle = string.IsNullOrWhiteSpace(request.OverrideTitle) ? null : request.OverrideTitle.Trim();
        branch.OverrideDescription = string.IsNullOrWhiteSpace(request.OverrideDescription) ? null : request.OverrideDescription.Trim();
        branch.OverrideStatus = string.IsNullOrWhiteSpace(request.OverrideStatus) ? null : request.OverrideStatus.Trim();
        branch.OverridePriority = request.OverridePriority;

        await dbContext.SaveChangesAsync();

        return Ok(new TaskBranchResponse
        {
            BranchId = branch.Id,
            TaskId = branch.TaskId,
            BranchName = branch.BranchName,
            CreatedFromTime = branch.CreatedFromTime,
            CreatedAt = branch.CreatedAt,
            IsMainTimeline = branch.IsMainTimeline,
            HasOverrides = branch.OverrideTitle != null || branch.OverrideDescription != null || branch.OverrideStatus != null || branch.OverridePriority != null
        });
    }

    [HttpDelete("branch/{branchId:guid}")]
    public async Task<IActionResult> DeleteBranch(Guid branchId)
    {
        var branch = await dbContext.TaskBranches.FirstOrDefaultAsync(b => b.Id == branchId);
        if (branch is null)
        {
            return NotFound("Branch not found");
        }

        dbContext.TaskBranches.Remove(branch);
        await dbContext.SaveChangesAsync();

        return NoContent();
    }

    private static ProjectTaskResponse ToResponse(ProjectTask task)
    {
        return new ProjectTaskResponse
        {
            Id = task.Id,
            Title = task.Title,
            Description = task.Description,
            Status = task.Status,
            Priority = task.Priority,
            UpdatedAt = EnsureUtc(task.UpdatedAt),
            CompletedAt = EnsureUtc(task.CompletedAt)
        };
    }

    private static TaskWorkUpdateResponse ToTaskWorkUpdateResponse(TaskWorkUpdate update)
    {
        return new TaskWorkUpdateResponse
        {
            Id = update.Id,
            TaskId = update.TaskId,
            Note = update.Note,
            StatusAfter = update.StatusAfter,
            MinutesSpent = update.MinutesSpent,
            CreatedAt = EnsureUtc(update.CreatedAt)
        };
    }

    private static DateTime EnsureUtc(DateTime value)
    {
        return value.Kind == DateTimeKind.Utc
            ? value
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }

    private static DateTime? EnsureUtc(DateTime? value)
    {
        if (value is null)
        {
            return null;
        }

        return EnsureUtc(value.Value);
    }

    private static bool IsDoneStatus(string status)
    {
        return string.Equals(status, "done", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "completed", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "closed", StringComparison.OrdinalIgnoreCase);
    }
}

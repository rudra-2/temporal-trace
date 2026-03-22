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
    [HttpGet("timeline/window")]
    public async Task<ActionResult<TimelineWindowResponse>> GetTimelineWindow()
    {
        var nowUtc = DateTime.UtcNow;
        var yesterdayStartUtc = nowUtc.Date.AddDays(-1);
        var yesterdayEndUtc = nowUtc.Date;

        var firstYesterdayActivity = await dbContext.ProjectTasks
            .TemporalAll()
            .Select(t => EF.Property<DateTime>(t, "PeriodStart"))
            .Where(ts => ts >= yesterdayStartUtc && ts < yesterdayEndUtc)
            .OrderBy(ts => ts)
            .Select(ts => (DateTime?)ts)
            .FirstOrDefaultAsync();

        var minTimeUtc = firstYesterdayActivity.HasValue
            ? EnsureUtc(firstYesterdayActivity.Value)
            : nowUtc.AddHours(-24);

        return Ok(new TimelineWindowResponse
        {
            MinTime = minTimeUtc,
            MaxTime = nowUtc,
            YesterdayStartUtc = yesterdayStartUtc,
            YesterdayEndUtc = yesterdayEndUtc,
            UsedFallbackWindow = !firstYesterdayActivity.HasValue
        });
    }

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

    [HttpGet("{id:int}/replay")]
    public async Task<ActionResult<DecisionReplayResponse>> GetDecisionReplay(int id, [FromQuery] DateTime? targetTime)
    {
        var taskExists = await dbContext.ProjectTasks.AnyAsync(t => t.Id == id);
        if (!taskExists)
        {
            return NotFound("Task not found");
        }

        var asOfUtc = targetTime.HasValue
            ? NormalizeTargetTime(targetTime.Value)
            : DateTime.UtcNow;

        if (asOfUtc > DateTime.UtcNow)
        {
            return BadRequest("targetTime cannot be in the future.");
        }

        var events = new List<DecisionReplayEventResponse>();

        var versions = await dbContext.ProjectTasks
            .TemporalAll()
            .Where(t => t.Id == id)
            .Select(t => new
            {
                t.Id,
                t.Title,
                t.Description,
                t.Status,
                t.Priority,
                PeriodStart = EF.Property<DateTime>(t, "PeriodStart")
            })
            .OrderBy(v => v.PeriodStart)
            .ToListAsync();

        foreach (var version in versions.Where(v => EnsureUtc(v.PeriodStart) <= asOfUtc))
        {
            events.Add(new DecisionReplayEventResponse
            {
                Timestamp = EnsureUtc(version.PeriodStart),
                EventType = "TASK_SNAPSHOT",
                Title = $"State changed to {version.Status}",
                Details = $"Priority {version.Priority} | {version.Title}",
                Outcome = version.Description
            });
        }

        var updates = await dbContext.TaskWorkUpdates
            .AsNoTracking()
            .Where(u => u.TaskId == id && u.CreatedAt <= asOfUtc)
            .OrderBy(u => u.CreatedAt)
            .ToListAsync();

        foreach (var update in updates)
        {
            events.Add(new DecisionReplayEventResponse
            {
                Timestamp = EnsureUtc(update.CreatedAt),
                EventType = "WORK_UPDATE",
                Title = "Execution update recorded",
                Details = update.Note,
                Outcome = update.StatusAfter is null
                    ? "No status change"
                    : $"Status moved to {update.StatusAfter}"
            });
        }

        var branches = await dbContext.TaskBranches
            .AsNoTracking()
            .Where(b => b.TaskId == id && b.CreatedAt <= asOfUtc)
            .OrderBy(b => b.CreatedAt)
            .ToListAsync();

        foreach (var branch in branches)
        {
            events.Add(new DecisionReplayEventResponse
            {
                Timestamp = EnsureUtc(branch.CreatedAt),
                EventType = "BRANCH_CREATED",
                Title = $"Scenario branch created: {branch.BranchName}",
                Details = $"Created from {EnsureUtc(branch.CreatedFromTime):u}",
                Outcome = branch.OverrideStatus is null && branch.OverridePriority is null && branch.OverrideTitle is null && branch.OverrideDescription is null
                    ? "No overrides yet"
                    : "Overrides active"
            });
        }

        events = events.OrderBy(e => e.Timestamp).ToList();
        var summary = events.Count == 0
            ? "No replay events found for selected range."
            : $"Reconstructed {events.Count} events from temporal snapshots, execution logs, and branch actions.";

        return Ok(new DecisionReplayResponse
        {
            TaskId = id,
            GeneratedAt = DateTime.UtcNow,
            Events = events,
            Summary = summary
        });
    }

    [HttpGet("{id:int}/branches/score")]
    public async Task<ActionResult<BranchScoreResponse>> ScoreBranches(int id, [FromQuery] DateTime? targetTime)
    {
        var taskExists = await dbContext.ProjectTasks.AnyAsync(t => t.Id == id);
        if (!taskExists)
        {
            return NotFound("Task not found");
        }

        var asOfUtc = targetTime.HasValue
            ? NormalizeTargetTime(targetTime.Value)
            : DateTime.UtcNow;

        if (asOfUtc > DateTime.UtcNow)
        {
            return BadRequest("targetTime cannot be in the future.");
        }

        var mainTask = await dbContext.ProjectTasks
            .TemporalAsOf(asOfUtc)
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id);

        if (mainTask is null)
        {
            return NotFound("No main task snapshot available at targetTime.");
        }

        var branches = await dbContext.TaskBranches
            .AsNoTracking()
            .Where(b => b.TaskId == id)
            .OrderBy(b => b.CreatedAt)
            .ToListAsync();

        var mainProgress = ProgressScore(mainTask.Status);
        var results = new List<BranchScoreResultResponse>();

        foreach (var branch in branches)
        {
            var branchStatus = branch.OverrideStatus ?? mainTask.Status;
            var branchPriority = branch.OverridePriority ?? mainTask.Priority;

            var branchProgress = ProgressScore(branchStatus);
            var leadImpact = Math.Round(branchProgress - mainProgress, 2);
            var riskScore = Math.Round(RiskScore(branchStatus), 2);
            var effortCost = Math.Round(Math.Clamp((branchPriority * 15.0)
                + (branch.OverrideDescription is null ? 0 : 10)
                + (branch.OverrideTitle is null ? 0 : 6), 0, 100), 2);

            var overall = Math.Round(Math.Clamp(
                (50 + (leadImpact * 0.7))
                + ((100 - riskScore) * 0.2)
                + ((100 - effortCost) * 0.1),
                0,
                100), 2);

            var reasons = new List<string>();
            reasons.Add(leadImpact >= 0 ? "Faster projected flow" : "Slower projected flow");
            reasons.Add(riskScore >= 60 ? "Higher blocker risk" : "Lower blocker risk");
            reasons.Add(effortCost >= 70 ? "High execution cost" : "Manageable execution cost");

            results.Add(new BranchScoreResultResponse
            {
                BranchId = branch.Id,
                BranchName = branch.BranchName,
                LeadTimeImpact = leadImpact,
                RiskScore = riskScore,
                EffortCost = effortCost,
                OverallScore = overall,
                Reasons = reasons
            });
        }

        var best = results.OrderByDescending(r => r.OverallScore).FirstOrDefault();
        if (best is not null)
        {
            best.IsRecommended = true;
        }

        return Ok(new BranchScoreResponse
        {
            TaskId = id,
            TargetTime = asOfUtc,
            Branches = results
        });
    }

    [HttpGet("standup/daily")]
    public async Task<ActionResult<DailyStandupResponse>> GetDailyStandup([FromQuery] DateTime? targetDate)
    {
        var dayUtc = targetDate.HasValue
            ? NormalizeTargetTime(targetDate.Value).Date
            : DateTime.UtcNow.Date;
        var nextDayUtc = dayUtc.AddDays(1);

        var updates = await dbContext.TaskWorkUpdates
            .AsNoTracking()
            .Where(u => u.CreatedAt >= dayUtc && u.CreatedAt < nextDayUtc)
            .OrderByDescending(u => u.CreatedAt)
            .ToListAsync();

        var touchedTaskIds = updates.Select(u => u.TaskId).Distinct().ToList();
        var tasks = await dbContext.ProjectTasks
            .AsNoTracking()
            .Where(t => touchedTaskIds.Contains(t.Id) || (t.CompletedAt.HasValue && t.CompletedAt.Value >= dayUtc && t.CompletedAt.Value < nextDayUtc))
            .ToListAsync();

        var taskTitleById = tasks.ToDictionary(t => t.Id, t => t.Title);

        var completedToday = tasks
            .Where(t => t.CompletedAt.HasValue && t.CompletedAt.Value >= dayUtc && t.CompletedAt.Value < nextDayUtc)
            .Select(t => t.Title)
            .Distinct()
            .OrderBy(t => t)
            .ToList();

        var inProgressToday = updates
            .Where(u => string.Equals(u.StatusAfter, "InProgress", StringComparison.OrdinalIgnoreCase))
            .Select(u => taskTitleById.TryGetValue(u.TaskId, out var title) ? title : $"Task #{u.TaskId}")
            .Distinct()
            .OrderBy(t => t)
            .ToList();

        var blockedToday = updates
            .Where(u => string.Equals(u.StatusAfter, "Blocked", StringComparison.OrdinalIgnoreCase))
            .Select(u => taskTitleById.TryGetValue(u.TaskId, out var title) ? title : $"Task #{u.TaskId}")
            .Distinct()
            .OrderBy(t => t)
            .ToList();

        var highlights = updates
            .Take(5)
            .Select(u =>
            {
                var title = taskTitleById.TryGetValue(u.TaskId, out var found) ? found : $"Task #{u.TaskId}";
                return $"{title}: {u.Note}";
            })
            .ToList();

        var narrative =
            $"Daily Standup ({dayUtc:yyyy-MM-dd} UTC): "
            + $"Completed {completedToday.Count} item(s), progressed {inProgressToday.Count} item(s), and blocked {blockedToday.Count} item(s). "
            + (highlights.Count > 0
                ? $"Top update: {highlights[0]}"
                : "No execution updates were logged today.");

        return Ok(new DailyStandupResponse
        {
            TargetDate = dayUtc,
            CompletedToday = completedToday,
            InProgressToday = inProgressToday,
            BlockedToday = blockedToday,
            Highlights = highlights,
            Narrative = narrative
        });
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

    private static DateTime NormalizeTargetTime(DateTime value)
    {
        if (value.Kind == DateTimeKind.Unspecified)
        {
            return DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }

        return value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
    }

    private static double ProgressScore(string status)
    {
        return status.ToLowerInvariant() switch
        {
            "done" or "completed" or "closed" => 100,
            "inprogress" => 65,
            "open" or "todo" => 40,
            "blocked" => 20,
            _ => 45
        };
    }

    private static double RiskScore(string status)
    {
        return status.ToLowerInvariant() switch
        {
            "blocked" => 90,
            "open" => 55,
            "inprogress" => 40,
            "done" or "completed" or "closed" => 10,
            _ => 50
        };
    }
}

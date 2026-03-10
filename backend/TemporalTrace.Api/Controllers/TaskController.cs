using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TemporalTrace.Api.Contracts;
using TemporalTrace.Api.Data;
using TemporalTrace.Api.Models;

namespace TemporalTrace.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TaskController(AppDbContext dbContext) : ControllerBase
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

    [HttpPost]
    public async Task<ActionResult<ProjectTaskResponse>> CreateTask([FromBody] ProjectTaskUpsertRequest request)
    {
        var task = new ProjectTask
        {
            Title = request.Title,
            Description = request.Description,
            Status = request.Status,
            Priority = request.Priority
        };

        dbContext.ProjectTasks.Add(task);
        await dbContext.SaveChangesAsync();

        var response = ToResponse(task);
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

        await dbContext.SaveChangesAsync();
        return Ok(ToResponse(existing));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteTask(int id)
    {
        var existing = await dbContext.ProjectTasks.FirstOrDefaultAsync(t => t.Id == id);
        if (existing is null)
        {
            return NotFound();
        }

        dbContext.ProjectTasks.Remove(existing);
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
            Priority = task.Priority
        };
    }
}

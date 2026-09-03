using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TemporalTrace.Api.Data;
using TemporalTrace.Api.Models;

namespace TemporalTrace.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TaskController(AppDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ProjectTask>>> GetTasks()
    {
        var tasks = await dbContext.ProjectTasks
            .AsNoTracking()
            .OrderBy(t => t.Id)
            .ToListAsync();

        return Ok(tasks);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ProjectTask>> GetTask(int id)
    {
        var task = await dbContext.ProjectTasks
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id);

        return task is null ? NotFound() : Ok(task);
    }

    [HttpGet("{id:int}/at")]
    public async Task<ActionResult<ProjectTask>> GetTaskAtTime(int id, [FromQuery] DateTime targetTime)
    {
        if (targetTime == default)
        {
            return BadRequest("targetTime query parameter is required.");
        }

        var asOfUtc = targetTime.Kind switch
        {
            DateTimeKind.Utc => targetTime,
            DateTimeKind.Local => targetTime.ToUniversalTime(),
            _ => DateTime.SpecifyKind(targetTime, DateTimeKind.Utc)
        };

        var taskAtTime = await dbContext.ProjectTasks
            .TemporalAsOf(asOfUtc)
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id);

        return taskAtTime is null ? NotFound() : Ok(taskAtTime);
    }

    [HttpPost]
    public async Task<ActionResult<ProjectTask>> CreateTask([FromBody] ProjectTask request)
    {
        dbContext.ProjectTasks.Add(request);
        await dbContext.SaveChangesAsync();

        return CreatedAtAction(nameof(GetTask), new { id = request.Id }, request);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ProjectTask>> UpdateTask(int id, [FromBody] ProjectTask request)
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
        return Ok(existing);
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
}

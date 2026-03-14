using Microsoft.EntityFrameworkCore;
using TemporalTrace.Api.Models;

namespace TemporalTrace.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<ProjectTask> ProjectTasks => Set<ProjectTask>();
    public DbSet<TaskBranch> TaskBranches => Set<TaskBranch>();
    public DbSet<TaskWorkUpdate> TaskWorkUpdates => Set<TaskWorkUpdate>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProjectTask>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Title)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(e => e.Description)
                .HasMaxLength(2000);

            entity.Property(e => e.Status)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(e => e.UpdatedAt)
                .IsRequired();

            entity.ToTable("ProjectTasks", tableBuilder =>
            {
                tableBuilder.IsTemporal();
            });
        });

        modelBuilder.Entity<TaskWorkUpdate>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.TaskId).IsRequired();

            entity.Property(e => e.Note)
                .IsRequired()
                .HasMaxLength(1000);

            entity.Property(e => e.StatusAfter)
                .HasMaxLength(50);

            entity.Property(e => e.CreatedAt).IsRequired();

            entity.HasIndex(e => e.TaskId);
            entity.HasIndex(e => e.CreatedAt);

            entity.ToTable("TaskWorkUpdates");
        });

        modelBuilder.Entity<TaskBranch>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.TaskId).IsRequired();

            entity.Property(e => e.BranchName)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(e => e.CreatedFromTime).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.IsMainTimeline).IsRequired();

            entity.Property(e => e.OverrideTitle)
                .HasMaxLength(200);

            entity.Property(e => e.OverrideDescription)
                .HasMaxLength(2000);

            entity.Property(e => e.OverrideStatus)
                .HasMaxLength(50);

            entity.HasIndex(e => e.TaskId);

            entity.ToTable("TaskBranches", tableBuilder =>
            {
                tableBuilder.IsTemporal();
            });
        });
    }
}

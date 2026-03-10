using Microsoft.EntityFrameworkCore;
using TemporalTrace.Api.Models;

namespace TemporalTrace.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<ProjectTask> ProjectTasks => Set<ProjectTask>();
    public DbSet<TaskBranch> TaskBranches => Set<TaskBranch>();

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

            entity.ToTable("ProjectTasks", tableBuilder =>
            {
                tableBuilder.IsTemporal();
            });
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

            entity.HasIndex(e => e.TaskId);

            entity.ToTable("TaskBranches", tableBuilder =>
            {
                tableBuilder.IsTemporal();
            });
        });
    }
}

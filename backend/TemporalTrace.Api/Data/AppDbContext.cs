using Microsoft.EntityFrameworkCore;
using TemporalTrace.Api.Models;

namespace TemporalTrace.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<ProjectTask> ProjectTasks => Set<ProjectTask>();

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
    }
}

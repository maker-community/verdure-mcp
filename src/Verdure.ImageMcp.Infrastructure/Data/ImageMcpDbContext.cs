using Microsoft.EntityFrameworkCore;
using Verdure.ImageMcp.Domain.Entities;

namespace Verdure.ImageMcp.Infrastructure.Data;

/// <summary>
/// Database context for the Image MCP server
/// </summary>
public class ImageMcpDbContext : DbContext
{
    public ImageMcpDbContext(DbContextOptions<ImageMcpDbContext> options) : base(options)
    {
    }

    /// <summary>
    /// Image generation tasks
    /// </summary>
    public DbSet<ImageGenerationTask> ImageGenerationTasks { get; set; } = null!;

    /// <summary>
    /// API tokens for authentication
    /// </summary>
    public DbSet<ApiToken> ApiTokens { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure ImageGenerationTask entity
        modelBuilder.Entity<ImageGenerationTask>(entity =>
        {
            entity.ToTable("image_generation_tasks");
            
            entity.HasKey(e => e.Id);
            
            entity.Property(e => e.Id)
                .HasColumnName("id")
                .HasDefaultValueSql("gen_random_uuid()");
            
            entity.Property(e => e.Prompt)
                .HasColumnName("prompt")
                .IsRequired()
                .HasMaxLength(4000);
            
            entity.Property(e => e.Size)
                .HasColumnName("size")
                .HasMaxLength(20);
            
            entity.Property(e => e.Quality)
                .HasColumnName("quality")
                .HasMaxLength(20);
            
            entity.Property(e => e.Style)
                .HasColumnName("style")
                .HasMaxLength(20);
            
            entity.Property(e => e.Status)
                .HasColumnName("status")
                .IsRequired();
            
            entity.Property(e => e.ImageData)
                .HasColumnName("image_data");
            
            entity.Property(e => e.ImageUrl)
                .HasColumnName("image_url")
                .HasMaxLength(2000);
            
            entity.Property(e => e.ErrorMessage)
                .HasColumnName("error_message")
                .HasMaxLength(2000);
            
            entity.Property(e => e.Email)
                .HasColumnName("email")
                .HasMaxLength(255);
            
            entity.Property(e => e.EmailSent)
                .HasColumnName("email_sent")
                .HasDefaultValue(false);
            
            entity.Property(e => e.UserId)
                .HasColumnName("user_id")
                .HasMaxLength(255);
            
            entity.Property(e => e.HangfireJobId)
                .HasColumnName("hangfire_job_id")
                .HasMaxLength(100);
            
            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
            
            entity.Property(e => e.UpdatedAt)
                .HasColumnName("updated_at");
            
            entity.Property(e => e.CompletedAt)
                .HasColumnName("completed_at");

            // Indexes
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.CreatedAt);
        });

        // Configure ApiToken entity
        modelBuilder.Entity<ApiToken>(entity =>
        {
            entity.ToTable("api_tokens");
            
            entity.HasKey(e => e.Id);
            
            entity.Property(e => e.Id)
                .HasColumnName("id")
                .HasDefaultValueSql("gen_random_uuid()");
            
            entity.Property(e => e.TokenHash)
                .HasColumnName("token_hash")
                .IsRequired()
                .HasMaxLength(128);
            
            entity.Property(e => e.Name)
                .HasColumnName("name")
                .IsRequired()
                .HasMaxLength(100);
            
            entity.Property(e => e.IsActive)
                .HasColumnName("is_active")
                .HasDefaultValue(true);
            
            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
            
            entity.Property(e => e.ExpiresAt)
                .HasColumnName("expires_at");
            
            entity.Property(e => e.LastUsedAt)
                .HasColumnName("last_used_at");

            // Indexes
            entity.HasIndex(e => e.TokenHash).IsUnique();
            entity.HasIndex(e => e.IsActive);
        });
    }
}

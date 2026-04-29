using Microsoft.EntityFrameworkCore;
using RenPyVisualScriptMVVM.Infrastructure.StoryStorage.Entities;

namespace RenPyVisualScriptMVVM.Infrastructure.StoryStorage;

public sealed class StoryDbContext : DbContext
{
    public StoryDbContext(DbContextOptions<StoryDbContext> options) : base(options)
    {
    }

    public DbSet<StoryProjectEntity> Projects => Set<StoryProjectEntity>();
    public DbSet<StoryLabelEntity> Labels => Set<StoryLabelEntity>();
    public DbSet<StoryTextFragmentEntity> Fragments => Set<StoryTextFragmentEntity>();
    public DbSet<StoryWordEntity> Words => Set<StoryWordEntity>();
    public DbSet<StoryWordFormatTagEntity> WordFormatTags => Set<StoryWordFormatTagEntity>();
    public DbSet<StoryCharacterEntity> Characters => Set<StoryCharacterEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<StoryProjectEntity>(entity =>
        {
            entity.ToTable("StoryProjects");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).IsRequired();
            entity.Property(x => x.ProjectPath).IsRequired();
            entity.HasIndex(x => x.ProjectPath).IsUnique();
            entity.HasMany(x => x.Labels)
                .WithOne(x => x.Project)
                .HasForeignKey(x => x.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(x => x.Characters)
                .WithOne(x => x.Project)
                .HasForeignKey(x => x.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<StoryLabelEntity>(entity =>
        {
            entity.ToTable("StoryLabels");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).IsRequired();
            entity.Property(x => x.FilePath).IsRequired();
            entity.Property(x => x.RawText).IsRequired();
            entity.Property(x => x.ContentHash).IsRequired();
            entity.HasIndex(x => new { x.ProjectId, x.Name, x.FilePath }).IsUnique();
            entity.HasMany(x => x.Fragments)
                .WithOne(x => x.Label)
                .HasForeignKey(x => x.LabelId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(x => x.Words)
                .WithOne(x => x.Label)
                .HasForeignKey(x => x.LabelId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasMany(x => x.FormatTags)
                .WithOne(x => x.Label)
                .HasForeignKey(x => x.LabelId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<StoryTextFragmentEntity>(entity =>
        {
            entity.ToTable("StoryTextFragments");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Kind).IsRequired();
            entity.Property(x => x.RawText).IsRequired();
            entity.Property(x => x.PlainText).IsRequired();
            entity.HasIndex(x => new { x.LabelId, x.SortOrder });
            entity.HasIndex(x => x.SpeakerCode);
            entity.HasMany(x => x.Words)
                .WithOne(x => x.Fragment)
                .HasForeignKey(x => x.FragmentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<StoryWordEntity>(entity =>
        {
            entity.ToTable("StoryWords");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Text).IsRequired();
            entity.Property(x => x.PlainText).IsRequired();
            entity.Property(x => x.LeadingTrivia).IsRequired();
            entity.Property(x => x.TrailingTrivia).IsRequired();
            entity.HasIndex(x => x.LabelId);
            entity.HasIndex(x => new { x.FragmentId, x.SortOrder });
            entity.HasMany(x => x.FormatTags)
                .WithOne(x => x.Word)
                .HasForeignKey(x => x.WordId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<StoryWordFormatTagEntity>(entity =>
        {
            entity.ToTable("StoryWordFormatTags");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TagName).IsRequired();
            entity.Property(x => x.RawTag).IsRequired();
            entity.HasIndex(x => x.LabelId);
            entity.HasIndex(x => new { x.WordId, x.SortOrder });
        });

        modelBuilder.Entity<StoryCharacterEntity>(entity =>
        {
            entity.ToTable("StoryCharacters");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).IsRequired();
            entity.Property(x => x.Color).IsRequired();
            entity.Property(x => x.InGameName).IsRequired();
            entity.Property(x => x.FilePath).IsRequired();
            entity.HasIndex(x => new { x.ProjectId, x.SortOrder });
            entity.HasIndex(x => new { x.ProjectId, x.Name });
        });

    }
}

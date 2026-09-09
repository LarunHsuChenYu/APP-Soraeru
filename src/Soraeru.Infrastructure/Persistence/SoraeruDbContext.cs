using Microsoft.EntityFrameworkCore;
using Soraeru.Infrastructure.Persistence.Entities;

namespace Soraeru.Infrastructure.Persistence;

public sealed class SoraeruDbContext : DbContext
{
    public SoraeruDbContext(DbContextOptions<SoraeruDbContext> options)
        : base(options)
    {
    }

    public DbSet<UserEntity> Users => Set<UserEntity>();

    public DbSet<UsageDailyEntity> UsageDaily => Set<UsageDailyEntity>();

    public DbSet<WordCardEntity> WordCards => Set<WordCardEntity>();

    public DbSet<VerifiedMnemonicEntity> VerifiedMnemonics => Set<VerifiedMnemonicEntity>();

    public DbSet<WordRegenerationEntity> WordRegenerations => Set<WordRegenerationEntity>();

    public DbSet<LlmRuntimeSettingsEntity> LlmRuntimeSettings => Set<LlmRuntimeSettingsEntity>();

    public DbSet<LlmUsageEntity> LlmUsages => Set<LlmUsageEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var users = modelBuilder.Entity<UserEntity>();
        users.ToTable("Users");
        users.HasKey(x => x.Id);
        users.HasIndex(x => x.Email).IsUnique();
        users.HasIndex(x => x.GoogleSubject).IsUnique();
        users.Property(x => x.Email).HasMaxLength(320).IsRequired();
        users.Property(x => x.PasswordHash).HasMaxLength(500);
        users.Property(x => x.GoogleSubject).HasMaxLength(128);
        users.Property(x => x.DisplayName).HasMaxLength(120).IsRequired();
        users.Property(x => x.PlanTier).HasMaxLength(32).IsRequired();
        users.Property(x => x.NotationPref).HasMaxLength(64).IsRequired();

        var usage = modelBuilder.Entity<UsageDailyEntity>();
        usage.ToTable("UsageDaily");
        usage.HasKey(x => new { x.UserId, x.UsageDate });

        var cards = modelBuilder.Entity<WordCardEntity>();
        cards.ToTable("WordCards");
        cards.HasKey(x => x.Id);
        cards.HasIndex(x => x.UserId);
        cards.HasIndex(x => new { x.UserId, x.DetectedLanguage, x.NormalizedText });
        cards.Property(x => x.SourceText).HasMaxLength(200).IsRequired();
        cards.Property(x => x.NormalizedText).HasMaxLength(200).IsRequired();
        cards.Property(x => x.DetectedLanguage).HasMaxLength(32).IsRequired();
        cards.Property(x => x.MeaningZh).HasMaxLength(500).IsRequired();
        cards.Property(x => x.Pronunciation).HasMaxLength(500).IsRequired();
        cards.Property(x => x.SelectedMnemonic).HasMaxLength(500).IsRequired();

        var verified = modelBuilder.Entity<VerifiedMnemonicEntity>();
        verified.ToTable("VerifiedMnemonics");
        verified.HasKey(x => x.Id);
        verified.HasIndex(x => new { x.Language, x.NormalizedSource }).IsUnique();
        verified.HasIndex(x => new { x.Language, x.NormalizedSource, x.IsEnabled });
        verified.Property(x => x.Language).HasMaxLength(32).IsRequired();
        verified.Property(x => x.SourceText).HasMaxLength(200).IsRequired();
        verified.Property(x => x.NormalizedSource).HasMaxLength(200).IsRequired();
        verified.Property(x => x.DisplayText).HasMaxLength(500).IsRequired();
        verified.Property(x => x.NotationText).HasMaxLength(500).IsRequired();
        verified.Property(x => x.Explanation).HasMaxLength(2000).IsRequired();

        var regenerations = modelBuilder.Entity<WordRegenerationEntity>();
        regenerations.ToTable("WordRegenerations");
        regenerations.HasKey(x => new { x.UserId, x.SourceLanguage, x.NormalizedText });
        regenerations.Property(x => x.SourceLanguage).HasMaxLength(32).IsRequired();
        regenerations.Property(x => x.NormalizedText).HasMaxLength(200).IsRequired();

        var llmSettings = modelBuilder.Entity<LlmRuntimeSettingsEntity>();
        llmSettings.ToTable("LlmRuntimeSettings");
        llmSettings.HasKey(x => x.Id);
        llmSettings.Property(x => x.ApiKey).HasMaxLength(500);
        llmSettings.Property(x => x.Model).HasMaxLength(128);
        llmSettings.Property(x => x.BaseUrl).HasMaxLength(500);
        llmSettings.Property(x => x.UpdatedByEmail).HasMaxLength(320);

        var llmUsage = modelBuilder.Entity<LlmUsageEntity>();
        llmUsage.ToTable("LlmUsages");
        llmUsage.HasKey(x => x.Id);
        llmUsage.HasIndex(x => x.CreatedAt);
        llmUsage.HasIndex(x => new { x.FeatureType, x.CreatedAt });
        llmUsage.Property(x => x.FeatureType).HasMaxLength(64).IsRequired();
        llmUsage.Property(x => x.Model).HasMaxLength(128).IsRequired();
        llmUsage.Property(x => x.Provider).HasMaxLength(64).IsRequired();
        llmUsage.Property(x => x.ErrorCode).HasMaxLength(64);
        llmUsage.Property(x => x.EstimatedCostNtd).HasPrecision(18, 6);
    }
}

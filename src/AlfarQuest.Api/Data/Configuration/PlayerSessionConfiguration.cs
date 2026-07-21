using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlfarQuest.Api.Data.Configuration;

public sealed class PlayerSessionConfiguration : IEntityTypeConfiguration<PlayerSession>
{
    public void Configure(EntityTypeBuilder<PlayerSession> b)
    {
        b.HasKey(s => s.Id);

        // Every authenticated request looks a session up by this and nothing else,
        // so it is the one index that has to exist. Unique doubles as a guard
        // against a token being minted twice.
        b.Property(s => s.TokenHash).HasMaxLength(64).IsRequired();
        b.HasIndex(s => s.TokenHash).IsUnique();

        b.Property(s => s.UserAgent).HasMaxLength(256);
    }
}

public sealed class PlayerAvatarConfiguration : IEntityTypeConfiguration<PlayerAvatar>
{
    public void Configure(EntityTypeBuilder<PlayerAvatar> b)
    {
        b.HasKey(a => a.PlayerAccountId);
        // Without this the provider picks a short varbinary and the insert
        // silently truncates a perfectly valid image.
        b.Property(a => a.Content).HasColumnType("longblob").IsRequired();
        b.Property(a => a.ContentType).HasMaxLength(40).IsRequired();

        b.HasOne<PlayerAccount>()
            .WithOne()
            .HasForeignKey<PlayerAvatar>(a => a.PlayerAccountId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

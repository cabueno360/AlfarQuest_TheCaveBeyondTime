using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlfarQuest.Api.Data.Configuration;

public sealed class PlayerAccountConfiguration : IEntityTypeConfiguration<PlayerAccount>
{
    public void Configure(EntityTypeBuilder<PlayerAccount> b)
    {
        b.HasKey(a => a.Id);

        // The unique index is what actually prevents two accounts on one address.
        // The service checks first to produce a friendly message; this is the check
        // that holds when two sign-ups race.
        b.Property(a => a.Email).HasMaxLength(254).IsRequired();
        b.HasIndex(a => a.Email).IsUnique();

        b.Property(a => a.PasswordHash).HasMaxLength(256).IsRequired();
        b.Property(a => a.FullName).HasMaxLength(80).IsRequired();
        b.Property(a => a.Country).HasMaxLength(56);
        b.Property(a => a.PhoneNumber).HasMaxLength(32);
        b.Property(a => a.AvatarVersion).HasMaxLength(64);
        b.Property(a => a.CurrentCharacter).HasMaxLength(40);
        b.Property(a => a.ExternalProvider).HasMaxLength(40);
        b.Property(a => a.ExternalSubjectId).HasMaxLength(128);

        // One account, at most one external identity — the pair is what a future
        // "sign in with Google" callback will look the account up by.
        b.HasIndex(a => new { a.ExternalProvider, a.ExternalSubjectId });

        b.HasMany(a => a.Sessions)
            .WithOne()
            .HasForeignKey(s => s.PlayerAccountId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

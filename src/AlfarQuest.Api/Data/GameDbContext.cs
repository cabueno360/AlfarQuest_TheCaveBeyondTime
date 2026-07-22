using Microsoft.EntityFrameworkCore;

namespace AlfarQuest.Api.Data;

public class GameDbContext : DbContext
{
    public GameDbContext(DbContextOptions<GameDbContext> options) : base(options) { }

    public DbSet<HeroEntity> Heroes => Set<HeroEntity>();
    public DbSet<PlayerSave> Saves => Set<PlayerSave>();
    public DbSet<SaveHero> SaveHeroes => Set<SaveHero>();
    public DbSet<SaveSkill> SaveSkills => Set<SaveSkill>();
    public DbSet<SaveClaim> SaveClaims => Set<SaveClaim>();
    public DbSet<SaveTally> SaveTallies => Set<SaveTally>();
    public DbSet<SaveContainer> SaveContainers => Set<SaveContainer>();
    public DbSet<SaveContainerItem> SaveContainerItems => Set<SaveContainerItem>();
    public DbSet<SaveEquipment> SaveEquipment => Set<SaveEquipment>();
    public DbSet<SaveHeroTally> SaveHeroTallies => Set<SaveHeroTally>();

    public DbSet<PlayerAccount> Accounts => Set<PlayerAccount>();
    public DbSet<PlayerSession> Sessions => Set<PlayerSession>();
    public DbSet<PlayerAvatar> Avatars => Set<PlayerAvatar>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        // Account, session and avatar shapes live beside their entities so this
        // method does not grow every time the profile does.
        b.ApplyConfigurationsFromAssembly(typeof(GameDbContext).Assembly);

        b.Entity<HeroEntity>().HasKey(h => h.Key);
        b.Entity<HeroEntity>().Property(h => h.Key).HasMaxLength(40);

        b.Entity<SaveHero>()
            .HasMany(h => h.Skills)
            .WithOne()
            .HasForeignKey(s => s.SaveHeroId)
            .OnDelete(DeleteBehavior.Cascade);
        b.Entity<SaveSkill>().Property(s => s.SkillId).HasMaxLength(40);

        b.Entity<SaveHero>()
            .HasMany(h => h.Equipped)
            .WithOne()
            .HasForeignKey(e => e.SaveHeroId)
            .OnDelete(DeleteBehavior.Cascade);
        b.Entity<SaveEquipment>().Property(e => e.ItemId).HasMaxLength(40);

        b.Entity<SaveHero>()
            .HasMany(h => h.Belongings)
            .WithOne()
            .HasForeignKey(t => t.SaveHeroId)
            .OnDelete(DeleteBehavior.Cascade);
        b.Entity<SaveHeroTally>().Property(t => t.Kind).HasMaxLength(20);
        b.Entity<SaveHeroTally>().Property(t => t.TallyKey).HasMaxLength(60);

        b.Entity<PlayerSave>()
            .HasMany(s => s.Belongings)
            .WithOne()
            .HasForeignKey(t => t.PlayerSaveId)
            .OnDelete(DeleteBehavior.Cascade);
        b.Entity<SaveTally>().Property(t => t.Kind).HasMaxLength(16);

        b.Entity<PlayerSave>()
            .HasMany(s => s.Containers)
            .WithOne()
            .HasForeignKey(c => c.PlayerSaveId)
            .OnDelete(DeleteBehavior.Cascade);
        b.Entity<SaveContainer>().Property(c => c.ContainerKey).HasMaxLength(64);
        b.Entity<SaveContainer>()
            .HasMany(c => c.Remaining)
            .WithOne()
            .HasForeignKey(i => i.SaveContainerId)
            .OnDelete(DeleteBehavior.Cascade);
        b.Entity<SaveContainerItem>().Property(i => i.Kind).HasMaxLength(16);
        b.Entity<SaveContainerItem>().Property(i => i.TallyKey).HasMaxLength(40);
        b.Entity<SaveTally>().Property(t => t.TallyKey).HasMaxLength(40);

        b.Entity<PlayerSave>()
            .HasMany(s => s.Claims)
            .WithOne()
            .HasForeignKey(c => c.PlayerSaveId)
            .OnDelete(DeleteBehavior.Cascade);
        b.Entity<SaveClaim>().Property(c => c.RewardKey).HasMaxLength(64);

        b.Entity<PlayerSave>()
            .HasMany(s => s.Party)
            .WithOne()
            .HasForeignKey(sh => sh.PlayerSaveId)
            .OnDelete(DeleteBehavior.Cascade);

        // Seed the three canonical delvers from "Story for Music".
        b.Entity<HeroEntity>().HasData(
            new HeroEntity { Key = "mage", Name = "The Fallen Mage", Title = "Bearer of the Caged Fire", HeroClass = "Mage", BaseHp = 90, UnlockedByDefault = true, Description = "An exile of Kae Ychel with a greater demon bound inside his heart. Hurls discs of light — and, when pressed, unleashes the hellfire he can barely contain." },
            new HeroEntity { Key = "cleric", Name = "The Grieving Cleric", Title = "Whose Faith Fractured", HeroClass = "Cleric", BaseHp = 140, UnlockedByDefault = true, Description = "A priest who traded his own descent into madness for a phial of panacea. Wades in with blessed plate and gilded mace, and can loose a nova of holy light." },
            new HeroEntity { Key = "thief", Name = "The Hollow Thief", Title = "Whose Crew the Crystal Took", HeroClass = "Thief", BaseHp = 100, UnlockedByDefault = true, Description = "A Seoshe gambler with a shard of the stolen crystal fused into his arm. Fires a crossbow from the dark and dashes through danger with reckless luck." }
        );
    }
}

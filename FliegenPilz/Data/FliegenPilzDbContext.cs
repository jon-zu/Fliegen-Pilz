using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FliegenPilz.Data;

public class FliegenPilzDbContext(DbContextOptions<FliegenPilzDbContext> options) : DbContext(options)
{
    public DbSet<AccountEntity> Accounts => Set<AccountEntity>();
    public DbSet<CharacterEntity> Characters => Set<CharacterEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var accountIdConverter = new ValueConverter<AccountId, int>(id => id.Value, value => new AccountId(value));
        var characterIdConverter = new ValueConverter<CharacterId, int>(id => id.Value, value => new CharacterId(value));

        var accountIdComparer = new ValueComparer<AccountId>(
            (left, right) => left.Value == right.Value,
            id => id.Value.GetHashCode(),
            id => new AccountId(id.Value));

        var characterIdComparer = new ValueComparer<CharacterId>(
            (left, right) => left.Value == right.Value,
            id => id.Value.GetHashCode(),
            id => new CharacterId(id.Value));

        modelBuilder.Entity<AccountEntity>(builder =>
        {
            builder.Property(a => a.Id)
                .ValueGeneratedOnAdd()
                .HasConversion(accountIdConverter, accountIdComparer);

            builder.HasIndex(a => a.Username)
                .IsUnique();
        });

        modelBuilder.Entity<CharacterEntity>(builder =>
        {
            builder.Property(c => c.Id)
                .ValueGeneratedOnAdd()
                .HasConversion(characterIdConverter, characterIdComparer);

            builder.Property(c => c.AccountId)
                .HasConversion(accountIdConverter, accountIdComparer);

            builder.HasOne(c => c.Account)
                .WithMany(a => a.Characters)
                .HasForeignKey(c => c.AccountId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}

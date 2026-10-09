using LkFinances.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace LkFinances.Core.Data;

public class FinanceDb(DbContextOptions<FinanceDb> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Ledger> Ledgers => Set<Ledger>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<BalanceAssertion> BalanceAssertions => Set<BalanceAssertion>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configuration)
    {
        configuration.Properties<decimal>().HavePrecision(18, 2);
        configuration.Properties<Direction>().HaveConversion<string>().HaveMaxLength(10);
        configuration.Properties<TransactionStatus>().HaveConversion<string>().HaveMaxLength(10);
        configuration.Properties<CategoryKind>().HaveConversion<string>().HaveMaxLength(20);
        configuration.Properties<Bucket>().HaveConversion<string>().HaveMaxLength(20);
    }

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<User>(e =>
        {
            e.Property(u => u.Email).HasMaxLength(320);
            e.HasIndex(u => u.Email).IsUnique();
        });

        model.Entity<Ledger>(e =>
        {
            e.Property(l => l.Name).HasMaxLength(100);
            e.Property(l => l.Currency).HasMaxLength(3).IsFixedLength();
            e.HasOne<User>().WithMany().HasForeignKey(l => l.OwnerUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(l => l.OwnerUserId);
        });

        model.Entity<Category>(e =>
        {
            e.Property(c => c.Name).HasMaxLength(Category.NameMaxLength);
            e.HasOne<Ledger>().WithMany().HasForeignKey(c => c.LedgerId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(c => new { c.LedgerId, c.Name }).IsUnique();
        });

        model.Entity<Transaction>(e =>
        {
            e.Property(t => t.Description).HasMaxLength(Transaction.DescriptionMaxLength);
            e.Property(t => t.Notes).HasMaxLength(Transaction.NotesMaxLength);
            e.Property(t => t.ImportHash).HasMaxLength(64);
            e.HasOne<Ledger>().WithMany().HasForeignKey(t => t.LedgerId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(t => t.Category).WithMany().HasForeignKey(t => t.CategoryId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(t => new { t.LedgerId, t.OccurredOn });
            e.HasIndex(t => new { t.LedgerId, t.ImportHash }).IsUnique().HasFilter("\"ImportHash\" IS NOT NULL");
        });

        model.Entity<BalanceAssertion>(e =>
        {
            e.HasKey(b => new { b.LedgerId, b.Bucket });
            e.HasOne<Ledger>().WithMany().HasForeignKey(b => b.LedgerId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}

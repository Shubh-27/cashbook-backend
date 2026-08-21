using Microsoft.EntityFrameworkCore;
using backend.model.DbModels.Views;

namespace backend.model.DbModels;

public partial class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Account> Accounts { get; set; }
    public virtual DbSet<Description> Descriptions { get; set; }
    public virtual DbSet<Transaction> Transactions { get; set; }
    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<VwTransactionsList> VwTransactionsList { get; set; }
    public virtual DbSet<VwAccountsList> VwAccountsList { get; set; }
    public virtual DbSet<VwDescriptionsList> VwDescriptionsList { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Account>(entity =>
        {
            entity.ToTable("Accounts");

            entity.HasKey(e => e.AccountID);

            entity.HasIndex(e => e.AccountID, "IX_Accounts_AccountID").IsUnique();

            entity.HasIndex(e => e.AccountSID, "IX_Accounts_AccountSID").IsUnique();

            entity.Property(e => e.CreatedDateTime).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.LastModifiedDateTime).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.Status).HasDefaultValue(1);
        });

        modelBuilder.Entity<Description>(entity =>
        {
            entity.ToTable("Descriptions");

            entity.HasKey(e => e.DescriptionID);

            entity.HasIndex(e => e.DescriptionID, "IX_Descriptions_DescriptionID").IsUnique();

            entity.HasIndex(e => e.DescriptionSID, "IX_Descriptions_DescriptionSID").IsUnique();

            entity.HasIndex(e => e.DescriptionName, "IX_Descriptions_DescriptionName").IsUnique();

            entity.Property(e => e.CreatedDateTime).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.LastModifiedDateTime).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.Status).HasDefaultValue(1);
        });

        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.ToTable("Transactions");

            entity.HasKey(e => e.TransactionID);

            entity.HasIndex(e => e.TransactionID, "IX_Transactions_TransactionID").IsUnique();

            entity.HasIndex(e => e.TransactionSID, "IX_Transactions_TransactionSID").IsUnique();

            entity.HasIndex(e => e.AccountID, "IX_Transactions_AccountID");

            entity.HasIndex(e => e.DescriptionID, "IX_Transactions_DescriptionID");

            entity.HasIndex(e => e.TransactionDate, "IX_Transactions_TransactionDate");

            entity.HasIndex(e => e.Status, "IX_Transactions_Status");

            entity.HasIndex(e => new { e.Status, e.TransactionDate }, "IX_Transactions_Status_TransactionDate");

            entity.HasIndex(e => new { e.AccountID, e.Status }, "IX_Transactions_AccountID_Status");

            entity.HasIndex(e => new { e.DescriptionID, e.Status }, "IX_Transactions_DescriptionID_Status");

            entity.Property(e => e.Balance).HasDefaultValue(0.0);
            entity.Property(e => e.CreatedDateTime).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.Credit).HasDefaultValue(0.0);
            entity.Property(e => e.Debit).HasDefaultValue(0.0);
            entity.Property(e => e.LastModifiedDateTime).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.Status).HasDefaultValue(1);

            entity.HasOne(d => d.Account).WithMany(p => p.Transactions).HasForeignKey(d => d.AccountID);

            entity.HasOne(d => d.Description).WithMany(p => p.Transactions).HasForeignKey(d => d.DescriptionID);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");

            entity.HasKey(e => e.UserID);

            entity.HasIndex(e => e.UserID, "IX_Users_UserID").IsUnique();

            entity.HasIndex(e => e.UserSID, "IX_Users_UserSID").IsUnique();

            entity.Property(e => e.CreatedDateTime).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.LastModifiedDateTime).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.Status).HasDefaultValue(1);
        });

        modelBuilder.Entity<VwTransactionsList>(entity =>
        {
            entity.HasNoKey();
            entity.ToView("vw_transactions_list");
        });

        modelBuilder.Entity<VwAccountsList>(entity =>
        {
            entity.HasNoKey();
            entity.ToView("vw_accounts_list");
        });

        modelBuilder.Entity<VwDescriptionsList>(entity =>
        {
            entity.HasNoKey();
            entity.ToView("vw_descriptions_list");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}

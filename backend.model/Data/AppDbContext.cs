using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using backend.model.Models;

namespace backend.model.Data;

public partial class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Accounts> Accounts { get; set; }

    public virtual DbSet<Descriptions> Descriptions { get; set; }

    public virtual DbSet<Transactions> Transactions { get; set; }

    public virtual DbSet<Users> Users { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Accounts>(entity =>
        {
            entity.HasKey(e => e.AccountID);

            entity.HasIndex(e => e.AccountID, "IX_Accounts_AccountID").IsUnique();

            entity.HasIndex(e => e.AccountSID, "IX_Accounts_AccountSID").IsUnique();

            entity.Property(e => e.CreatedDateTime).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.LastModifiedDateTime).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.Status).HasDefaultValue(1);
        });

        modelBuilder.Entity<Descriptions>(entity =>
        {
            entity.HasKey(e => e.DescriptionID);

            entity.HasIndex(e => e.DescriptionID, "IX_Descriptions_DescriptionID").IsUnique();

            entity.HasIndex(e => e.DescriptionSID, "IX_Descriptions_DescriptionSID").IsUnique();

            entity.Property(e => e.CreatedDateTime).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.LastModifiedDateTime).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.Status).HasDefaultValue(1);
        });

        modelBuilder.Entity<Transactions>(entity =>
        {
            entity.HasKey(e => e.TransactionID);

            entity.HasIndex(e => e.TransactionID, "IX_Transactions_TransactionID").IsUnique();

            entity.HasIndex(e => e.TransactionSID, "IX_Transactions_TransactionSID").IsUnique();

            entity.Property(e => e.Balance).HasDefaultValue(0.0);
            entity.Property(e => e.CreatedDateTime).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.Credit).HasDefaultValue(0.0);
            entity.Property(e => e.Debit).HasDefaultValue(0.0);
            entity.Property(e => e.LastModifiedDateTime).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.Status).HasDefaultValue(1);

            entity.HasOne(d => d.Account).WithMany(p => p.Transactions).HasForeignKey(d => d.AccountID);

            entity.HasOne(d => d.Description).WithMany(p => p.Transactions).HasForeignKey(d => d.DescriptionID);
        });

        modelBuilder.Entity<Users>(entity =>
        {
            entity.HasKey(e => e.UserID);

            entity.HasIndex(e => e.UserID, "IX_Users_UserID").IsUnique();

            entity.HasIndex(e => e.UserSID, "IX_Users_UserSID").IsUnique();

            entity.Property(e => e.CreatedDateTime).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.LastModifiedDateTime).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.Status).HasDefaultValue(1);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}

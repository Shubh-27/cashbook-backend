using System.ComponentModel.DataAnnotations.Schema;

namespace backend.model.TenantDB;

[Table("Accounts")]
public partial class Account
{
    public int AccountID { get; set; }

    public string AccountSID { get; set; } = null!;

    public string? AccountName { get; set; }

    public long? AccountNumber { get; set; }

    public string? BankName { get; set; }

    public string? CreatedDateTime { get; set; }

    public int? CreatedByUserID { get; set; }

    public string? LastModifiedDateTime { get; set; }

    public int? LastModifiedByUserID { get; set; }

    public int Status { get; set; }

    public virtual ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}

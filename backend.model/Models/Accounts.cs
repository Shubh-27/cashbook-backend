using System;
using System.Collections.Generic;

namespace backend.model.Models;

public partial class Accounts
{
    public int AccountID { get; set; }

    public string AccountSID { get; set; } = null!;

    public string? AccountName { get; set; }

    public int? AccountNumber { get; set; }

    public string? BankName { get; set; }

    public string? CreatedDateTime { get; set; }

    public int? CreatedByUserID { get; set; }

    public string? LastModifiedDateTime { get; set; }

    public int? LastModifiedByUserID { get; set; }

    public int Status { get; set; }

    public virtual ICollection<Transactions> Transactions { get; set; } = new List<Transactions>();
}

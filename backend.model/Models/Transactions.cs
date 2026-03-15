using System;
using System.Collections.Generic;

namespace backend.model.Models;

public partial class Transactions
{
    public int TransactionID { get; set; }

    public string TransactionSID { get; set; } = null!;

    public string TransactionDate { get; set; } = null!;

    public int? DescriptionID { get; set; }

    public double? Debit { get; set; }

    public double? Credit { get; set; }

    public double? Balance { get; set; }

    public string? Notes { get; set; }

    public int? AccountID { get; set; }

    public string? CreatedDateTime { get; set; }

    public int? CreatedByUserID { get; set; }

    public string? LastModifiedDateTime { get; set; }

    public int? LastModifiedByUserID { get; set; }

    public int Status { get; set; }

    public virtual Accounts? Account { get; set; }

    public virtual Descriptions? Description { get; set; }
}

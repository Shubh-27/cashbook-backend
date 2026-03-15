using System;
using System.Collections.Generic;

namespace backend.model.Models;

public partial class Descriptions
{
    public int DescriptionID { get; set; }

    public string DescriptionSID { get; set; } = null!;

    public string DescriptionName { get; set; } = null!;

    public string? CreatedDateTime { get; set; }

    public int? CreatedByUserID { get; set; }

    public string? LastModifiedDateTime { get; set; }

    public int? LastModifiedByUserID { get; set; }

    public int Status { get; set; }

    public virtual ICollection<Transactions> Transactions { get; set; } = new List<Transactions>();
}

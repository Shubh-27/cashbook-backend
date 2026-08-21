using System.ComponentModel.DataAnnotations.Schema;

namespace backend.model.DbModels;

[Table("Descriptions")]
public partial class Description
{
    public int DescriptionID { get; set; }

    public string DescriptionSID { get; set; } = null!;

    public string DescriptionName { get; set; } = null!;

    public string? CreatedDateTime { get; set; }

    public int? CreatedByUserID { get; set; }

    public string? LastModifiedDateTime { get; set; }

    public int? LastModifiedByUserID { get; set; }

    public int Status { get; set; }

    public virtual ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}

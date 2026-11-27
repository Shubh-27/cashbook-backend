using System.ComponentModel.DataAnnotations.Schema;

namespace backend.model.TenantDB;

[Table("ImportProfiles")]
public class ImportProfile
{
    public int ImportProfileID { get; set; }
    public string ImportProfileSID { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string FileType { get; set; } = null!;
    public string? WorksheetName { get; set; }
    public int HeaderRow { get; set; }
    public string HeaderSignature { get; set; } = null!;
    public string ColumnMappingJson { get; set; } = null!;
    public string? DateFormat { get; set; }
    public string? SampleJson { get; set; }
    public int? CurrentVersionID { get; set; }
    public string? CreatedDateTime { get; set; }
    public string? LastModifiedDateTime { get; set; }
    public int Status { get; set; }

    public virtual ICollection<ImportBatch> ImportBatches { get; set; } = new List<ImportBatch>();
    public virtual ICollection<ImportProfileVersion> Versions { get; set; } = new List<ImportProfileVersion>();
}

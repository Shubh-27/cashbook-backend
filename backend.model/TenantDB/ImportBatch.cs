using System.ComponentModel.DataAnnotations.Schema;

namespace backend.model.TenantDB;

[Table("ImportBatches")]
public class ImportBatch
{
    public int ImportBatchID { get; set; }
    public string ImportBatchSID { get; set; } = null!;
    public int? ImportProfileID { get; set; }
    public int? ProfileVersionID { get; set; }
    public string? FileHash { get; set; }
    public int AccountID { get; set; }
    public string SourceFileName { get; set; } = null!;
    public string FileType { get; set; } = null!;
    public string? StatementMetadataJson { get; set; }
    public int TotalRows { get; set; }
    public int ImportedRows { get; set; }
    public int SkippedDuplicateRows { get; set; }
    public int InvalidRows { get; set; }
    public string? CreatedDateTime { get; set; }
    public string? UndoneDateTime { get; set; }
    public int Status { get; set; }

    public virtual Account Account { get; set; } = null!;
    public virtual ImportProfile? ImportProfile { get; set; }
}

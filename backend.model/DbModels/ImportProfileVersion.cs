using System.ComponentModel.DataAnnotations.Schema;

namespace backend.model.DbModels;

[Table("ImportProfileVersions")]
public class ImportProfileVersion
{
    public int ImportProfileVersionID { get; set; }
    public int ProfileID { get; set; }
    public int VersionNumber { get; set; }
    public string FileType { get; set; } = null!;
    public string? WorksheetName { get; set; }
    public string HeaderSignature { get; set; } = null!;
    public int HeaderRowIndexHint { get; set; }
    public int HeaderCellCount { get; set; }
    public int? DateColumnIndex { get; set; }
    public string? DateFormat { get; set; }
    public int? DescriptionCol { get; set; }
    public int? DebitCol { get; set; }
    public int? CreditCol { get; set; }
    public int? AmountCol { get; set; }
    public int? BalanceCol { get; set; }
    public int? NotesCol { get; set; }
    public string? FooterMarker { get; set; }
    public string? FooterSignature { get; set; }
    public string? SecondaryTableMarker { get; set; }
    public string SampleJson { get; set; } = null!;
    public string CreatedAt { get; set; } = null!;
    public string? CreatedBy { get; set; }

    public virtual ImportProfile Profile { get; set; } = null!;
}

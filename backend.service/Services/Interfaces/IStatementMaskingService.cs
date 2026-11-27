namespace backend.service.Services.Interfaces;

public interface IStatementMaskingService
{
    string MaskCell(string value);
    List<string> MaskRow(IEnumerable<string> values);
}

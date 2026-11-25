using ClosedXML.Excel;

namespace backend.tests.Fixtures;

public static class StatementFixtureGenerator
{
    public static string DirectoryPath => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Fixtures", "Statements"));

    public static void Ensure()
    {
        Directory.CreateDirectory(DirectoryPath);
        File.WriteAllText(Path.Combine(DirectoryPath, "synthetic-statement.csv"), "\nSynthetic Bank\nDate,Description,Debit,Credit\n02-04-2024,ABCDE1234F payment,₹350,\n11/12/2024,help@example.test,,1000\nCharges section\nDate,Charge,Amount\n01-01-2024,Service fee,10\n");
        using var workbook = new XLWorkbook();
        var overview = workbook.AddWorksheet("Overview"); overview.Cell("A1").Value = "Synthetic account metadata"; overview.Range("A1:C1").Merge();
        var sheet = workbook.AddWorksheet("Transactions"); sheet.Cell("A1").Value = "Synthetic Bank Statement"; sheet.Range("A1:D1").Merge(); sheet.Cell("A3").Value = "Customer: SYNTHETIC123456";
        sheet.Cell("A5").Value = "Date"; sheet.Cell("B5").Value = "Description"; sheet.Cell("C5").Value = "Debit"; sheet.Cell("D5").Value = "Credit";
        sheet.Cell("A6").Value = new DateTime(2024, 4, 2); sheet.Cell("A6").Style.DateFormat.Format = "dd-MM-yyyy"; sheet.Cell("B6").Value = "ABCDE1234F phone 9876543210"; sheet.Cell("C6").Value = 350;
        sheet.Cell("A7").Value = "11/12/2024"; sheet.Cell("B7").Value = "Email help@example.test"; sheet.Cell("D7").Value = 1000;
        sheet.Cell("A8").Value = "Charges section for synthetic account"; sheet.Cell("A9").Value = "Date"; sheet.Cell("B9").Value = "Charge"; sheet.Cell("C9").Value = "Amount"; sheet.Cell("A10").Value = "01-01-2024"; sheet.Cell("B10").Value = "Service fee"; sheet.Cell("C10").Value = 10;
        workbook.SaveAs(Path.Combine(DirectoryPath, "synthetic-statement.xlsx"));
    }
}

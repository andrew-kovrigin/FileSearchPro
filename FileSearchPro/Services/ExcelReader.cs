using ClosedXML.Excel;

namespace FileSearchPro.Services;

public static class ExcelReader
{
    public static string ExtractText(string filePath)
    {
        try
        {
            using var workbook = new XLWorkbook(filePath);
            var texts = new List<string>();

            foreach (var worksheet in workbook.Worksheets)
            {
                var range = worksheet.RangeUsed();
                if (range == null) continue;

                foreach (var row in range.Rows())
                {
                    foreach (var cell in row.Cells())
                    {
                        var value = cell.GetString();
                        if (!string.IsNullOrWhiteSpace(value))
                            texts.Add(value);
                    }
                }
            }

            return string.Join(" ", texts);
        }
        catch
        {
            return string.Empty;
        }
    }
}

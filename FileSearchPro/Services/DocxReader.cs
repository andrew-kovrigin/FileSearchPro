using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace FileSearchPro.Services;

public static class DocxReader
{
    public static string ExtractText(string filePath)
    {
        try
        {
            using var doc = WordprocessingDocument.Open(filePath, false);
            var body = doc.MainDocumentPart?.Document?.Body;
            if (body == null) return string.Empty;

            return string.Join(" ", body.Descendants<Text>().Select(t => t.Text));
        }
        catch
        {
            return string.Empty;
        }
    }
}

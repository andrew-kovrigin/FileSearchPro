namespace FileSearchPro.Models;

public class ExclusionRule
{
    public string Pattern { get; set; } = string.Empty;
    public string Type { get; set; } = "folder"; // "folder" or "file"
    public bool IsEnabled { get; set; } = true;
}

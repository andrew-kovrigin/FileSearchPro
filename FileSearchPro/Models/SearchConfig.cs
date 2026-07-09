namespace FileSearchPro.Models;

public class SearchConfig
{
    public string LastIpRange { get; set; } = string.Empty;
    public List<string> SelectedShares { get; set; } = new() { "C$" };
    public string FilePattern { get; set; } = "*.*";
    public long? MinSize { get; set; }
    public long? MaxSize { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public bool UseCurrentUser { get; set; } = true;
    public string Username { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public bool SearchContent { get; set; }
    public string ContentSearchText { get; set; } = string.Empty;
    public string ContentExtensions { get; set; } = ".txt,.log,.csv,.xml,.json,.cs,.py,.js,.md,.cfg,.ini";
    public string ExcludeExtensions { get; set; } = ".exe,.dll,.bin,.zip,.rar,.7z,.png,.jpg,.gif";
    public bool IncludeNoExt { get; set; } = true;
    public string Language { get; set; } = "ru";
    public int PingTimeoutMs { get; set; } = 300;
    public int ShareTimeoutMs { get; set; } = 3000;
    public int FileIOTimeoutMs { get; set; } = 5000;
}

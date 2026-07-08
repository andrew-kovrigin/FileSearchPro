namespace FileSearchPro.Models;

public class NetworkTarget
{
    public string IpAddress { get; set; } = string.Empty;
    public bool IsOnline { get; set; }
    public List<string> AvailableShares { get; set; } = new();
}

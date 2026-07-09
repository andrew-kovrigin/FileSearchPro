namespace FileSearchPro.Models;

public enum ScanLogEntryStatus
{
    Info,
    Online,
    Offline,
    Unreachable,
    Error,
    Scanning,
    Complete
}

public class ScanLogEntry
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string IpAddress { get; set; } = string.Empty;
    public ScanLogEntryStatus Status { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<string> SharesFound { get; set; } = new();
}

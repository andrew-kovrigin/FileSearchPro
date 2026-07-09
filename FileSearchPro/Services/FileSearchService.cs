using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using FileSearchPro.Models;

namespace FileSearchPro.Services;

public class FileSearchService
{
    private readonly List<ExclusionRule> _exclusions;
    private readonly NetworkCredential _credentials;
    private readonly CancellationToken _ct;
    private readonly ExclusionService _exclusionService = new();
    private int _scannedCount;
    private int _ioTimeoutMs = 5000;

    public FileSearchService(List<ExclusionRule> exclusions, NetworkCredential credentials, CancellationToken ct, int ioTimeoutMs = 5000)
    {
        _exclusions = exclusions;
        _credentials = credentials;
        _ct = ct;
        _ioTimeoutMs = ioTimeoutMs;
    }

    public void Search(
        List<NetworkTarget> targets,
        List<string> shares,
        string filePattern,
        bool searchContent,
        string contentText,
        List<string> contentExtensions,
        List<string> excludeExtensions,
        bool includeNoExt,
        Action<SearchResult> onResult,
        Action<string> onCurrentFile,
        Action<int> onScanned,
        Action<ScanLogEntry>? onLog = null)
    {
        var searchWords = searchContent && !string.IsNullOrEmpty(contentText)
            ? contentText.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : Array.Empty<string>();

        foreach (var target in targets)
        {
            _ct.ThrowIfCancellationRequested();
            if (!target.IsOnline) continue;

            onLog?.Invoke(new ScanLogEntry
            {
                IpAddress = target.IpAddress,
                Status = ScanLogEntryStatus.Scanning,
                Message = $"Scanning files on {target.IpAddress}..."
            });

            var validShares = shares.Where(s => target.AvailableShares.Contains(s)).ToList();

            foreach (var share in validShares)
            {
                _ct.ThrowIfCancellationRequested();
                var uncPath = $"\\\\{target.IpAddress}\\{share}";
                SearchIterative(uncPath, target.IpAddress, share, filePattern,
                    searchContent, searchWords, contentExtensions, excludeExtensions, includeNoExt,
                    onResult, onCurrentFile, onScanned);
            }
        }
    }

    private void SearchIterative(
        string rootDir, string ip, string share, string filePattern,
        bool searchContent, string[] searchWords,
        List<string> contentExtensions, List<string> excludeExtensions, bool includeNoExt,
        Action<SearchResult> onResult, Action<string> onCurrentFile, Action<int> onScanned)
    {
        var dirQueue = new Queue<string>();
        dirQueue.Enqueue(rootDir);

        while (dirQueue.Count > 0)
        {
            _ct.ThrowIfCancellationRequested();
            var directory = dirQueue.Dequeue();

            IEnumerable<string> files;
            try
            {
                var pattern = searchContent ? "*" : filePattern;
                var task = Task.Run(() => Directory.EnumerateFiles(directory, pattern, new EnumerationOptions
                {
                    RecurseSubdirectories = false,
                    IgnoreInaccessible = true,
                    AttributesToSkip = FileAttributes.System
                }).ToList());
                if (!task.Wait(5000)) continue;
                files = task.Result;
            }
            catch { continue; }

            foreach (var file in files)
            {
                _ct.ThrowIfCancellationRequested();
                Interlocked.Increment(ref _scannedCount);
                if (_scannedCount % 10 == 0) onScanned(_scannedCount);

                try
                {
                    if (_exclusionService.IsExcluded(file, _exclusions)) continue;

                    onCurrentFile(file);

                    var ext = Path.GetExtension(file).ToLowerInvariant();
                    var hasNoExt = string.IsNullOrEmpty(ext);

                    if (searchContent)
                    {
                        if (hasNoExt && !includeNoExt) continue;
                        if (!hasNoExt && contentExtensions.Count > 0 && !contentExtensions.Contains(ext)) continue;
                        if (searchWords.Length > 0 && !FileContainsAny(file, searchWords)) continue;
                    }

                    if (!hasNoExt && excludeExtensions.Contains(ext)) continue;

                    var info = new FileInfo(file);

                    onResult(new SearchResult
                    {
                        IpAddress = ip,
                        Share = share,
                        FullPath = file,
                        FileName = info.Name,
                        Size = info.Length,
                        LastModified = info.LastWriteTime
                    });
                }
                catch { }
            }

            IEnumerable<string> subdirs;
            try
            {
                var task = Task.Run(() => Directory.EnumerateDirectories(directory, "*", new EnumerationOptions
                {
                    IgnoreInaccessible = true,
                    AttributesToSkip = FileAttributes.System | FileAttributes.Hidden
                }).ToList());
                if (!task.Wait(5000)) continue;
                subdirs = task.Result;
            }
            catch { continue; }

            foreach (var subdir in subdirs)
            {
                _ct.ThrowIfCancellationRequested();
                try
                {
                    if (!_exclusionService.IsExcludedDirectory(subdir, _exclusions))
                        dirQueue.Enqueue(subdir);
                }
                catch { }
            }
        }
    }

    private bool FileContainsAny(string filePath, string[] words)
    {
        try
        {
            var task = Task.Run(() =>
            {
                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(stream);
                var buffer = new char[4096];
                int read;
                while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
                {
                    var chunk = new string(buffer, 0, read);
                    foreach (var word in words)
                    {
                        if (chunk.Contains(word, StringComparison.OrdinalIgnoreCase))
                            return true;
                    }
                }
                return false;
            });
            return task.Wait(5000) && task.Result;
        }
        catch { return false; }
    }
}

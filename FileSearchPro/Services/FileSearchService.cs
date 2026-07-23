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
        Action<ScanLogEntry>? onLog = null,
        bool searchAllShares = false)
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
                Message = string.Format(LanguageManager.GetString("LogScanningFiles"), target.IpAddress)
            });

            var validShares = searchAllShares
                ? target.AvailableShares.ToList()
                : shares.Where(s => target.AvailableShares.Contains(s)).ToList();

            onLog?.Invoke(new ScanLogEntry
            {
                IpAddress = target.IpAddress,
                Status = ScanLogEntryStatus.Scanning,
                Message = string.Format(LanguageManager.GetString("LogAvailableShares"), string.Join(", ", target.AvailableShares), string.Join(", ", validShares))
            });

            if (validShares.Count == 0)
            {
                onLog?.Invoke(new ScanLogEntry
                {
                    IpAddress = target.IpAddress,
                    Status = ScanLogEntryStatus.Scanning,
                    Message = string.Format(LanguageManager.GetString("LogNoShares"), target.IpAddress)
                });
                continue;
            }

            foreach (var share in validShares)
            {
                _ct.ThrowIfCancellationRequested();
                var uncPath = $"\\\\{target.IpAddress}\\{share}";
                ImpersonationHelper.RunAs(_credentials, () =>
                    SearchIterative(uncPath, target.IpAddress, share, filePattern,
                        searchContent, searchWords, contentExtensions, excludeExtensions, includeNoExt,
                        onResult, onCurrentFile, onScanned));
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
                var task = Task.Run(() =>
                {
                    try
                    {
                        return Directory.EnumerateFiles(directory, pattern, new EnumerationOptions
                        {
                            RecurseSubdirectories = false,
                            IgnoreInaccessible = true,
                            AttributesToSkip = FileAttributes.System
                        }).ToList();
                    }
                    catch (IOException) { return new List<string>(); }
                    catch (UnauthorizedAccessException) { return new List<string>(); }
                });
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

                    FileInfo info;
                    try
                    {
                        info = new FileInfo(file);
                        if (!info.Exists) continue;
                    }
                    catch (IOException) { continue; }
                    catch (UnauthorizedAccessException) { continue; }

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
                catch (IOException) { continue; }
                catch (UnauthorizedAccessException) { continue; }
                catch { }
            }

            IEnumerable<string> subdirs;
            try
            {
                var task = Task.Run(() =>
                {
                    try
                    {
                        return Directory.EnumerateDirectories(directory, "*", new EnumerationOptions
                        {
                            IgnoreInaccessible = true,
                            AttributesToSkip = FileAttributes.System | FileAttributes.Hidden
                        }).ToList();
                    }
                    catch (IOException) { return new List<string>(); }
                    catch (UnauthorizedAccessException) { return new List<string>(); }
                });
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
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            var task = Task.Run(() =>
            {
                try
                {
                    string content;
                    if (ext == ".docx")
                    {
                        content = DocxReader.ExtractText(filePath);
                    }
                    else if (ext == ".xlsx")
                    {
                        content = ExcelReader.ExtractText(filePath);
                    }
                    else
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
                    }

                    foreach (var word in words)
                    {
                        if (content.Contains(word, StringComparison.OrdinalIgnoreCase))
                            return true;
                    }
                    return false;
                }
                catch (IOException) { return false; }
                catch (UnauthorizedAccessException) { return false; }
                catch { return false; }
            });
            return task.Wait(5000) && task.Result;
        }
        catch { return false; }
    }
}

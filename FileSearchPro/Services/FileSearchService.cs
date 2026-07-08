using System.IO;
using System.Net;
using System.Threading;
using FileSearchPro.Models;

namespace FileSearchPro.Services;

public class FileSearchService
{
    private readonly List<ExclusionRule> _exclusions;
    private readonly NetworkCredential _credentials;
    private readonly CancellationToken _ct;
    private readonly ExclusionService _exclusionService = new();
    private int _scannedCount;

    public FileSearchService(List<ExclusionRule> exclusions, NetworkCredential credentials, CancellationToken ct)
    {
        _exclusions = exclusions;
        _credentials = credentials;
        _ct = ct;
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
        Action<int> onScanned)
    {
        foreach (var target in targets)
        {
            _ct.ThrowIfCancellationRequested();

            if (!target.IsOnline) continue;

            foreach (var share in shares)
            {
                _ct.ThrowIfCancellationRequested();

                if (!target.AvailableShares.Contains(share))
                    continue;

                var uncPath = $"\\\\{target.IpAddress}\\{share}";
                SearchDirectory(uncPath, target.IpAddress, share, filePattern,
                    searchContent, contentText, contentExtensions, excludeExtensions, includeNoExt,
                    onResult, onCurrentFile, onScanned);
            }
        }
    }

    private void SearchDirectory(
        string directory,
        string ip,
        string share,
        string filePattern,
        bool searchContent,
        string contentText,
        List<string> contentExtensions,
        List<string> excludeExtensions,
        bool includeNoExt,
        Action<SearchResult> onResult,
        Action<string> onCurrentFile,
        Action<int> onScanned)
    {
        var searchWords = searchContent && !string.IsNullOrEmpty(contentText)
            ? contentText.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : Array.Empty<string>();

        IEnumerable<string> files;
        var effectivePattern = searchContent ? "*" : filePattern;
        try
        {
            files = Directory.EnumerateFiles(directory, effectivePattern, new EnumerationOptions
            {
                RecurseSubdirectories = false,
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.System
            });
        }
        catch (UnauthorizedAccessException) { return; }
        catch (IOException) { return; }

        foreach (var file in files)
        {
            _ct.ThrowIfCancellationRequested();

            onScanned(Interlocked.Increment(ref _scannedCount));

            if (_exclusionService.IsExcluded(file, _exclusions))
                continue;

            onCurrentFile(file);

            FileInfo? info = null;
            try { info = new FileInfo(file); }
            catch { continue; }

            var ext = Path.GetExtension(file).ToLowerInvariant();
            var hasNoExt = string.IsNullOrEmpty(ext);

            if (searchContent)
            {
                if (hasNoExt && !includeNoExt)
                    continue;

                if (!hasNoExt && contentExtensions.Count > 0 && !contentExtensions.Contains(ext))
                    continue;

                if (searchWords.Length > 0 && !FileContainsAny(file, searchWords))
                    continue;
            }

            if (!hasNoExt && excludeExtensions.Contains(ext))
                continue;

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

        IEnumerable<string> subdirs;
        try
        {
            subdirs = Directory.EnumerateDirectories(directory, "*", new EnumerationOptions
            {
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.System | FileAttributes.Hidden
            });
        }
        catch (UnauthorizedAccessException) { return; }
        catch (IOException) { return; }

        foreach (var subdir in subdirs)
        {
            _ct.ThrowIfCancellationRequested();

            if (_exclusionService.IsExcludedDirectory(subdir, _exclusions))
                continue;

            SearchDirectory(subdir, ip, share, filePattern,
                searchContent, contentText, contentExtensions, excludeExtensions, includeNoExt,
                onResult, onCurrentFile, onScanned);
        }
    }

    private bool FileContainsAny(string filePath, string[] words)
    {
        try
        {
            var content = File.ReadAllText(filePath);

            foreach (var word in words)
            {
                if (content.Contains(word, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }
}

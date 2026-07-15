using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileSearchPro.Models;
using FileSearchPro.Services;

namespace FileSearchPro.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly SettingsService _settingsService = new();
    private readonly ExclusionService _exclusionService = new();
    private readonly AuthService _authService = new();
    private readonly NetworkScanner _networkScanner = new();
    private FileSearchService? _searchService;
    private CancellationTokenSource? _cts;
    private List<ExclusionRule> _exclusions = new();
    private SearchConfig _config = new();
    private string _password = string.Empty;
    private readonly DispatcherTimer _threadTimer;
    private volatile bool _isClosing;

    [ObservableProperty]
    private string _ipRange = string.Empty;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private string _currentFileText = string.Empty;

    [ObservableProperty]
    private string _threadCountText = string.Empty;

    [ObservableProperty]
    private string _scannedCountText = "0";

    [ObservableProperty]
    private string _foundCountText = "0";

    [ObservableProperty]
    private string _logCountText = "0";

    [ObservableProperty]
    private bool _isSearchProgressVisible;

    [ObservableProperty]
    private bool _isStartEnabled = true;

    [ObservableProperty]
    private bool _isStopEnabled;

    [ObservableProperty]
    private SearchResult? _selectedResult;

    [ObservableProperty]
    private string _previewFileName = string.Empty;

    [ObservableProperty]
    private string _previewText = string.Empty;

    public ObservableCollection<ScanLogEntry> LogEntries { get; } = new();
    public ObservableCollection<SearchResult> Results { get; } = new();

    public MainViewModel()
    {
        _config = _settingsService.LoadConfig();
        _exclusions = _exclusionService.LoadExclusions();
        IpRange = _config.LastIpRange;

        _threadTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _threadTimer.Tick += ThreadTimer_Tick;

        if (!string.IsNullOrEmpty(_config.Language))
            LanguageManager.SetLanguage(_config.Language);

        LanguageManager.LanguageChanged += OnLanguageChanged;
    }

    public event Action? RequestScrollToLast;

    [RelayCommand]
    private void OpenSettings()
    {
        _config.LastIpRange = IpRange;
        var dialog = new SettingsWindow(_config, _exclusions);
        if (dialog.ShowDialog() == true)
        {
            _config = dialog.Config;
            _exclusions = dialog.Exclusions;
            _password = dialog.Password;
            _settingsService.SaveConfig(_config);
            _exclusionService.SaveExclusions(_exclusions);
            IpRange = _config.LastIpRange;
        }
    }

    [RelayCommand]
    private void ClearLog()
    {
        LogEntries.Clear();
        LogCountText = string.Format(LanguageManager.GetString("EntriesCount"), 0);
    }

    [RelayCommand]
    private void CopyLog()
    {
        var text = string.Join(Environment.NewLine, LogEntries.Select(entry =>
            $"[{entry.Timestamp:HH:mm:ss}] [{entry.Status}] [{entry.IpAddress}] {entry.Message}"));
        System.Windows.Clipboard.SetText(text);
    }

    [RelayCommand]
    private void ExportLog()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Log files (*.log)|*.log|All files (*.*)|*.*",
            DefaultExt = ".log",
            FileName = $"FileSearchPro_{DateTime.Now:yyyyMMdd_HHmmss}.log"
        };

        if (dialog.ShowDialog() == true)
        {
            var text = string.Join(Environment.NewLine, LogEntries.Select(entry =>
                $"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss}] [{entry.Status}] [{entry.IpAddress}] {entry.Message}"));
            File.WriteAllText(dialog.FileName, text);
        }
    }

    [RelayCommand]
    private async Task StartSearchAsync()
    {
        if (string.IsNullOrWhiteSpace(IpRange))
        {
            System.Windows.MessageBox.Show(
                LanguageManager.GetString("MsgEnterIP"),
                LanguageManager.GetString("MsgError"),
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
            return;
        }

        _config.LastIpRange = IpRange;
        _settingsService.SaveConfig(_config);

        IsStartEnabled = false;
        IsStopEnabled = true;
        IsSearchProgressVisible = true;
        CurrentFileText = LanguageManager.GetString("ConnectingHosts");
        StatusText = LanguageManager.GetString("Searching");
        _threadTimer.Start();

        var selectedShares = _config.SearchAllShares
            ? new List<string> { "C$", "D$", "ADMIN$", "Users", "IPC$" }
            : _config.SelectedShares.Where(s => new[] { "C$", "D$", "Users" }.Contains(s)).ToList();

        var credentials = _authService.GetCredentials(
            _config.UseCurrentUser,
            _config.Domain,
            _config.Username,
            _password);

        _cts = new CancellationTokenSource();

        try
        {
            var ips = NetworkScanner.ParseIpRange(IpRange);
            LogEntries.Clear();
            ScannedCountText = "0";
            FoundCountText = "0";
            CurrentFileText = string.Format(LanguageManager.GetString("ScanningAddresses"), ips.Count);

            OnScanLog(new ScanLogEntry
            {
                Status = ScanLogEntryStatus.Info,
                Message = $"Настройки: ping={_config.PingTimeoutMs}мс, шары={_config.ShareTimeoutMs}мс, файлы={_config.FileIOTimeoutMs}мс"
            });

            var authMode = _config.UseCurrentUser
                ? "текущий пользователь"
                : $"{_config.Domain}\\{_config.Username}";
            OnScanLog(new ScanLogEntry
            {
                Status = ScanLogEntryStatus.Info,
                Message = $"Авторизация: {authMode}"
            });

            OnScanLog(new ScanLogEntry
            {
                Status = ScanLogEntryStatus.Info,
                Message = string.Format(LanguageManager.GetString("ScanStarted"), ips.Count)
            });

            _networkScanner.SetTimeouts(_config.PingTimeoutMs, _config.ShareTimeoutMs);
            _networkScanner.SetCredentials(credentials);

            var results = new List<SearchResult>();
            var lockObj = new object();
            var filePattern = _config.FilePattern;
            var searchContent = _config.SearchContent;
            var contentText = _config.ContentSearchText;
            var contentExtensions = _config.ContentExtensions
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(e => e.StartsWith('.') ? e.ToLowerInvariant() : "." + e.ToLowerInvariant())
                .ToList();
            var excludeExtensions = _config.ExcludeExtensions
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(e => e.StartsWith('.') ? e.ToLowerInvariant() : "." + e.ToLowerInvariant())
                .ToList();
            var includeNoExt = _config.IncludeNoExt;
            var lastResultSnapshot = 0;
            var resultTimer = new Stopwatch();

            _searchService = new FileSearchService(_exclusions, credentials, _cts.Token, _config.FileIOTimeoutMs);

            var totalSw = Stopwatch.StartNew();
            var searchTask = Task.Run(() =>
            {
                var phaseSw = Stopwatch.StartNew();
                var targets = _networkScanner.ScanNetwork(ips, _cts.Token, OnScanLog, OnScanProgress);
                var onlineTargets = targets.Where(t => t.IsOnline).ToList();
                phaseSw.Stop();

                OnScanLog(new ScanLogEntry
                {
                    Status = ScanLogEntryStatus.Info,
                    Message = $"Сеть: {phaseSw.Elapsed.TotalMinutes:F1}мин | Онлайн: {onlineTargets.Count} | Оффлайн: {targets.Count - onlineTargets.Count}"
                });

                App.Current.Dispatcher.BeginInvoke(() =>
                {
                    CurrentFileText = string.Format(LanguageManager.GetString("FoundHostsSearching"), onlineTargets.Count);
                });

                _searchService.Search(
                    onlineTargets,
                    selectedShares,
                    filePattern,
                    searchContent,
                    contentText,
                    contentExtensions,
                    excludeExtensions,
                    includeNoExt,
                    onResult: result =>
                    {
                        lock (lockObj)
                        {
                            results.Add(result);
                            var count = results.Count;
                            if (count - lastResultSnapshot >= 20 || !resultTimer.IsRunning || resultTimer.ElapsedMilliseconds >= 1000)
                            {
                                lastResultSnapshot = count;
                                resultTimer.Restart();
                                var snapshot = results.ToArray();
                                App.Current.Dispatcher.BeginInvoke(() =>
                                {
                                    Results.Clear();
                                    foreach (var r in snapshot) Results.Add(r);
                                    FoundCountText = snapshot.Length.ToString();
                                });
                            }
                        }
                    },
                    onCurrentFile: path =>
                    {
                        App.Current.Dispatcher.BeginInvoke(() =>
                        {
                            CurrentFileText = path;
                        });
                    },
                    onScanned: count =>
                    {
                        App.Current.Dispatcher.BeginInvoke(() =>
                        {
                            ScannedCountText = count.ToString();
                        });
                    },
                    onLog: OnScanLog);
            }, _cts.Token);

            await searchTask;

            totalSw.Stop();
            var finalResults = results.ToArray();

            OnScanLog(new ScanLogEntry
            {
                Status = ScanLogEntryStatus.Info,
                Message = $"Всего: {totalSw.Elapsed.TotalMinutes:F1}мин | Файлов: {finalResults.Length}"
            });

            App.Current.Dispatcher.Invoke(() =>
            {
                Results.Clear();
                foreach (var r in finalResults) Results.Add(r);
                ScannedCountText = finalResults.Length.ToString();
                StatusText = LanguageManager.GetString("Finished");
                CurrentFileText = string.Format(LanguageManager.GetString("ReadyFoundFiles"), finalResults.Length);
                IsSearchProgressVisible = false;
            });

            OnScanLog(new ScanLogEntry
            {
                Status = ScanLogEntryStatus.Complete,
                Message = string.Format(LanguageManager.GetString("SearchCompleteFound"), finalResults.Length)
            });
        }
        catch (OperationCanceledException)
        {
            CurrentFileText = LanguageManager.GetString("SearchCancelled");
            IsSearchProgressVisible = false;
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                string.Format(LanguageManager.GetString("MsgErrorPrefix"), ex.Message),
                LanguageManager.GetString("MsgErrorTitle"),
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
            CurrentFileText = LanguageManager.GetString("SearchError");
            IsSearchProgressVisible = false;
        }
        finally
        {
            IsStartEnabled = true;
            IsStopEnabled = false;
            _threadTimer.Stop();
            ThreadCountText = "";
        }
    }

    [RelayCommand]
    private void StopSearch()
    {
        _cts?.Cancel();
        IsStopEnabled = false;
        IsStartEnabled = true;
        IsSearchProgressVisible = false;
        StatusText = LanguageManager.GetString("SearchCancelled");
        _threadTimer.Stop();
        ThreadCountText = "";
    }

    public async Task LoadPreviewAsync(SearchResult? selected)
    {
        if (selected == null) return;

        var ext = Path.GetExtension(selected.FileName).ToLowerInvariant();
        var hasNoExt = string.IsNullOrEmpty(ext);
        var textExts = new HashSet<string> { ".txt", ".log", ".csv", ".xml", ".json", ".cs", ".py", ".js", ".md", ".cfg", ".ini", ".conf" };

        PreviewFileName = selected.FileName;

        if (!hasNoExt && !textExts.Contains(ext))
        {
            PreviewText = string.Format(LanguageManager.GetString("PreviewUnavailable"), ext);
            return;
        }

        PreviewText = LanguageManager.GetString("Loading");

        try
        {
            var content = await Task.Run(() =>
            {
                var fi = new FileInfo(selected.FullPath);
                int maxBytes = 50000;
                using var stream = new FileStream(selected.FullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(stream);
                var buffer = new char[maxBytes];
                int read = reader.Read(buffer, 0, buffer.Length);
                var text = new string(buffer, 0, read);
                if (fi.Length > maxBytes)
                    text += string.Format(LanguageManager.GetString("PreviewTruncated"), fi.Length / 1024);
                return text;
            });

            PreviewText = content;
        }
        catch
        {
            PreviewText = LanguageManager.GetString("PreviewReadError");
        }
    }

    private void ThreadTimer_Tick(object? sender, EventArgs e)
    {
        var proc = Process.GetCurrentProcess();
        var threads = proc.Threads.Count;
        var memMB = proc.WorkingSet64 / 1024 / 1024;
        ThreadCountText = $"Потоков: {threads} | Память: {memMB}MB";
    }

    private void OnLanguageChanged()
    {
        LogCountText = string.Format(LanguageManager.GetString("EntriesCount"), LogEntries.Count);
    }

    private void OnScanLog(ScanLogEntry entry)
    {
        App.Current.Dispatcher.BeginInvoke(() =>
        {
            LogEntries.Add(entry);
            LogCountText = string.Format(LanguageManager.GetString("EntriesCount"), LogEntries.Count);
            RequestScrollToLast?.Invoke();
        });
    }

    private void OnScanProgress(string ip)
    {
        App.Current.Dispatcher.BeginInvoke(() =>
        {
            CurrentFileText = string.Format(LanguageManager.GetString("ScanningIP"), ip);
        });
    }

    public void OnClosing()
    {
        if (_isClosing) return;
        _isClosing = true;

        _cts?.Cancel();
        _threadTimer.Stop();
        LanguageManager.LanguageChanged -= OnLanguageChanged;

        try
        {
            _config.LastIpRange = IpRange;
            _settingsService.SaveConfig(_config);
            _exclusionService.SaveExclusions(_exclusions);
        }
        catch { }
    }
}

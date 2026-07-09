using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Threading;
using FileSearchPro.Models;
using FileSearchPro.Services;

namespace FileSearchPro;

public partial class MainWindow : Window
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
    private readonly ObservableCollection<ScanLogEntry> _logEntries = new();
    private readonly DispatcherTimer _threadTimer;
    private volatile bool _isClosing;

    public MainWindow()
    {
        InitializeComponent();
        _config = _settingsService.LoadConfig();
        _exclusions = _exclusionService.LoadExclusions();
        IpRangeBox.Text = _config.LastIpRange;
        LogListView.ItemsSource = _logEntries;

        _threadTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _threadTimer.Tick += ThreadTimer_Tick;

        if (!string.IsNullOrEmpty(_config.Language))
            LanguageManager.SetLanguage(_config.Language);

        LanguageManager.LanguageChanged += OnLanguageChanged;
    }

    private void ThreadTimer_Tick(object? sender, EventArgs e)
    {
        var proc = Process.GetCurrentProcess();
        var threads = proc.Threads.Count;
        var memMB = proc.WorkingSet64 / 1024 / 1024;
        ThreadCountText.Text = $"Потоков: {threads} | Память: {memMB}MB";
    }

    private void OnLanguageChanged()
    {
        LogCountText.Text = string.Format(LanguageManager.GetString("EntriesCount"), _logEntries.Count);
    }

    private void OpenSettings_Click(object sender, RoutedEventArgs e)
    {
        _config.LastIpRange = IpRangeBox.Text;
        var dialog = new SettingsWindow(_config, _exclusions);
        dialog.Owner = this;
        if (dialog.ShowDialog() == true)
        {
            _config = dialog.Config;
            _exclusions = dialog.Exclusions;
            _password = dialog.Password;
            _settingsService.SaveConfig(_config);
            _exclusionService.SaveExclusions(_exclusions);
            IpRangeBox.Text = _config.LastIpRange;
        }
    }

    private void ClearLog_Click(object sender, RoutedEventArgs e)
    {
        _logEntries.Clear();
        LogCountText.Text = string.Format(LanguageManager.GetString("EntriesCount"), 0);
    }

    private void CopyLog_Click(object sender, RoutedEventArgs e)
    {
        var text = string.Join(Environment.NewLine, _logEntries.Select(entry =>
            $"[{entry.Timestamp:HH:mm:ss}] [{entry.Status}] [{entry.IpAddress}] {entry.Message}"));
        Clipboard.SetText(text);
    }

    private void ExportLog_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Log files (*.log)|*.log|All files (*.*)|*.*",
            DefaultExt = ".log",
            FileName = $"FileSearchPro_{DateTime.Now:yyyyMMdd_HHmmss}.log"
        };

        if (dialog.ShowDialog() == true)
        {
            var text = string.Join(Environment.NewLine, _logEntries.Select(entry =>
                $"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss}] [{entry.Status}] [{entry.IpAddress}] {entry.Message}"));
            File.WriteAllText(dialog.FileName, text);
        }
    }

    private void OnScanLog(ScanLogEntry entry)
    {
        Dispatcher.BeginInvoke(() =>
        {
            _logEntries.Add(entry);
            LogCountText.Text = string.Format(LanguageManager.GetString("EntriesCount"), _logEntries.Count);
            LogListView.ScrollIntoView(LogListView.Items[^1]);
        });
    }

    private void OnScanProgress(string ip)
    {
        Dispatcher.BeginInvoke(() =>
        {
            CurrentFileText.Text = string.Format(LanguageManager.GetString("ScanningIP"), ip);
        });
    }

    private async void ResultsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ResultsGrid.SelectedItem is not SearchResult selected) return;

        var ext = Path.GetExtension(selected.FileName).ToLowerInvariant();
        var hasNoExt = string.IsNullOrEmpty(ext);
        var textExts = new HashSet<string> { ".txt", ".log", ".csv", ".xml", ".json", ".cs", ".py", ".js", ".md", ".cfg", ".ini", ".conf" };

        if (!hasNoExt && !textExts.Contains(ext))
        {
            PreviewFileName.Text = selected.FileName;
            PreviewTextBlock.Text = string.Format(LanguageManager.GetString("PreviewUnavailable"), ext);
            return;
        }

        PreviewFileName.Text = selected.FileName;
        PreviewTextBlock.Text = LanguageManager.GetString("Loading");

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

            var searchText = (_config.SearchContent) ? _config.ContentSearchText : null;
            ShowPreview(content, searchText);
        }
        catch
        {
            PreviewTextBlock.Text = LanguageManager.GetString("PreviewReadError");
        }
    }

    private void ShowPreview(string content, string? highlight)
    {
        if (string.IsNullOrEmpty(highlight))
        {
            PreviewTextBlock.Text = content;
            return;
        }

        var words = highlight.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length == 0)
        {
            PreviewTextBlock.Text = content;
            return;
        }

        var inlines = PreviewTextBlock.Inlines;
        inlines.Clear();

        var pattern = string.Join("|", words.Select(w => Regex.Escape(w)));
        var lines = content.Split('\n');

        foreach (var rawLine in lines)
        {
            var line = rawLine.EndsWith('\r') ? rawLine[..^1] : rawLine;

            if (words.Length > 0 && line.Length > 0)
            {
                var matches = Regex.Matches(line, pattern, RegexOptions.IgnoreCase);
                if (matches.Count > 0)
                {
                    int lastIdx = 0;
                    foreach (Match m in matches)
                    {
                        if (m.Index > lastIdx)
                            inlines.Add(new Run(line[lastIdx..m.Index]));

                        var run = new Run(m.Value);
                        run.Background = System.Windows.Media.Brushes.Gold;
                        run.Foreground = System.Windows.Media.Brushes.Black;
                        run.FontWeight = FontWeights.Bold;
                        inlines.Add(run);
                        lastIdx = m.Index + m.Length;
                    }
                    if (lastIdx < line.Length)
                        inlines.Add(new Run(line[lastIdx..]));
                }
                else
                {
                    inlines.Add(new Run(line));
                }
            }
            else
            {
                inlines.Add(new Run(line));
            }

            inlines.Add(new LineBreak());
        }
    }

    private async void StartSearch_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(IpRangeBox.Text))
        {
            MessageBox.Show(LanguageManager.GetString("MsgEnterIP"), LanguageManager.GetString("MsgError"), MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _config.LastIpRange = IpRangeBox.Text;
        _settingsService.SaveConfig(_config);

        BtnStart.IsEnabled = false;
        BtnStop.IsEnabled = true;
        SearchProgress.Visibility = Visibility.Visible;
        CurrentFileText.Text = LanguageManager.GetString("ConnectingHosts");
        StatusText.Text = LanguageManager.GetString("Searching");
        _threadTimer.Start();

        var selectedShares = new List<string>();
        if (_config.SelectedShares.Contains("C$")) selectedShares.Add("C$");
        if (_config.SelectedShares.Contains("D$")) selectedShares.Add("D$");
        if (_config.SelectedShares.Contains("Users")) selectedShares.Add("Users");

        var credentials = _authService.GetCredentials(
            _config.UseCurrentUser,
            _config.Domain,
            _config.Username,
            _password);

        _cts = new CancellationTokenSource();

        try
        {
            var ips = NetworkScanner.ParseIpRange(IpRangeBox.Text);
            _logEntries.Clear();
            ScannedCountText.Text = "0";
            FoundCountText.Text = "0";
            CurrentFileText.Text = string.Format(LanguageManager.GetString("ScanningAddresses"), ips.Count);

            OnScanLog(new ScanLogEntry
            {
                Status = ScanLogEntryStatus.Info,
                Message = $"Настройки: ping={_config.PingTimeoutMs}мс, шары={_config.ShareTimeoutMs}мс, файлы={_config.FileIOTimeoutMs}мс"
            });

            OnScanLog(new ScanLogEntry
            {
                Status = ScanLogEntryStatus.Info,
                Message = string.Format(LanguageManager.GetString("ScanStarted"), ips.Count)
            });

            _networkScanner.SetTimeouts(_config.PingTimeoutMs, _config.ShareTimeoutMs);

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

            // Всё в одном Task.Run — сеть + файлы, один поток
            var totalSw = Stopwatch.StartNew();
            var searchTask = Task.Run(() =>
            {
                // Фаза 1: Сканирование сети
                var phaseSw = Stopwatch.StartNew();
                var targets = _networkScanner.ScanNetwork(ips, _cts.Token, OnScanLog, OnScanProgress);
                var onlineTargets = targets.Where(t => t.IsOnline).ToList();
                phaseSw.Stop();

                OnScanLog(new ScanLogEntry
                {
                    Status = ScanLogEntryStatus.Info,
                    Message = $"Сеть: {phaseSw.Elapsed.TotalMinutes:F1}мин | Онлайн: {onlineTargets.Count} | Оффлайн: {targets.Count - onlineTargets.Count}"
                });

                Dispatcher.BeginInvoke(() =>
                {
                    CurrentFileText.Text = string.Format(LanguageManager.GetString("FoundHostsSearching"), onlineTargets.Count);
                });

                // Фаза 2: Поиск файлов
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
                                Dispatcher.BeginInvoke(() =>
                                {
                                    ResultsGrid.ItemsSource = snapshot;
                                    FoundCountText.Text = snapshot.Length.ToString();
                                });
                            }
                        }
                    },
                    onCurrentFile: path =>
                    {
                        Dispatcher.BeginInvoke(() =>
                        {
                            CurrentFileText.Text = path;
                        });
                    },
                    onScanned: count =>
                    {
                        Dispatcher.BeginInvoke(() =>
                        {
                            ScannedCountText.Text = count.ToString();
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

            Dispatcher.Invoke(() =>
            {
                ResultsGrid.ItemsSource = finalResults;
                ScannedCountText.Text = finalResults.Length.ToString();
                StatusText.Text = LanguageManager.GetString("Finished");
                CurrentFileText.Text = string.Format(LanguageManager.GetString("ReadyFoundFiles"), finalResults.Length);
                SearchProgress.Visibility = Visibility.Collapsed;
            });

            OnScanLog(new ScanLogEntry
            {
                Status = ScanLogEntryStatus.Complete,
                Message = string.Format(LanguageManager.GetString("SearchCompleteFound"), finalResults.Length)
            });
        }
        catch (OperationCanceledException)
        {
            CurrentFileText.Text = LanguageManager.GetString("SearchCancelled");
            SearchProgress.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            MessageBox.Show(string.Format(LanguageManager.GetString("MsgErrorPrefix"), ex.Message), LanguageManager.GetString("MsgErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
            CurrentFileText.Text = LanguageManager.GetString("SearchError");
            SearchProgress.Visibility = Visibility.Collapsed;
        }
        finally
        {
            BtnStart.IsEnabled = true;
            BtnStop.IsEnabled = false;
            _threadTimer.Stop();
            ThreadCountText.Text = "";
        }
    }

    private void StopSearch_Click(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        BtnStop.IsEnabled = false;
        BtnStart.IsEnabled = true;
        SearchProgress.Visibility = Visibility.Collapsed;
        StatusText.Text = LanguageManager.GetString("SearchCancelled");
        _threadTimer.Stop();
        ThreadCountText.Text = "";
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_isClosing) return;
        _isClosing = true;

        _cts?.Cancel();
        _threadTimer.Stop();
        LanguageManager.LanguageChanged -= OnLanguageChanged;

        try
        {
            _config.LastIpRange = IpRangeBox.Text;
            _settingsService.SaveConfig(_config);
            _exclusionService.SaveExclusions(_exclusions);
        }
        catch { }

        base.OnClosed(e);

        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < 3000)
        {
            Thread.Sleep(50);
        }

        Process.GetCurrentProcess().Kill();
    }
}

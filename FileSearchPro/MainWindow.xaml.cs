using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
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

    private bool _isLoading;

    public MainWindow()
    {
        InitializeComponent();
        _isLoading = true;
        LoadSettings();
        LoadExclusions();
        WorkersSlider.ValueChanged += WorkersSlider_ValueChanged;
        AttachAutoSaveHandlers();
        _isLoading = false;
    }

    private void WorkersSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (WorkersCountText != null)
            WorkersCountText.Text = ((int)WorkersSlider.Value).ToString();
        AutoSave();
    }

    private void AttachAutoSaveHandlers()
    {
        IpRangeBox.TextChanged += (_, _) => AutoSave();
        FilePatternBox.TextChanged += (_, _) => AutoSave();
        MinSizeBox.TextChanged += (_, _) => AutoSave();
        MaxSizeBox.TextChanged += (_, _) => AutoSave();
        ContentSearchBox.TextChanged += (_, _) => AutoSave();
        ContentExtensionsBox.TextChanged += (_, _) => AutoSave();
        ContentExcludeExtensionsBox.TextChanged += (_, _) => AutoSave();
        ChkShareC.Checked += (_, _) => AutoSave();
        ChkShareC.Unchecked += (_, _) => AutoSave();
        ChkShareD.Checked += (_, _) => AutoSave();
        ChkShareD.Unchecked += (_, _) => AutoSave();
        ChkUsers.Checked += (_, _) => AutoSave();
        ChkUsers.Unchecked += (_, _) => AutoSave();
        ChkAll.Checked += (_, _) => AutoSave();
        ChkAll.Unchecked += (_, _) => AutoSave();
        ChkSearchContent.Checked += (_, _) => AutoSave();
        ChkSearchContent.Unchecked += (_, _) => AutoSave();
        ChkIncludeNoExt.Checked += (_, _) => AutoSave();
        ChkIncludeNoExt.Unchecked += (_, _) => AutoSave();
        RbCurrentUser.Checked += (_, _) => AutoSave();
        RbCustom.Checked += (_, _) => AutoSave();
        DomainBox.TextChanged += (_, _) => AutoSave();
        UsernameBox.TextChanged += (_, _) => AutoSave();
        DateFromPicker.SelectedDateChanged += (_, _) => AutoSave();
        DateToPicker.SelectedDateChanged += (_, _) => AutoSave();
    }

    private void AutoSave()
    {
        if (_isLoading) return;
        SaveSettings();
    }

    private void LoadSettings()
    {
        var config = _settingsService.LoadConfig();
        IpRangeBox.Text = config.LastIpRange;
        FilePatternBox.Text = config.FilePattern;
        ChkShareC.IsChecked = config.SelectedShares.Contains("C$");
        ChkShareD.IsChecked = config.SelectedShares.Contains("D$");
        ChkUsers?.SetValue(ToggleButton.IsCheckedProperty, config.SelectedShares.Contains("Users"));

        if (config.MinSize.HasValue)
            MinSizeBox.Text = (config.MinSize.Value / 1024).ToString();
        if (config.MaxSize.HasValue)
            MaxSizeBox.Text = (config.MaxSize.Value / 1024).ToString();
        if (config.DateFrom.HasValue)
            DateFromPicker.SelectedDate = config.DateFrom;
        if (config.DateTo.HasValue)
            DateToPicker.SelectedDate = config.DateTo;

        RbCurrentUser.IsChecked = config.UseCurrentUser;
        RbCustom.IsChecked = !config.UseCurrentUser;
        DomainBox.Text = config.Domain;
        UsernameBox.Text = config.Username;

        WorkersSlider.Value = config.MaxWorkers;
        WorkersCountText.Text = config.MaxWorkers.ToString();
        ChkSearchContent.IsChecked = config.SearchContent;
        ContentSearchBox.Text = config.ContentSearchText;
        ContentExtensionsBox.Text = config.ContentExtensions;
        ContentExcludeExtensionsBox.Text = config.ExcludeExtensions;
        ChkIncludeNoExt.IsChecked = config.IncludeNoExt;
    }

    private void LoadExclusions()
    {
        _exclusions = _exclusionService.LoadExclusions();
        RefreshExclusionsList();
    }

    private void RefreshExclusionsList()
    {
        ExclusionsList.Items.Clear();
        foreach (var ex in _exclusions.Where(e => e.IsEnabled))
        {
            ExclusionsList.Items.Add($"[{ex.Type}] {ex.Pattern}");
        }
    }

    private void SaveSettings()
    {
        var selectedShares = new List<string>();
        if (ChkShareC.IsChecked == true) selectedShares.Add("C$");
        if (ChkShareD.IsChecked == true) selectedShares.Add("D$");
        if (ChkUsers?.IsChecked == true) selectedShares.Add("Users");

        long? minSize = null, maxSize = null;
        if (long.TryParse(MinSizeBox.Text, out long minKb)) minSize = minKb * 1024;
        if (long.TryParse(MaxSizeBox.Text, out long maxKb)) maxSize = maxKb * 1024;

        var config = new SearchConfig
        {
            LastIpRange = IpRangeBox.Text,
            SelectedShares = selectedShares,
            FilePattern = FilePatternBox.Text,
            MinSize = minSize,
            MaxSize = maxSize,
            DateFrom = DateFromPicker.SelectedDate,
            DateTo = DateToPicker.SelectedDate,
            UseCurrentUser = RbCurrentUser.IsChecked == true,
            Username = UsernameBox.Text,
            Domain = DomainBox.Text,
            MaxWorkers = (int)WorkersSlider.Value,
            SearchContent = ChkSearchContent.IsChecked == true,
            ContentSearchText = ContentSearchBox.Text,
            ContentExtensions = ContentExtensionsBox.Text,
            ExcludeExtensions = ContentExcludeExtensionsBox.Text,
            IncludeNoExt = ChkIncludeNoExt.IsChecked == true
        };
        _settingsService.SaveConfig(config);
    }

    private void RbCurrentUser_Checked(object sender, RoutedEventArgs e)
    {
        if (CustomCredsPanel != null)
            CustomCredsPanel.IsEnabled = false;
    }

    private void RbCustom_Checked(object sender, RoutedEventArgs e)
    {
        if (CustomCredsPanel != null)
            CustomCredsPanel.IsEnabled = true;
    }

    private void AddExclusion_Click(object sender, RoutedEventArgs e)
    {
        var pattern = NewExclusionBox.Text.Trim();
        if (string.IsNullOrEmpty(pattern)) return;

        var type = (ExclusionTypeCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() == "Папка" ? "folder" : "file";
        var rule = new ExclusionRule { Pattern = pattern, Type = type, IsEnabled = true };
        _exclusions.Add(rule);
        _exclusionService.SaveExclusions(_exclusions);
        RefreshExclusionsList();
        NewExclusionBox.Text = string.Empty;
    }

    private void RemoveExclusion_Click(object sender, RoutedEventArgs e)
    {
        if (ExclusionsList.SelectedIndex < 0) return;
        var selected = ExclusionsList.SelectedItem.ToString()!;
        var pattern = selected.Split(' ', 2)[1];
        _exclusions.RemoveAll(ex => ex.Pattern == pattern);
        _exclusionService.SaveExclusions(_exclusions);
        RefreshExclusionsList();
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
            PreviewTextBlock.Text = $"Предпросмотр недоступен для {ext}";
            return;
        }

        PreviewFileName.Text = selected.FileName;
        PreviewTextBlock.Text = "Загрузка...";

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
                    text += "\n\n... (обрезано, показано 50КБ из " + (fi.Length / 1024) + "КБ)";
                return text;
            });

            var searchText = (ChkSearchContent.IsChecked == true) ? ContentSearchBox.Text : null;
            ShowPreview(content, searchText);
        }
        catch
        {
            PreviewTextBlock.Text = "Ошибка чтения файла";
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
            MessageBox.Show("Введите IP-адреса или диапазон.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        SaveSettings();
        BtnStart.IsEnabled = false;
        BtnStop.IsEnabled = true;
        SearchProgress.Visibility = Visibility.Visible;
        CurrentFileText.Text = "Подключение к хостам...";
        StatusText.Text = "Поиск...";

        var selectedShares = new List<string>();
        if (ChkShareC.IsChecked == true) selectedShares.Add("C$");
        if (ChkShareD.IsChecked == true) selectedShares.Add("D$");
        if (ChkUsers?.IsChecked == true) selectedShares.Add("Users");
        if (ChkAll.IsChecked == true)
        {
            selectedShares = new List<string> { "C$", "D$", "Users", "ADMIN$", "IPC$" };
        }

        var credentials = _authService.GetCredentials(
            RbCurrentUser.IsChecked == true,
            DomainBox.Text,
            UsernameBox.Text,
            PasswordBox.Password);

        _cts = new CancellationTokenSource();

        try
        {
            var ips = NetworkScanner.ParseIpRange(IpRangeBox.Text);
            ScannedCountText.Text = "0";
            FoundCountText.Text = "0";
            CurrentFileText.Text = $"Сканирование {ips.Count} адресов...";

            var maxWorkers = (int)WorkersSlider.Value;
            var targets = await _networkScanner.ScanNetworkAsync(ips, maxWorkers, _cts.Token);
            var onlineTargets = targets.Where(t => t.IsOnline).ToList();

            CurrentFileText.Text = $"Найдено {onlineTargets.Count} хостов. Поиск файлов...";

            var results = new List<SearchResult>();
            var lockObj = new object();
            var filePattern = FilePatternBox.Text;
            var searchContent = ChkSearchContent.IsChecked == true;
            var contentText = ContentSearchBox.Text;
            var contentExtensions = ContentExtensionsBox.Text
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(e => e.StartsWith('.') ? e.ToLowerInvariant() : "." + e.ToLowerInvariant())
                .ToList();
            var excludeExtensions = ContentExcludeExtensionsBox.Text
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(e => e.StartsWith('.') ? e.ToLowerInvariant() : "." + e.ToLowerInvariant())
                .ToList();
            var includeNoExt = ChkIncludeNoExt.IsChecked == true;

            _searchService = new FileSearchService(_exclusions, credentials, _cts.Token);

            await Task.Run(() =>
            {
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
                            var snapshot = results.ToArray();
                            Dispatcher.BeginInvoke(() =>
                            {
                                ResultsGrid.ItemsSource = snapshot;
                                FoundCountText.Text = snapshot.Length.ToString();
                            });
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
                    });
            }, _cts.Token);

            var finalResults = results.ToArray();
            Dispatcher.Invoke(() =>
            {
                ResultsGrid.ItemsSource = finalResults;
                ScannedCountText.Text = finalResults.Length.ToString();
                StatusText.Text = $"Завершено";
                CurrentFileText.Text = $"Готов. Найдено файлов: {finalResults.Length}";
                SearchProgress.Visibility = Visibility.Collapsed;
            });
        }
        catch (OperationCanceledException)
        {
            CurrentFileText.Text = "Поиск отменён";
            SearchProgress.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            CurrentFileText.Text = "Ошибка поиска";
            SearchProgress.Visibility = Visibility.Collapsed;
        }
        finally
        {
            BtnStart.IsEnabled = true;
            BtnStop.IsEnabled = false;
        }
    }

    private void StopSearch_Click(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        BtnStop.IsEnabled = false;
    }

    protected override void OnClosed(EventArgs e)
    {
        SaveSettings();
        _exclusionService.SaveExclusions(_exclusions);
        base.OnClosed(e);
    }
}

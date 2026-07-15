using System.Windows;
using System.Windows.Controls;
using FileSearchPro.Models;
using FileSearchPro.Services;

namespace FileSearchPro;

public partial class SettingsWindow : Window
{
    public SearchConfig Config { get; private set; }
    public List<ExclusionRule> Exclusions { get; private set; }
    public string Password => PasswordBox.Password;

    private readonly ExclusionService _exclusionService = new();

    public SettingsWindow(SearchConfig config, List<ExclusionRule> exclusions)
    {
        InitializeComponent();
        Config = config;
        Exclusions = new List<ExclusionRule>(exclusions);
        LoadControlsFromConfig();
        LoadExclusionsList();
    }

    private void LoadControlsFromConfig()
    {
        if (Config.Language == "en")
            RbLangEn.IsChecked = true;
        else
            RbLangRu.IsChecked = true;

        ChkShareC.IsChecked = Config.SelectedShares.Contains("C$");
        ChkShareD.IsChecked = Config.SelectedShares.Contains("D$");
        ChkUsers.IsChecked = Config.SelectedShares.Contains("Users");
        ChkAll.IsChecked = Config.SearchAllShares;

        FilePatternBox.Text = Config.FilePattern;

        if (Config.MinSize.HasValue)
            MinSizeBox.Text = (Config.MinSize.Value / 1024).ToString();
        if (Config.MaxSize.HasValue)
            MaxSizeBox.Text = (Config.MaxSize.Value / 1024).ToString();
        if (Config.DateFrom.HasValue)
            DateFromPicker.SelectedDate = Config.DateFrom;
        if (Config.DateTo.HasValue)
            DateToPicker.SelectedDate = Config.DateTo;

        ChkSearchContent.IsChecked = Config.SearchContent;
        ContentSearchBox.Text = Config.ContentSearchText;
        ContentExtensionsBox.Text = Config.ContentExtensions;
        ContentExcludeExtensionsBox.Text = Config.ExcludeExtensions;
        ChkIncludeNoExt.IsChecked = Config.IncludeNoExt;

        RbCurrentUser.IsChecked = Config.UseCurrentUser;
        RbCustom.IsChecked = !Config.UseCurrentUser;
        DomainBox.Text = Config.Domain;
        UsernameBox.Text = Config.Username;

        PingTimeoutBox.Text = Config.PingTimeoutMs.ToString();
        ShareTimeoutBox.Text = Config.ShareTimeoutMs.ToString();
        FileIOTimeoutBox.Text = Config.FileIOTimeoutMs.ToString();
    }

    private void LoadExclusionsList()
    {
        ExclusionsList.Items.Clear();
        foreach (var ex in Exclusions.Where(e => e.IsEnabled))
            ExclusionsList.Items.Add($"[{ex.Type}] {ex.Pattern}");
    }

    private SearchConfig CollectConfigFromControls()
    {
        var searchAll = ChkAll.IsChecked == true;
        var selectedShares = new List<string>();
        if (searchAll)
        {
            selectedShares = new List<string> { "C$", "D$", "ADMIN$", "Users", "IPC$" };
        }
        else
        {
            if (ChkShareC.IsChecked == true) selectedShares.Add("C$");
            if (ChkShareD.IsChecked == true) selectedShares.Add("D$");
            if (ChkUsers?.IsChecked == true) selectedShares.Add("Users");
        }

        long? minSize = null, maxSize = null;
        if (long.TryParse(MinSizeBox.Text, out long minKb)) minSize = minKb * 1024;
        if (long.TryParse(MaxSizeBox.Text, out long maxKb)) maxSize = maxKb * 1024;

        return new SearchConfig
        {
            LastIpRange = Config.LastIpRange,
            SelectedShares = selectedShares,
            SearchAllShares = searchAll,
            FilePattern = FilePatternBox.Text,
            MinSize = minSize,
            MaxSize = maxSize,
            DateFrom = DateFromPicker.SelectedDate,
            DateTo = DateToPicker.SelectedDate,
            UseCurrentUser = RbCurrentUser.IsChecked == true,
            Username = UsernameBox.Text,
            Domain = DomainBox.Text,
            SearchContent = ChkSearchContent.IsChecked == true,
            ContentSearchText = ContentSearchBox.Text,
            ContentExtensions = ContentExtensionsBox.Text,
            ExcludeExtensions = ContentExcludeExtensionsBox.Text,
            IncludeNoExt = ChkIncludeNoExt.IsChecked == true,
            Language = RbLangEn.IsChecked == true ? "en" : "ru",
            PingTimeoutMs = int.TryParse(PingTimeoutBox.Text, out var pt) ? pt : 300,
            ShareTimeoutMs = int.TryParse(ShareTimeoutBox.Text, out var st) ? st : 3000,
            FileIOTimeoutMs = int.TryParse(FileIOTimeoutBox.Text, out var ft) ? ft : 5000
        };
    }

    private void RbLangRu_Checked(object sender, RoutedEventArgs e)
    {
        if (RbLangRu?.IsChecked == true)
            LanguageManager.SetLanguage("ru");
    }

    private void RbLangEn_Checked(object sender, RoutedEventArgs e)
    {
        if (RbLangEn?.IsChecked == true)
            LanguageManager.SetLanguage("en");
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

        var type = ExclusionTypeCombo.SelectedIndex == 0 ? "folder" : "file";
        var rule = new ExclusionRule { Pattern = pattern, Type = type, IsEnabled = true };
        Exclusions.Add(rule);
        LoadExclusionsList();
        NewExclusionBox.Text = string.Empty;
    }

    private void RemoveExclusion_Click(object sender, RoutedEventArgs e)
    {
        if (ExclusionsList.SelectedIndex < 0) return;
        var selected = ExclusionsList.SelectedItem.ToString()!;
        var pattern = selected.Split(' ', 2)[1];
        Exclusions.RemoveAll(ex => ex.Pattern == pattern);
        LoadExclusionsList();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        Config = CollectConfigFromControls();
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}

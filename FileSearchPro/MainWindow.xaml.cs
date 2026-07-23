using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using FileSearchPro.ViewModels;

namespace FileSearchPro;

public partial class MainWindow : Window
{
    private MainViewModel? _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _viewModel = DataContext as MainViewModel;
        if (_viewModel != null)
        {
            _viewModel.RequestScrollToLast += ScrollLogToLast;
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        }
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.SelectedResult) && _viewModel?.SelectedResult != null)
        {
            _ = _viewModel.LoadPreviewAsync(_viewModel.SelectedResult);
        }
    }

    private void ScrollLogToLast()
    {
        if (LogListView.Items.Count > 0)
            LogListView.ScrollIntoView(LogListView.Items[^1]);
    }

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _viewModel?.OnClosing();
    }
}

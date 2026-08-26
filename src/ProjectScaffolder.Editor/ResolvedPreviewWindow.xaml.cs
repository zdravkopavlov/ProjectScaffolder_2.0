using System.IO;
using System.Windows;
using ProjectScaffolder.Editor.Services;
using ProjectScaffolder.Editor.ViewModels;

namespace ProjectScaffolder.Editor;

public partial class ResolvedPreviewWindow : Window
{
    private readonly MainViewModel mainViewModel;

    internal ResolvedPreviewWindow(MainViewModel mainViewModel)
    {
        this.mainViewModel = mainViewModel;
        InitializeComponent();
        TargetPathBox.Text = Path.Combine(Path.GetTempPath(), "ProjectScaffolder Preview", "Sample Project");
        RefreshPreview();
    }

    private void Window_SourceInitialized(object? sender, EventArgs e) => ThemeManager.ApplyWindowChrome(this);

    private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshPreview();

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void RefreshPreview()
    {
        DataContext = mainViewModel.BuildResolvedPreview(TargetPathBox.Text);
        OutputList.SelectedIndex = OutputList.Items.Count > 0 ? 0 : -1;
    }
}

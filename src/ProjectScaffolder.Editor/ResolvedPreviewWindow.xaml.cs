using System.Diagnostics;
using System.IO;
using System.Windows;
using ProjectScaffolder.Core.Generation;
using ProjectScaffolder.Core.Serialization;
using ProjectScaffolder.Editor.Services;
using ProjectScaffolder.Editor.ViewModels;

namespace ProjectScaffolder.Editor;

public partial class ResolvedPreviewWindow : Window
{
    private readonly MainViewModel mainViewModel;
    private string? generatedWorkspacePath;
    private string? generatedProjectPath;

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

    private async void Generate_Click(object sender, RoutedEventArgs e)
    {
        GenerateButton.IsEnabled = false;
        GenerationStatusText.Text = "Generating with the real project engine…";
        try
        {
            DisposableGenerationResult result = await mainViewModel.GenerateDisposableProjectAsync(TargetPathBox.Text).ConfigureAwait(true);
            generatedWorkspacePath = result.WorkspacePath;
            generatedProjectPath = result.ProjectPath;
            OpenGeneratedButton.IsEnabled = true;
            DeleteGeneratedButton.IsEnabled = true;
            GenerationStatusText.Text = $"{result.Summary}  {result.ProjectPath}";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or ProjectGenerationException or ContractValidationException)
        {
            GenerateButton.IsEnabled = true;
            GenerationStatusText.Text = $"Generation failed: {exception.Message}";
        }
    }

    private void OpenGenerated_Click(object sender, RoutedEventArgs e)
    {
        if (generatedProjectPath is null || !Directory.Exists(generatedProjectPath))
        {
            GenerationStatusText.Text = "The generated project folder is no longer available.";
            return;
        }

        try
        {
            ProcessStartInfo startInfo = new("explorer.exe") { UseShellExecute = true };
            startInfo.ArgumentList.Add(generatedProjectPath);
            _ = Process.Start(startInfo);
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            GenerationStatusText.Text = $"Could not open the generated folder: {exception.Message}";
        }
    }

    private void DeleteGenerated_Click(object sender, RoutedEventArgs e)
    {
        if (generatedWorkspacePath is null)
        {
            return;
        }

        try
        {
            _ = MainViewModel.DeleteDisposableWorkspace(generatedWorkspacePath);
            generatedWorkspacePath = null;
            generatedProjectPath = null;
            OpenGeneratedButton.IsEnabled = false;
            DeleteGeneratedButton.IsEnabled = false;
            GenerateButton.IsEnabled = true;
            GenerationStatusText.Text = "Disposable test project deleted.";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            GenerationStatusText.Text = $"Could not delete the test project: {exception.Message}";
        }
    }

    private void RefreshPreview()
    {
        DataContext = mainViewModel.BuildResolvedPreview(TargetPathBox.Text);
        OutputList.SelectedIndex = OutputList.Items.Count > 0 ? 0 : -1;
    }
}

using System.Configuration;
using System.Data;
using System.Windows;
using ProjectScaffolder.Editor.Services;
using ProjectScaffolder.Editor.ViewModels;

namespace ProjectScaffolder.Editor;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ThemeManager.Apply(ThemePreference.System);

        try
        {
            EditorPaths paths = EditorPaths.FromArguments(e.Args);
            TemplateLibraryStore store = new(paths);
            MainViewModel viewModel = new(store);
            await viewModel.InitializeAsync().ConfigureAwait(true);
            MainWindow window = new(viewModel);
            MainWindow = window;
            window.Show();
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"Project Scaffolder could not start.\n\n{exception.Message}",
                "Project Scaffolder",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }
}


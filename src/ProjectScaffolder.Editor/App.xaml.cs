using System.Configuration;
using System.Data;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
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
            EditorStartupOptions options = EditorStartupOptions.Parse(e.Args);
            TemplateLibraryStore store = new(options.Paths);
            MainViewModel viewModel = new(store);
            await viewModel.InitializeAsync().ConfigureAwait(true);
            MainWindow window = new(viewModel);
            MainWindow = window;
            window.Show();
            if (options.SmokeSelectFirstNode)
            {
                TemplateListItemViewModel? item = viewModel.Templates.FirstOrDefault(candidate => candidate.Editor?.Nodes.Count > 0);
                if (item is null)
                {
                    throw new InvalidOperationException("The editor smoke test could not find a template node to select.");
                }

                viewModel.SelectedListItem = item;
                viewModel.SelectedNode = item.Editor!.Nodes[0];
                ThemeManager.Apply(ThemePreference.Light);
                if (TryFindResource("WindowBackgroundBrush") is not SolidColorBrush lightBrush || lightBrush.Color != Color.FromRgb(0xF4, 0xF4, 0xF4))
                {
                    throw new InvalidOperationException("The Light theme resources were not applied.");
                }

                ThemeManager.Apply(ThemePreference.Dark);
                if (TryFindResource("WindowBackgroundBrush") is not SolidColorBrush darkBrush || darkBrush.Color != Color.FromRgb(0x1B, 0x1B, 0x1B))
                {
                    throw new InvalidOperationException("The Dark theme resources were not applied.");
                }

                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                Shutdown(0);
            }
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


using System.Configuration;
using System.Data;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using ProjectScaffolder.Core.Contracts;
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
                TemplateListItemViewModel? item = viewModel.Templates.FirstOrDefault(candidate =>
                    candidate.Editor is not null && FlattenNodes(candidate.Editor.Nodes).Any(node => node.IsTextFile));
                EditorNodeViewModel? node = item?.Editor is null
                    ? null
                    : FlattenNodes(item.Editor.Nodes).FirstOrDefault(candidate => candidate.IsTextFile);
                if (item is null || node is null)
                {
                    throw new InvalidOperationException("The editor smoke test could not find a text-file node to select.");
                }

                viewModel.SelectedListItem = item;
                viewModel.SelectedNode = node;
                node.Collision = FileCollisionPolicy.Skip;
                node.Collision = null;
                item.Editor!.AcceptSaved(item.Editor.Snapshot());
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

                if (TryFindResource(SystemColors.InactiveSelectionHighlightBrushKey) is not SolidColorBrush inactiveSelectionBrush ||
                    inactiveSelectionBrush.Color != Color.FromRgb(0x35, 0x57, 0x7D))
                {
                    throw new InvalidOperationException("The Dark theme inactive-selection resources were not applied.");
                }

                ResolvedPreviewWindow previewWindow = new(viewModel)
                {
                    Owner = window,
                };
                previewWindow.Show();
                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                previewWindow.Close();
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

    private static IEnumerable<EditorNodeViewModel> FlattenNodes(IEnumerable<EditorNodeViewModel> nodes)
    {
        foreach (EditorNodeViewModel node in nodes)
        {
            yield return node;
            foreach (EditorNodeViewModel child in FlattenNodes(node.Children))
            {
                yield return child;
            }
        }
    }
}


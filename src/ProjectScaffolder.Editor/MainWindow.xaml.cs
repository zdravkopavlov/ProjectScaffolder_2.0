using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ProjectScaffolder.Editor.Services;
using ProjectScaffolder.Editor.ViewModels;

namespace ProjectScaffolder.Editor;

public partial class MainWindow : Window
{
    private bool closeApproved;

    internal MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private MainViewModel ViewModel => (MainViewModel)DataContext;

    private void Window_SourceInitialized(object? sender, EventArgs e) => ThemeManager.ApplyWindowChrome(this);

    private void StructureTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e) =>
        ViewModel.SelectedNode = e.NewValue as EditorNodeViewModel;

    private void ProjectRoot_Click(object sender, RoutedEventArgs e)
    {
        ClearTreeSelection(StructureTree);
        ViewModel.SelectedNode = null;
    }

    private static void ClearTreeSelection(ItemsControl itemsControl)
    {
        foreach (object item in itemsControl.Items)
        {
            if (itemsControl.ItemContainerGenerator.ContainerFromItem(item) is not TreeViewItem treeItem)
            {
                continue;
            }

            treeItem.IsSelected = false;
            ClearTreeSelection(treeItem);
        }
    }

    private void ThemePicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        ThemePreference preference = ThemePicker.SelectedIndex switch
        {
            1 => ThemePreference.Light,
            2 => ThemePreference.Dark,
            _ => ThemePreference.System,
        };
        ThemeManager.Apply(preference);
        ThemeManager.ApplyWindowChrome(this);
    }

    private async void RemoveTemplate_Click(object sender, RoutedEventArgs e)
    {
        TemplateListItemViewModel? item = ViewModel.SelectedListItem;
        if (item is null)
        {
            return;
        }

        string detail = item.Editor?.IsNew == true
            ? "This unsaved template will be discarded."
            : "Its saved package will be moved to Project Scaffolder's Trash folder so it remains recoverable.";
        MessageBoxResult result = MessageBox.Show(
            $"Remove '{item.DisplayName.TrimEnd(' ', '*')}' from Project Scaffolder?\n\n{detail}",
            "Remove template",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (result == MessageBoxResult.Yes)
        {
            _ = await ViewModel.RemoveSelectedTemplateAsync().ConfigureAwait(true);
        }
    }

    private async void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (closeApproved || !ViewModel.Templates.Any(item => item.Editor?.IsDirty == true))
        {
            return;
        }

        e.Cancel = true;
        MessageBoxResult result = MessageBox.Show(
            "There are unsaved template changes. Save them before closing?",
            "Project Scaffolder",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning);
        if (result == MessageBoxResult.Cancel)
        {
            return;
        }

        if (result == MessageBoxResult.Yes && !await ViewModel.SaveAllDirtyTemplatesAsync().ConfigureAwait(true))
        {
            return;
        }

        closeApproved = true;
        Close();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.S && Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && ViewModel.SaveTemplateCommand.CanExecute(null))
        {
            ViewModel.SaveTemplateCommand.Execute(null);
            e.Handled = true;
        }
    }
}

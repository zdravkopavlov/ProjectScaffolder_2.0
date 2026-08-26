using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ProjectScaffolder.Editor.Services;
using ProjectScaffolder.Editor.ViewModels;

namespace ProjectScaffolder.Editor;

public partial class MainWindow : Window
{
    private bool closeApproved;
    private Point dragStartPoint;
    private EditorNodeViewModel? dragCandidate;

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

    private void StructureTree_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        dragStartPoint = e.GetPosition(StructureTree);
        dragCandidate = FindVisualAncestor<TreeViewItem>(e.OriginalSource as DependencyObject)?.DataContext as EditorNodeViewModel;
    }

    private void StructureTree_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || dragCandidate is null ||
            FindVisualAncestor<TextBox>(e.OriginalSource as DependencyObject) is not null)
        {
            return;
        }

        Point position = e.GetPosition(StructureTree);
        if (Math.Abs(position.X - dragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(position.Y - dragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        EditorNodeViewModel node = dragCandidate;
        dragCandidate = null;
        _ = DragDrop.DoDragDrop(StructureTree, node, DragDropEffects.Move);
    }

    private void StructureTree_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = TryGetDropDestination(e, out EditorNodeViewModel? dragged, out EditorNodeViewModel? parent, out _)
            && ViewModel.CanMoveNode(dragged!, parent)
                ? DragDropEffects.Move
                : DragDropEffects.None;
        e.Handled = true;
    }

    private void StructureTree_Drop(object sender, DragEventArgs e)
    {
        if (TryGetDropDestination(e, out EditorNodeViewModel? dragged, out EditorNodeViewModel? parent, out int index) &&
            ViewModel.MoveNode(dragged!, parent, index))
        {
            TreeViewItem? targetContainer = FindVisualAncestor<TreeViewItem>(e.OriginalSource as DependencyObject);
            if (parent is not null && ReferenceEquals(targetContainer?.DataContext, parent))
            {
                targetContainer.IsExpanded = true;
            }
        }

        e.Handled = true;
    }

    private void ProjectRoot_DragOver(object sender, DragEventArgs e)
    {
        EditorNodeViewModel? dragged = GetDraggedNode(e);
        e.Effects = dragged is not null && ViewModel.CanMoveNode(dragged, null)
            ? DragDropEffects.Move
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void ProjectRoot_Drop(object sender, DragEventArgs e)
    {
        EditorNodeViewModel? dragged = GetDraggedNode(e);
        if (dragged is not null && ViewModel.SelectedTemplate is not null)
        {
            _ = ViewModel.MoveNode(dragged, null, ViewModel.SelectedTemplate.Nodes.Count);
        }

        e.Handled = true;
    }

    private bool TryGetDropDestination(
        DragEventArgs e,
        out EditorNodeViewModel? dragged,
        out EditorNodeViewModel? parent,
        out int insertionIndex)
    {
        dragged = GetDraggedNode(e);
        parent = null;
        insertionIndex = ViewModel.SelectedTemplate?.Nodes.Count ?? 0;
        if (dragged is null || ViewModel.SelectedTemplate is null)
        {
            return false;
        }

        TreeViewItem? targetContainer = FindVisualAncestor<TreeViewItem>(e.OriginalSource as DependencyObject);
        if (targetContainer?.DataContext is not EditorNodeViewModel target)
        {
            return true;
        }

        double relativeY = e.GetPosition(targetContainer).Y;
        bool centralFolderDrop = target.Kind == EditorNodeKind.Folder &&
            relativeY >= targetContainer.ActualHeight * 0.25 &&
            relativeY <= targetContainer.ActualHeight * 0.75;
        if (centralFolderDrop)
        {
            parent = target;
            insertionIndex = target.Children.Count;
            return true;
        }

        parent = target.Parent;
        var siblings = parent?.Children ?? ViewModel.SelectedTemplate.Nodes;
        insertionIndex = siblings.IndexOf(target);
        if (relativeY > targetContainer.ActualHeight / 2)
        {
            insertionIndex++;
        }

        return true;
    }

    private static EditorNodeViewModel? GetDraggedNode(DragEventArgs e) =>
        e.Data.GetDataPresent(typeof(EditorNodeViewModel))
            ? e.Data.GetData(typeof(EditorNodeViewModel)) as EditorNodeViewModel
            : null;

    private static T? FindVisualAncestor<T>(DependencyObject? source)
        where T : DependencyObject
    {
        for (DependencyObject? current = source; current is not null; current = GetVisualOrContentParent(current))
        {
            if (current is T match)
            {
                return match;
            }
        }

        return null;
    }

    private static DependencyObject? GetVisualOrContentParent(DependencyObject current) => current switch
    {
        ContentElement content => ContentOperations.GetParent(content) ?? (content as FrameworkContentElement)?.Parent,
        _ => VisualTreeHelper.GetParent(current),
    };

    private void InlineName_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is TextBox { IsVisible: true } textBox)
        {
            _ = textBox.Dispatcher.BeginInvoke(() =>
            {
                textBox.Focus();
                textBox.SelectAll();
            }, DispatcherPriority.Input);
        }
    }

    private void InlineName_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) => CommitInlineRename(sender as TextBox);

    private void InlineName_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox textBox || textBox.DataContext is not EditorNodeViewModel node)
        {
            return;
        }

        if (e.Key == Key.Escape)
        {
            node.EndRename();
            StructureTree.Focus();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            CommitInlineRename(textBox);
            StructureTree.Focus();
            e.Handled = true;
        }
    }

    private static void CommitInlineRename(TextBox? textBox)
    {
        if (textBox?.DataContext is not EditorNodeViewModel { IsEditing: true } node)
        {
            return;
        }

        node.Name = textBox.Text;
        node.EndRename();
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
        if (e.Key == Key.F2 && ViewModel.RenameNodeCommand.CanExecute(null))
        {
            ViewModel.RenameNodeCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.S && Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && ViewModel.SaveTemplateCommand.CanExecute(null))
        {
            ViewModel.SaveTemplateCommand.Execute(null);
            e.Handled = true;
        }
    }
}

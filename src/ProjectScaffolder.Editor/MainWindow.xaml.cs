using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using ProjectScaffolder.Editor.Services;
using ProjectScaffolder.Editor.ViewModels;

namespace ProjectScaffolder.Editor;

public partial class MainWindow : Window
{
    private bool closeApproved;
    private readonly DispatcherTimer renameTimer;
    private Point dragStartPoint;
    private EditorNodeViewModel? dragCandidate;
    private EditorNodeViewModel? renameClickCandidate;
    private EditorNodeViewModel? pendingRenameNode;
    private EditorNodeViewModel? dropTargetNode;
    private bool pointerMoved;
    private TextBox? variableInsertionTarget;
    private bool variableMenuOpen;
    private bool variableWasInserted;

    internal MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        renameTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(GetDoubleClickTime()),
        };
        renameTimer.Tick += RenameTimer_Tick;
    }

    private MainViewModel ViewModel => (MainViewModel)DataContext;

    [DllImport("user32.dll")]
    private static extern uint GetDoubleClickTime();

    private void Window_SourceInitialized(object? sender, EventArgs e) => ThemeManager.ApplyWindowChrome(this);

    private void StructureTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e) =>
        ViewModel.SelectedNode = e.NewValue as EditorNodeViewModel;

    private void ProjectRoot_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        ClearTreeSelection(StructureTree);
        ViewModel.SelectedNode = null;
        e.Handled = true;
    }

    private void StructureTree_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        CancelPendingMouseRename();
        dragStartPoint = e.GetPosition(StructureTree);
        pointerMoved = false;
        TreeViewItem? clickedItem = FindVisualAncestor<TreeViewItem>(e.OriginalSource as DependencyObject);
        dragCandidate = clickedItem?.DataContext as EditorNodeViewModel;
        renameClickCandidate = e.ClickCount == 1 &&
            ReferenceEquals(dragCandidate, ViewModel.SelectedNode) &&
            IsInsideNodeLabel(e.OriginalSource as DependencyObject) &&
            FindVisualAncestor<TextBox>(e.OriginalSource as DependencyObject) is null
                ? dragCandidate
                : null;
        if (clickedItem is null)
        {
            ClearTreeSelection(StructureTree);
            ViewModel.SelectedNode = null;
        }
    }

    private void StructureTree_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!pointerMoved && e.ClickCount == 1 && renameClickCandidate is not null &&
            ReferenceEquals(renameClickCandidate, ViewModel.SelectedNode))
        {
            pendingRenameNode = renameClickCandidate;
            renameTimer.Start();
        }

        renameClickCandidate = null;
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
        pointerMoved = true;
        CancelPendingMouseRename();
        try
        {
            _ = DragDrop.DoDragDrop(StructureTree, node, DragDropEffects.Move);
        }
        finally
        {
            ClearDropTarget();
        }
    }

    private void StructureTree_DragOver(object sender, DragEventArgs e)
    {
        bool valid = TryGetDropDestination(e, out EditorNodeViewModel? dragged, out EditorNodeViewModel? parent, out _) &&
            ViewModel.CanMoveNode(dragged!, parent);
        e.Effects = valid ? DragDropEffects.Move : DragDropEffects.None;
        if (valid)
        {
            SetDropTarget(parent);
        }
        else
        {
            ClearDropTarget();
        }

        e.Handled = true;
    }

    private void StructureTree_Drop(object sender, DragEventArgs e)
    {
        if (TryGetDropDestination(e, out EditorNodeViewModel? dragged, out EditorNodeViewModel? parent, out int index))
        {
            TreeViewItem? parentContainer = parent is null ? null : FindTreeViewItem(StructureTree, parent);
            if (ViewModel.MoveNode(dragged!, parent, index) && parentContainer is not null)
            {
                parentContainer.IsExpanded = true;
            }
        }

        ClearDropTarget();
        e.Handled = true;
    }

    private void ProjectRoot_DragOver(object sender, DragEventArgs e)
    {
        EditorNodeViewModel? dragged = GetDraggedNode(e);
        e.Effects = dragged is not null && ViewModel.CanMoveNode(dragged, null)
            ? DragDropEffects.Move
            : DragDropEffects.None;
        if (e.Effects == DragDropEffects.Move)
        {
            SetDropTarget(null);
        }
        else
        {
            ClearDropTarget();
        }

        e.Handled = true;
    }

    private void ProjectRoot_Drop(object sender, DragEventArgs e)
    {
        EditorNodeViewModel? dragged = GetDraggedNode(e);
        if (dragged is not null && ViewModel.SelectedTemplate is not null)
        {
            _ = ViewModel.MoveNode(dragged, null, ViewModel.SelectedTemplate.Nodes.Count);
        }

        ClearDropTarget();
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

        if (target.Kind == EditorNodeKind.Folder)
        {
            parent = target;
            insertionIndex = target.Children.Count;
            return true;
        }

        parent = target.Parent;
        var siblings = parent?.Children ?? ViewModel.SelectedTemplate.Nodes;
        insertionIndex = siblings.Count;

        return true;
    }

    private static EditorNodeViewModel? GetDraggedNode(DragEventArgs e) =>
        e.Data.GetDataPresent(typeof(EditorNodeViewModel))
            ? e.Data.GetData(typeof(EditorNodeViewModel)) as EditorNodeViewModel
            : null;

    private void SetDropTarget(EditorNodeViewModel? folder)
    {
        if (ReferenceEquals(dropTargetNode, folder) && (folder is not null || ProjectRootBorder.ReadLocalValue(Border.BackgroundProperty) != DependencyProperty.UnsetValue))
        {
            return;
        }

        ClearDropTarget();
        dropTargetNode = folder;
        if (folder is null)
        {
            ProjectRootBorder.SetResourceReference(Border.BackgroundProperty, "AccentSelectionBrush");
            ProjectRootBorder.SetResourceReference(Border.BorderBrushProperty, "AccentBrush");
            ProjectRootBorder.BorderThickness = new Thickness(1);
        }
        else
        {
            folder.IsDropTarget = true;
        }
    }

    private void ClearDropTarget()
    {
        if (dropTargetNode is not null)
        {
            dropTargetNode.IsDropTarget = false;
            dropTargetNode = null;
        }

        ProjectRootBorder.ClearValue(Border.BackgroundProperty);
        ProjectRootBorder.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
        ProjectRootBorder.BorderThickness = new Thickness(0, 0, 0, 1);
    }

    private void DropSurface_DragLeave(object sender, DragEventArgs e)
    {
        if (sender is not FrameworkElement element || !element.IsMouseOver)
        {
            ClearDropTarget();
        }
    }

    private static TreeViewItem? FindTreeViewItem(ItemsControl parent, EditorNodeViewModel node)
    {
        foreach (object item in parent.Items)
        {
            if (parent.ItemContainerGenerator.ContainerFromItem(item) is not TreeViewItem container)
            {
                continue;
            }

            if (ReferenceEquals(container.DataContext, node))
            {
                return container;
            }

            TreeViewItem? descendant = FindTreeViewItem(container, node);
            if (descendant is not null)
            {
                return descendant;
            }
        }

        return null;
    }

    private static bool IsInsideNodeLabel(DependencyObject? source)
    {
        for (DependencyObject? current = source; current is not null; current = GetVisualOrContentParent(current))
        {
            if (current is FrameworkElement { Tag: "NodeLabel" })
            {
                return true;
            }

            if (current is TreeViewItem)
            {
                return false;
            }
        }

        return false;
    }

    private void RenameTimer_Tick(object? sender, EventArgs e)
    {
        renameTimer.Stop();
        EditorNodeViewModel? node = pendingRenameNode;
        pendingRenameNode = null;
        if (node is not null && ReferenceEquals(node, ViewModel.SelectedNode))
        {
            node.BeginRename();
        }
    }

    private void CancelPendingMouseRename()
    {
        renameTimer.Stop();
        pendingRenameNode = null;
    }

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

    private void InlineName_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (variableMenuOpen && ReferenceEquals(variableInsertionTarget, sender))
        {
            return;
        }

        CommitInlineRename(sender as TextBox);
    }

    private void InlineName_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox textBox || textBox.DataContext is not EditorNodeViewModel node)
        {
            return;
        }

        if (e.Key == Key.Space && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            ShowVariableMenu(textBox, textBox);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
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

    private void CommitInlineRename(TextBox? textBox)
    {
        if (textBox?.DataContext is not EditorNodeViewModel { IsEditing: true } node)
        {
            return;
        }

        node.Name = textBox.Text;
        node.EndRename();
        ViewModel.NormalizeStructureOrder();
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

    private async void AttachFile_Click(object sender, RoutedEventArgs e)
    {
        OpenFileDialog dialog = CreateAttachmentDialog("Attach file to template");
        if (dialog.ShowDialog(this) == true)
        {
            await ViewModel.AttachFileAsync(dialog.FileName).ConfigureAwait(true);
        }
    }

    private async void ReplaceAttachedFile_Click(object sender, RoutedEventArgs e)
    {
        OpenFileDialog dialog = CreateAttachmentDialog("Replace attached file contents");
        if (dialog.ShowDialog(this) == true)
        {
            await ViewModel.ReplaceSelectedAssetAsync(dialog.FileName).ConfigureAwait(true);
        }
    }

    private static OpenFileDialog CreateAttachmentDialog(string title) => new()
    {
        Title = title,
        Filter = "All files (*.*)|*.*",
        CheckFileExists = true,
        Multiselect = false,
    };

    private void InsertVariable_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button)
        {
            ShowVariableMenu(TextContentEditor, button);
        }
    }

    private void ShowVariableMenu(TextBox target, UIElement placementTarget)
    {
        variableInsertionTarget = target;
        variableMenuOpen = true;
        variableWasInserted = false;
        ContextMenu menu = new()
        {
            Placement = PlacementMode.Bottom,
            PlacementTarget = placementTarget,
        };
        foreach (VariableOption variable in ViewModel.VariableChoices)
        {
            MenuItem item = new()
            {
                Header = $"{variable.Label}    {variable.Token}",
                ToolTip = variable.Meaning,
                Tag = variable,
            };
            item.Click += VariableMenuItem_Click;
            menu.Items.Add(item);
        }

        menu.Closed += (_, _) => VariableMenu_Closed(target);
        menu.IsOpen = true;
    }

    private void VariableMenu_Closed(TextBox target)
    {
        bool restoreEditingFocus = variableWasInserted;
        variableMenuOpen = false;
        variableWasInserted = false;
        variableInsertionTarget = null;
        if (restoreEditingFocus)
        {
            _ = target.Dispatcher.BeginInvoke(() =>
            {
                target.Focus();
                target.CaretIndex = Math.Clamp(target.CaretIndex, 0, target.Text.Length);
            }, DispatcherPriority.Input);
        }
        else if (target.DataContext is EditorNodeViewModel { IsEditing: true })
        {
            CommitInlineRename(target);
        }
    }

    private void VariableMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: VariableOption variable } || variableInsertionTarget is not TextBox target)
        {
            return;
        }

        int insertionStart = target.SelectionStart;
        target.SelectedText = variable.Token;
        target.CaretIndex = insertionStart + variable.Token.Length;
        target.SelectionLength = 0;
        variableWasInserted = true;
    }

    private void TestTemplate_Click(object sender, RoutedEventArgs e)
    {
        ResolvedPreviewWindow preview = new(ViewModel)
        {
            Owner = this,
        };
        _ = preview.ShowDialog();
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

        ViewModel.DiscardAllStaging();
        closeApproved = true;
        Close();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F2 && ViewModel.RenameNodeCommand.CanExecute(null))
        {
            CancelPendingMouseRename();
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

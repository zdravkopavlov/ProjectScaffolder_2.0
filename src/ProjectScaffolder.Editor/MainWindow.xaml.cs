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

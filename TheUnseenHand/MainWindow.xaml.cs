using System.Windows;
using System.Windows.Input;
using System.ComponentModel;
using TheUnseenHand.ViewModels;
using TheUnseenHand.Views;

namespace TheUnseenHand;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        DataContext = new MainViewModel();
    }

    private void DragArea_MouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void CloseButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        Close();
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
            viewModel.Stop();
    }

    private void AddActionButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel)
            return;

        var editor = new ActionEditorWindow
        {
            Owner = this
        };

        if (editor.ShowDialog() == true && editor.Result is not null)
        {
            viewModel.Actions.Add(editor.Result);
        }
    }

    private void EditActionButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel ||
            viewModel.SelectedAction is null)
        {
            return;
        }

        var selectedAction = viewModel.SelectedAction;
        var editor = new ActionEditorWindow(selectedAction)
        {
            Owner = this
        };

        if (editor.ShowDialog() == true && editor.Result is not null)
        {
            selectedAction.Type = editor.Result.Type;
            selectedAction.Value = editor.Result.Value;
            selectedAction.DurationMilliseconds = editor.Result.DurationMilliseconds;
            selectedAction.Condition = editor.Result.Condition;
            selectedAction.Actions = editor.Result.Actions;
            selectedAction.ElseActions = editor.Result.ElseActions;
        }
    }

    private void MoveActionUpButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel { SelectedAction: not null } viewModel)
            return;

        int index = viewModel.Actions.IndexOf(viewModel.SelectedAction);
        if (index > 0)
            viewModel.Actions.Move(index, index - 1);
    }

    private void MoveActionDownButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel { SelectedAction: not null } viewModel)
            return;

        int index = viewModel.Actions.IndexOf(viewModel.SelectedAction);
        if (index >= 0 && index < viewModel.Actions.Count - 1)
            viewModel.Actions.Move(index, index + 1);
    }
}

using System.Windows;
using System.Windows.Input;
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
        }
    }
}

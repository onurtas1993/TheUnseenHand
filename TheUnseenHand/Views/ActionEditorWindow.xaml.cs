using System.Windows;
using System.Windows.Input;
using TheUnseenHand.Models;
using TheUnseenHand.ViewModels;

namespace TheUnseenHand.Views;

public partial class ActionEditorWindow : Window
{
    private readonly ActionEditorViewModel _viewModel;

    public MacroAction? Result { get; private set; }

    public ActionEditorWindow(MacroAction? action = null, bool allowIf = true)
    {
        InitializeComponent();

        _viewModel = new ActionEditorViewModel(action, allowIf);
        DataContext = _viewModel;
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        Result = _viewModel.CreateAction();
        DialogResult = true;
    }

    private void DragArea_MouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
            DragMove();
    }

}

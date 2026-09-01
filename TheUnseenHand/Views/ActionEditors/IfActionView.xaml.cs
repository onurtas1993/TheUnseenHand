using System.Windows;
using System.Windows.Controls;
using TheUnseenHand.Models;
using TheUnseenHand.ViewModels.ActionEditors;

namespace TheUnseenHand.Views.ActionEditors;

public partial class IfActionView : UserControl
{
    public IfActionView()
    {
        InitializeComponent();
    }

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not IfActionViewModel viewModel)
            return;

        var editor = new ActionEditorWindow(allowIf: false)
        {
            Owner = Window.GetWindow(this)
        };

        if (editor.ShowDialog() == true && editor.Result is not null)
            viewModel.Actions.Add(editor.Result);
    }

    private void EditButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not IfActionViewModel viewModel ||
            viewModel.SelectedAction is null)
        {
            return;
        }

        MacroAction selected = viewModel.SelectedAction;
        var editor = new ActionEditorWindow(selected, allowIf: false)
        {
            Owner = Window.GetWindow(this)
        };

        if (editor.ShowDialog() == true && editor.Result is not null)
        {
            int index = viewModel.Actions.IndexOf(selected);
            viewModel.Actions[index] = editor.Result;
        }
    }

    private void RemoveButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is IfActionViewModel { SelectedAction: not null } viewModel)
            viewModel.Actions.Remove(viewModel.SelectedAction);
    }

    private void MoveUpButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not IfActionViewModel { SelectedAction: not null } viewModel)
            return;

        int index = viewModel.Actions.IndexOf(viewModel.SelectedAction);
        if (index > 0)
            viewModel.Actions.Move(index, index - 1);
    }

    private void MoveDownButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not IfActionViewModel { SelectedAction: not null } viewModel)
            return;

        int index = viewModel.Actions.IndexOf(viewModel.SelectedAction);
        if (index >= 0 && index < viewModel.Actions.Count - 1)
            viewModel.Actions.Move(index, index + 1);
    }

    private void AddElseButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not IfActionViewModel viewModel)
            return;

        var editor = new ActionEditorWindow(allowIf: false)
        {
            Owner = Window.GetWindow(this)
        };

        if (editor.ShowDialog() == true && editor.Result is not null)
            viewModel.ElseActions.Add(editor.Result);
    }

    private void EditElseButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not IfActionViewModel { SelectedElseAction: not null } viewModel)
            return;

        MacroAction selected = viewModel.SelectedElseAction;
        var editor = new ActionEditorWindow(selected, allowIf: false)
        {
            Owner = Window.GetWindow(this)
        };

        if (editor.ShowDialog() == true && editor.Result is not null)
        {
            int index = viewModel.ElseActions.IndexOf(selected);
            viewModel.ElseActions[index] = editor.Result;
        }
    }

    private void RemoveElseButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is IfActionViewModel { SelectedElseAction: not null } viewModel)
            viewModel.ElseActions.Remove(viewModel.SelectedElseAction);
    }

    private void MoveElseUpButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not IfActionViewModel { SelectedElseAction: not null } viewModel)
            return;

        int index = viewModel.ElseActions.IndexOf(viewModel.SelectedElseAction);
        if (index > 0)
            viewModel.ElseActions.Move(index, index - 1);
    }

    private void MoveElseDownButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not IfActionViewModel { SelectedElseAction: not null } viewModel)
            return;

        int index = viewModel.ElseActions.IndexOf(viewModel.SelectedElseAction);
        if (index >= 0 && index < viewModel.ElseActions.Count - 1)
            viewModel.ElseActions.Move(index, index + 1);
    }
}

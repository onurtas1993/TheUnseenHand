using System.ComponentModel;
using System.Runtime.CompilerServices;
using TheUnseenHand.Models;
using TheUnseenHand.ViewModels.ActionEditors;

namespace TheUnseenHand.ViewModels;

public class ActionEditorViewModel : INotifyPropertyChanged
{
    private MacroActionType _selectedActionType;
    private object _currentEditor = null!;

    public Array ActionTypes { get; } = Enum.GetValues<MacroActionType>();

    public MacroActionType SelectedActionType
    {
        get => _selectedActionType;
        set
        {
            if (_selectedActionType == value && _currentEditor is not null)
                return;

            _selectedActionType = value;
            OnPropertyChanged();
            CurrentEditor = CreateEditor(value);
        }
    }

    public object CurrentEditor
    {
        get => _currentEditor;
        private set
        {
            _currentEditor = value;
            OnPropertyChanged();
        }
    }

    public ActionEditorViewModel(MacroAction? action = null)
    {
        _selectedActionType = action?.Type ?? MacroActionType.Press;
        CurrentEditor = CreateEditor(_selectedActionType, action?.Value);
    }

    public MacroAction CreateAction()
    {
        string value = CurrentEditor switch
        {
            PressActionViewModel press => press.Key,
            WaitActionViewModel wait => wait.Milliseconds.ToString(),
            _ => string.Empty
        };

        return new MacroAction
        {
            Type = SelectedActionType,
            Value = value
        };
    }

    private static object CreateEditor(MacroActionType type, string? value = null)
    {
        return type switch
        {
            MacroActionType.Press => new PressActionViewModel
            {
                Key = value ?? string.Empty
            },
            MacroActionType.Wait => new WaitActionViewModel
            {
                Milliseconds = int.TryParse(value, out int milliseconds)
                    ? milliseconds
                    : 500
            },
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

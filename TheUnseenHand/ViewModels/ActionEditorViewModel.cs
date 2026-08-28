using System.ComponentModel;
using System.Runtime.CompilerServices;
using TheUnseenHand.Models;
using TheUnseenHand.ViewModels.ActionEditors;

namespace TheUnseenHand.ViewModels;

public class ActionEditorViewModel : INotifyPropertyChanged
{
    private MacroActionType _selectedActionType;
    private object _currentEditor = null!;

    public IReadOnlyList<MacroActionType> ActionTypes { get; }

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

    public ActionEditorViewModel(MacroAction? action = null, bool allowIf = true)
    {
        ActionTypes = allowIf
            ? Enum.GetValues<MacroActionType>()
            : new[] { MacroActionType.Press, MacroActionType.Wait };
        _selectedActionType = action?.Type ?? MacroActionType.Press;
        CurrentEditor = CreateEditor(_selectedActionType, action);
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
            Value = value,
            Condition = CurrentEditor is IfActionViewModel conditional
                ? new MacroCondition
                {
                    Source = conditional.Source,
                    Operator = conditional.Operator,
                    Value = conditional.ComparisonValue.Trim()
                }
                : null,
            Actions = CurrentEditor is IfActionViewModel ifEditor
                ? ifEditor.Actions.ToList()
                : new List<MacroAction>()
        };
    }

    private static object CreateEditor(MacroActionType type, MacroAction? action = null)
    {
        return type switch
        {
            MacroActionType.Press => new PressActionViewModel
            {
                Key = action?.Value ?? string.Empty
            },
            MacroActionType.Wait => new WaitActionViewModel
            {
                Milliseconds = int.TryParse(action?.Value, out int milliseconds)
                    ? milliseconds
                    : 500
            },
            MacroActionType.If => CreateIfEditor(action),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
    }

    private static IfActionViewModel CreateIfEditor(MacroAction? action)
    {
        var editor = new IfActionViewModel
        {
            Source = action?.Condition?.Source ?? ConditionSource.PlayerHP,
            Operator = action?.Condition?.Operator ?? ComparisonOperator.LessThan,
            ComparisonValue = action?.Condition?.Value ?? string.Empty
        };

        if (action is not null)
        {
            foreach (MacroAction child in action.Actions)
                editor.Actions.Add(child);
        }

        return editor;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

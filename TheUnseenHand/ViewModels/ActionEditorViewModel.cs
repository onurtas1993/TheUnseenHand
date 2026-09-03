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
        if (CurrentEditor is PressActionViewModel pressEditor)
        {
            if (string.IsNullOrWhiteSpace(pressEditor.Key))
                throw new InvalidOperationException("Enter a key to press.");

            if (pressEditor.DurationMilliseconds is < 1 or > 60_000)
            {
                throw new InvalidOperationException(
                    "Key duration must be between 1 and 60000 milliseconds.");
            }
        }

        if (CurrentEditor is IfActionViewModel conditionEditor &&
            string.IsNullOrWhiteSpace(conditionEditor.Source))
            throw new InvalidOperationException("Enter a game-capture output name.");

        if (CurrentEditor is IfActionViewModel intervalEditor &&
            intervalEditor.CheckIntervalMilliseconds < 0)
        {
            throw new InvalidOperationException("Check interval cannot be negative.");
        }

        string value = CurrentEditor switch
        {
            PressActionViewModel pressAction => pressAction.Key,
            WaitActionViewModel wait => wait.Milliseconds.ToString(),
            _ => string.Empty
        };

        return new MacroAction
        {
            Type = SelectedActionType,
            Value = value,
            DurationMilliseconds = CurrentEditor is PressActionViewModel durationEditor
                ? durationEditor.DurationMilliseconds
                : 0,
            CheckIntervalMilliseconds = CurrentEditor is IfActionViewModel checkIntervalEditor
                ? checkIntervalEditor.CheckIntervalMilliseconds
                : 0,
            Condition = CurrentEditor is IfActionViewModel conditional
                ? new MacroCondition
                {
                    Source = conditional.Source.Trim(),
                    Operator = conditional.Operator,
                    Value = conditional.ComparisonValue.Trim()
                }
                : null,
            Actions = CurrentEditor is IfActionViewModel ifEditor
                ? ifEditor.Actions.ToList()
                : new List<MacroAction>(),
            ElseActions = CurrentEditor is IfActionViewModel elseEditor
                ? elseEditor.ElseActions.ToList()
                : new List<MacroAction>()
        };
    }

    private static object CreateEditor(MacroActionType type, MacroAction? action = null)
    {
        return type switch
        {
            MacroActionType.Press => new PressActionViewModel
            {
                Key = action?.Value ?? string.Empty,
                DurationMilliseconds = action?.DurationMilliseconds > 0
                    ? action.DurationMilliseconds
                    : 75
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
            Source = action?.Condition?.Source ?? string.Empty,
            Operator = action?.Condition?.Operator ?? ComparisonOperator.LessThan,
            ComparisonValue = action?.Condition?.Value ?? string.Empty,
            CheckIntervalMilliseconds = action?.CheckIntervalMilliseconds ?? 0
        };

        if (action is not null)
        {
            foreach (MacroAction child in action.Actions)
                editor.Actions.Add(child);

            foreach (MacroAction child in action.ElseActions)
                editor.ElseActions.Add(child);
        }

        return editor;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

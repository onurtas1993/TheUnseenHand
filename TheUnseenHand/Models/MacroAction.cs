using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace TheUnseenHand.Models;

public class MacroAction : INotifyPropertyChanged
{
    private MacroActionType _type;
    private string _value = string.Empty;
    private int _durationMilliseconds;
    private MacroCondition? _condition;
    private List<MacroAction> _actions = new();
    private List<MacroAction> _elseActions = new();

    public MacroActionType Type
    {
        get => _type;
        set
        {
            if (_type == value)
                return;

            _type = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayText));
        }
    }

    public string Value
    {
        get => _value;
        set
        {
            if (_value == value)
                return;

            _value = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayText));
        }
    }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int DurationMilliseconds
    {
        get => _durationMilliseconds;
        set
        {
            if (_durationMilliseconds == value)
                return;

            _durationMilliseconds = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayText));
        }
    }

    public MacroCondition? Condition
    {
        get => _condition;
        set
        {
            _condition = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayText));
        }
    }

    public List<MacroAction> Actions
    {
        get => _actions;
        set
        {
            _actions = value ?? new List<MacroAction>();
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayText));
        }
    }

    public List<MacroAction> ElseActions
    {
        get => _elseActions;
        set
        {
            _elseActions = value ?? new List<MacroAction>();
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayText));
        }
    }

    [JsonIgnore]
    public string DisplayText =>
        Type switch
        {
            MacroActionType.Press when DurationMilliseconds > 75 =>
                $"HOLD {Value} FOR {FormatDuration(DurationMilliseconds)}",
            MacroActionType.Press => $"PRESS {Value}",
            MacroActionType.Wait => $"WAIT {Value} MS",
            MacroActionType.If when Condition is not null =>
                $"IF {FormatSource(Condition.Source)} {FormatOperator(Condition.Operator)} {Condition.Value} " +
                $"THEN ({Actions.Count}) ELSE ({ElseActions.Count})",
            _ => $"{Type} {Value}"
        };

    private static string FormatDuration(int milliseconds) =>
        milliseconds % 1000 == 0
            ? $"{milliseconds / 1000} S"
            : $"{milliseconds} MS";

    private static string FormatSource(ConditionSource source) => source switch
    {
        ConditionSource.PlayerHP => "HP",
        ConditionSource.PlayerMaxHP => "MAX HP",
        ConditionSource.PlayerHPPercent => "HP %",
        ConditionSource.PlayerMP => "MP",
        ConditionSource.PlayerMaxMP => "MAX MP",
        ConditionSource.PlayerMPPercent => "MP %",
        ConditionSource.CurrentMob => "MOB NAME",
        _ => source.ToString().ToUpperInvariant()
    };

    private static string FormatOperator(ComparisonOperator comparison) => comparison switch
    {
        ComparisonOperator.Equals => "=",
        ComparisonOperator.NotEquals => "!=",
        ComparisonOperator.LessThan => "<",
        ComparisonOperator.LessThanOrEqual => "<=",
        ComparisonOperator.GreaterThan => ">",
        ComparisonOperator.GreaterThanOrEqual => ">=",
        _ => comparison.ToString()
    };

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(
        [CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(propertyName));
    }
}

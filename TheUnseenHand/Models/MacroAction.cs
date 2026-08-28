using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace TheUnseenHand.Models;

public class MacroAction : INotifyPropertyChanged
{
    private MacroActionType _type;
    private string _value = string.Empty;
    private MacroCondition? _condition;
    private List<MacroAction> _actions = new();

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

    [JsonIgnore]
    public string DisplayText =>
        Type switch
        {
            MacroActionType.Press => $"PRESS {Value}",
            MacroActionType.Wait => $"WAIT {Value} MS",
            MacroActionType.If when Condition is not null =>
                $"IF {FormatSource(Condition.Source)} {FormatOperator(Condition.Operator)} {Condition.Value} THEN ({Actions.Count} ACTIONS)",
            _ => $"{Type} {Value}"
        };

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

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using TheUnseenHand.Models;

namespace TheUnseenHand.ViewModels.ActionEditors;

public sealed class IfActionViewModel : INotifyPropertyChanged
{
    private ConditionSource _source = ConditionSource.PlayerHP;
    private ComparisonOperator _operator = ComparisonOperator.LessThan;
    private string _comparisonValue = string.Empty;
    private MacroAction? _selectedAction;

    public Array Sources { get; } = Enum.GetValues<ConditionSource>();

    public IReadOnlyList<ComparisonOperator> Operators =>
        Source == ConditionSource.CurrentMob
            ? new[] { ComparisonOperator.Equals, ComparisonOperator.NotEquals }
            : Enum.GetValues<ComparisonOperator>();

    public ConditionSource Source
    {
        get => _source;
        set
        {
            if (_source == value)
                return;

            _source = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Operators));

            if (!Operators.Contains(Operator))
                Operator = ComparisonOperator.Equals;
        }
    }

    public ComparisonOperator Operator
    {
        get => _operator;
        set
        {
            _operator = value;
            OnPropertyChanged();
        }
    }

    public string ComparisonValue
    {
        get => _comparisonValue;
        set
        {
            _comparisonValue = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<MacroAction> Actions { get; } = new();

    public MacroAction? SelectedAction
    {
        get => _selectedAction;
        set
        {
            _selectedAction = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

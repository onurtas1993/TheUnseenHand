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
    private string _newMobName = string.Empty;
    private string? _selectedMobName;
    private MacroAction? _selectedAction;
    private MacroAction? _selectedElseAction;

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
            OnPropertyChanged(nameof(IsMobCondition));

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

    public bool IsMobCondition => Source == ConditionSource.CurrentMob;

    public string NewMobName
    {
        get => _newMobName;
        set
        {
            _newMobName = value;
            OnPropertyChanged();
        }
    }

    public string? SelectedMobName
    {
        get => _selectedMobName;
        set
        {
            _selectedMobName = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<string> MobNames { get; } = new();

    public ObservableCollection<MacroAction> Actions { get; } = new();
    public ObservableCollection<MacroAction> ElseActions { get; } = new();

    public MacroAction? SelectedAction
    {
        get => _selectedAction;
        set
        {
            _selectedAction = value;
            OnPropertyChanged();
        }
    }

    public MacroAction? SelectedElseAction
    {
        get => _selectedElseAction;
        set
        {
            _selectedElseAction = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

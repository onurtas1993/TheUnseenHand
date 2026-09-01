using System.ComponentModel;
using System.Runtime.CompilerServices;
using TheUnseenHand.Models;

namespace TheUnseenHand.ViewModels.ActionEditors;

public sealed class IfActionViewModel : INotifyPropertyChanged
{
    private string _source = string.Empty;
    private ComparisonOperator _operator = ComparisonOperator.LessThan;
    private string _comparisonValue = string.Empty;
    private int _checkIntervalMilliseconds;
    private MacroAction? _selectedAction;
    private MacroAction? _selectedElseAction;

    public IReadOnlyList<ComparisonOperator> Operators { get; } = Enum.GetValues<ComparisonOperator>();

    public string Source
    {
        get => _source;
        set
        {
            if (_source == value)
                return;

            _source = value;
            OnPropertyChanged();
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

    public int CheckIntervalMilliseconds
    {
        get => _checkIntervalMilliseconds;
        set
        {
            _checkIntervalMilliseconds = value;
            OnPropertyChanged();
        }
    }

    public System.Collections.ObjectModel.ObservableCollection<MacroAction> Actions { get; } = new();
    public System.Collections.ObjectModel.ObservableCollection<MacroAction> ElseActions { get; } = new();

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

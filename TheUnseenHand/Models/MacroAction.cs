using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace TheUnseenHand.Models;

public class MacroAction : INotifyPropertyChanged
{
    private MacroActionType _type;
    private string _value = string.Empty;

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

    [JsonIgnore]
    public string DisplayText =>
        Type switch
        {
            MacroActionType.Press => $"PRESS {Value}",
            MacroActionType.Wait => $"WAIT {Value} MS",
            _ => $"{Type} {Value}"
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

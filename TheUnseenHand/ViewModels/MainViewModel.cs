using System.Collections.ObjectModel;
using Microsoft.Win32;
using System.IO;
using System.ComponentModel;
using System.Collections.Specialized;
using System.Runtime.CompilerServices;
using Input.Abstractions;
using TheUnseenHand.Infrastructure;
using TheUnseenHand.Models;
using TheUnseenHand.Services;

namespace TheUnseenHand.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private static readonly string SettingsDirectory = AppContext.BaseDirectory;

    private static readonly string DefaultSettingsPath =
        Path.Combine(SettingsDirectory, "macro-settings.json");

    private static readonly string InputSettingsPath =
        Path.Combine(SettingsDirectory, "input.json");

    private readonly IMacroExecutionService _macroExecutionService;
    private readonly IMacroJsonService _macroJsonService;
    private readonly HashSet<MacroAction> _subscribedActions = new();
    private CancellationTokenSource? _executionCancellation;
    private string _targetProcessName = "notepad.exe";
    private KeyboardProvider _keyboardProvider = KeyboardProvider.Windows;
    private bool _isLoadingSettings = true;
    private MacroAction? _selectedAction;
    private MacroTreeNode? _selectedTreeNode;

    public ObservableCollection<MacroAction> Actions { get; } = new();
    public ObservableCollection<MacroTreeNode> MacroTree { get; } = new();
    public ObservableCollection<GameCaptureValueItem> GameCaptureValues { get; } = new();

    public MacroAction? SelectedAction
    {
        get => _selectedAction;
        set
        {
            if (ReferenceEquals(_selectedAction, value))
                return;

            _selectedAction = value;
            OnPropertyChanged();
        }
    }

    public MacroTreeNode? SelectedTreeNode
    {
        get => _selectedTreeNode;
        set
        {
            if (ReferenceEquals(_selectedTreeNode, value))
                return;

            _selectedTreeNode = value;
            SelectedAction = value?.Action;
            OnPropertyChanged();
        }
    }

    public RelayCommand RemoveCommand { get; }
    public RelayCommand StartCommand { get; }
    public RelayCommand StopCommand { get; }
    public RelayCommand LoadCommand { get; }
    public RelayCommand SaveCommand { get; }

    public MainViewModel(
        IMacroExecutionService? macroExecutionService = null,
        IMacroJsonService? macroJsonService = null)
    {
        _macroExecutionService = macroExecutionService ?? new MacroExecutionService();
        _macroExecutionService.GameStateRead += MacroExecutionService_GameStateRead;
        _macroJsonService = macroJsonService ?? new MacroJsonService();
        Actions.CollectionChanged += Actions_CollectionChanged;
        RemoveCommand = new RelayCommand(Remove);
        StartCommand = new RelayCommand(
            _ => _ = StartAsync(),
            _ => _executionCancellation is null && !_isLoadingSettings);
        StopCommand = new RelayCommand(_ => Stop(), _ => _executionCancellation is not null);
        LoadCommand = new RelayCommand(
            _ => _ = LoadAsync(),
            _ => _executionCancellation is null && !_isLoadingSettings);
        SaveCommand = new RelayCommand(_ => _ = SaveAsync(), _ => !_isLoadingSettings);

        _ = LoadDefaultAsync();
    }

    private void MacroExecutionService_GameStateRead(
        object? sender,
        GameStateReadEventArgs e)
    {
        void Apply()
        {
            GameCaptureValueItem? item = GameCaptureValues.FirstOrDefault(value =>
                string.Equals(value.Source, e.Source, StringComparison.OrdinalIgnoreCase));
            if (item is not null)
                item.Value = e.ActualValue;
        }

        if (System.Windows.Application.Current.Dispatcher.CheckAccess())
            Apply();
        else
            System.Windows.Application.Current.Dispatcher.Invoke(Apply);
    }

    private void Actions_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (MacroAction action in _subscribedActions)
            action.PropertyChanged -= Action_PropertyChanged;

        _subscribedActions.Clear();
        foreach (MacroAction action in Actions)
        {
            action.PropertyChanged += Action_PropertyChanged;
            _subscribedActions.Add(action);
        }

        RefreshGameCaptureValues();
        RefreshMacroTree();
    }

    private void Action_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        RefreshGameCaptureValues();
    }

    public void RefreshGameCaptureValues()
    {
        var previousValues = GameCaptureValues.ToDictionary(
            item => item.Source,
            item => item.Value,
            StringComparer.OrdinalIgnoreCase);
        var sources = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        CollectConditionSources(Actions, sources, seen);

        GameCaptureValues.Clear();
        foreach (string source in sources)
        {
            GameCaptureValues.Add(new GameCaptureValueItem(
                source,
                previousValues.GetValueOrDefault(source, "--")));
        }
    }

    public void RefreshMacroTree()
    {
        MacroAction? selectedAction = SelectedTreeNode?.Action;
        MacroTree.Clear();

        foreach (MacroAction action in Actions)
            MacroTree.Add(CreateActionNode(action, Actions));

        SelectedTreeNode = FindActionNode(MacroTree, selectedAction);
    }

    private static MacroTreeNode CreateActionNode(
        MacroAction action,
        IList<MacroAction> containingActions)
    {
        var node = new MacroTreeNode(action.DisplayText, action, containingActions);
        if (action.Type != MacroActionType.If)
            return node;

        var thenNode = new MacroTreeNode("THEN");
        foreach (MacroAction child in action.Actions)
            thenNode.Children.Add(CreateActionNode(child, action.Actions));

        var elseNode = new MacroTreeNode("ELSE");
        foreach (MacroAction child in action.ElseActions)
            elseNode.Children.Add(CreateActionNode(child, action.ElseActions));

        node.Children.Add(thenNode);
        node.Children.Add(elseNode);
        return node;
    }

    private static MacroTreeNode? FindActionNode(
        IEnumerable<MacroTreeNode> nodes,
        MacroAction? action)
    {
        if (action is null)
            return null;

        foreach (MacroTreeNode node in nodes)
        {
            if (ReferenceEquals(node.Action, action))
                return node;

            MacroTreeNode? match = FindActionNode(node.Children, action);
            if (match is not null)
                return match;
        }

        return null;
    }

    private static void CollectConditionSources(
        IEnumerable<MacroAction> actions,
        ICollection<string> sources,
        ISet<string> seen)
    {
        foreach (MacroAction action in actions)
        {
            if (action.Type == MacroActionType.If &&
                !string.IsNullOrWhiteSpace(action.Condition?.Source) &&
                seen.Add(action.Condition.Source))
            {
                sources.Add(action.Condition.Source);
            }

            CollectConditionSources(action.Actions, sources, seen);
            CollectConditionSources(action.ElseActions, sources, seen);
        }
    }

    private async Task StartAsync()
    {
        if (_executionCancellation is not null)
            return;

        _executionCancellation = new CancellationTokenSource();
        RefreshExecutionCommands();

        try
        {
            await _macroExecutionService.ExecuteWhileForegroundAsync(
                Actions,
                _targetProcessName,
                _keyboardProvider,
                _executionCancellation.Token);
        }
        catch (OperationCanceledException)
        {
            // Stop is an expected end to the macro loop.
        }
        catch (Exception exception)
        {
            System.Windows.MessageBox.Show(
                exception.Message,
                "The Unseen Hand",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
        finally
        {
            _executionCancellation.Dispose();
            _executionCancellation = null;
            RefreshExecutionCommands();
        }
    }

    public void Stop()
    {
        _executionCancellation?.Cancel();
        _macroExecutionService.ResetIntervalHistory();
    }

    private void RefreshExecutionCommands()
    {
        StartCommand.RaiseCanExecuteChanged();
        StopCommand.RaiseCanExecuteChanged();
        LoadCommand.RaiseCanExecuteChanged();
        SaveCommand.RaiseCanExecuteChanged();
    }

    private async Task LoadDefaultAsync()
    {
        try
        {
            if (!File.Exists(DefaultSettingsPath))
                throw new FileNotFoundException(
                    $"macro-settings.json was not found next to the application:\n{SettingsDirectory}");

            AppSettings settings = await _macroJsonService.LoadAsync(DefaultSettingsPath);
            ApplySettings(settings);
            _keyboardProvider = InputSettings.Load(InputSettingsPath).KeyboardProvider;
        }
        catch (Exception exception)
        {
            ShowSettingsError("Could not load default settings", exception);
        }
        finally
        {
            _isLoadingSettings = false;
            RefreshExecutionCommands();
        }
    }

    private async Task SaveAsync()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Save macro settings",
            Filter = "JSON settings (*.json)|*.json|All files (*.*)|*.*",
            DefaultExt = ".json",
            AddExtension = true,
            InitialDirectory = SettingsDirectory,
            FileName = "macro-settings.json"
        };

        if (dialog.ShowDialog() != true)
            return;

        try
        {
            var settings = new AppSettings
            {
                Target = new TargetSettings
                {
                    ProcessName = _targetProcessName
                },
                Macro = new MacroSettings { Actions = Actions.ToList() }
            };

            await _macroJsonService.SaveAsync(dialog.FileName, settings);
        }
        catch (Exception exception)
        {
            ShowSettingsError("Could not save settings", exception);
        }
    }

    private async Task LoadAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Load macro settings",
            Filter = "JSON settings (*.json)|*.json|All files (*.*)|*.*",
            DefaultExt = ".json",
            InitialDirectory = SettingsDirectory,
            CheckFileExists = true
        };

        if (dialog.ShowDialog() != true)
            return;

        try
        {
            AppSettings settings = await _macroJsonService.LoadAsync(dialog.FileName);
            ApplySettings(settings);
        }
        catch (Exception exception)
        {
            ShowSettingsError("Could not load settings", exception);
        }
    }

    private void ApplySettings(AppSettings settings)
    {
        Actions.Clear();
        foreach (MacroAction action in settings.Macro.Actions)
            Actions.Add(action);

        _targetProcessName = string.IsNullOrWhiteSpace(settings.Target.ProcessName)
            ? "notepad.exe"
            : settings.Target.ProcessName;
    }

    private static void ShowSettingsError(string title, Exception exception)
    {
        System.Windows.MessageBox.Show(
            exception.Message,
            title,
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Error);
    }

    private void Remove(object? parameter)
    {
        MacroTreeNode? node = SelectedTreeNode;
        if (node?.Action is null || node.ContainingActions is null)
            return;

        node.ContainingActions.Remove(node.Action);
        SelectedTreeNode = null;
        RefreshGameCaptureValues();
        RefreshMacroTree();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed class MacroTreeNode
{
    public MacroTreeNode(
        string displayText,
        MacroAction? action = null,
        IList<MacroAction>? containingActions = null)
    {
        DisplayText = displayText;
        Action = action;
        ContainingActions = containingActions;
    }

    public string DisplayText { get; }
    public MacroAction? Action { get; }
    public IList<MacroAction>? ContainingActions { get; }
    public ObservableCollection<MacroTreeNode> Children { get; } = new();
}

public sealed class GameCaptureValueItem : INotifyPropertyChanged
{
    private string _value;

    public GameCaptureValueItem(string source, string value)
    {
        Source = source;
        _value = value;
    }

    public string Source { get; }

    public string Value
    {
        get => _value;
        set
        {
            if (_value == value)
                return;

            _value = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

using System.Collections.ObjectModel;
using Microsoft.Win32;
using System.IO;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using TheUnseenHand.Infrastructure;
using TheUnseenHand.Models;
using TheUnseenHand.Services;

namespace TheUnseenHand.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private static readonly string SettingsDirectory = AppContext.BaseDirectory;
    private static readonly string DefaultSettingsPath =
        Path.Combine(SettingsDirectory, "macro-settings.json");

    private readonly IMacroExecutionService _macroExecutionService;
    private readonly IMacroJsonService _macroJsonService;
    private CancellationTokenSource? _executionCancellation;
    private string _targetProcessName = "notepad.exe";
    private bool _isLoadingSettings = true;
    private string _latestVitals = "Last GameVision value: --";
    private string _latestNumericCondition = "Last IF: --";

    public ObservableCollection<MacroAction> Actions { get; } = new();

    public MacroAction? SelectedAction { get; set; }

    public string LatestVitals
    {
        get => _latestVitals;
        private set
        {
            _latestVitals = value;
            OnPropertyChanged();
        }
    }

    public string LatestNumericCondition
    {
        get => _latestNumericCondition;
        private set
        {
            _latestNumericCondition = value;
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
            LatestVitals = $"Last GameVision value: {e.Source} = {e.ActualValue}";
            LatestNumericCondition =
                $"Last IF: {e.Comparison} = {e.Result.ToString().ToUpperInvariant()}";
        }

        if (System.Windows.Application.Current.Dispatcher.CheckAccess())
            Apply();
        else
            System.Windows.Application.Current.Dispatcher.Invoke(Apply);
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
                Target = new TargetSettings { ProcessName = _targetProcessName },
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
        if (parameter is MacroAction action)
        {
            Actions.Remove(action);
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

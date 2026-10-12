using System.Windows.Input;

namespace HidGuard.App.Mvvm;

public sealed class RelayCommand : ICommand
{
    private readonly Action execute;
    private readonly Func<bool>? canExecute;

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        this.execute = execute ?? throw new ArgumentNullException(nameof(execute));
        this.canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => canExecute?.Invoke() ?? true;

    public void Execute(object? parameter) => execute();
}

public sealed class RelayCommand<T> : ICommand
{
    private readonly Action<T?> execute;
    private readonly Func<T?, bool>? canExecute;

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
    {
        this.execute = execute ?? throw new ArgumentNullException(nameof(execute));
        this.canExecute = canExecute;
    }

    public bool CanExecute(object? parameter)
    {
        if (canExecute == null)
        {
            return true;
        }

        if (parameter == null && typeof(T).IsValueType)
        {
            return canExecute(default);
        }

        return canExecute((T?)parameter);
    }

    public void Execute(object? parameter)
    {
        if (parameter == null && typeof(T).IsValueType)
        {
            execute(default);
            return;
        }

        execute((T?)parameter);
    }
}

using System;
using System.Windows.Input;

// Generic RelayCommand that implements the ICommand interface
public class RelayCommand<T> : ICommand
{
    private readonly Action<T> _execute;
    private readonly Func<T, bool> _canExecute;

    public RelayCommand(Action<T> execute, Func<T, bool> canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public bool CanExecute(object parameter) => _canExecute == null || _canExecute((T)Convert.ChangeType(parameter, typeof(T)));
    public void Execute(object parameter) => _execute((T)Convert.ChangeType(parameter, typeof(T)));

    public event EventHandler CanExecuteChanged;
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

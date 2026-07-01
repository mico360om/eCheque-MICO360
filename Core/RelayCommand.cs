using System.Windows.Input;

namespace eCheque.MICO360.Core
{
    // ICommand lives in System.ObjectModel and is fully cross-platform (used by Avalonia too).
    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Func<object?, bool>? _canExecute;
        public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
        { _execute = execute; _canExecute = canExecute; }
        public RelayCommand(Action execute, Func<bool>? canExecute = null)
            : this(_ => execute(), canExecute == null ? null : _ => canExecute()) { }
        public event EventHandler? CanExecuteChanged;
        public bool CanExecute(object? p) => _canExecute == null || _canExecute(p);
        public void Execute(object? p) => _execute(p);
        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T?> _execute;
        private readonly Func<T?, bool>? _canExecute;
        public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
        { _execute = execute; _canExecute = canExecute; }
        public event EventHandler? CanExecuteChanged;
        public bool CanExecute(object? p) => _canExecute == null || _canExecute(p is T t ? t : default);
        public void Execute(object? p) => _execute(p is T t ? t : default);
        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}

using System;
using System.Windows.Input;

namespace VideoEditorWPF.Commands
{
    public class RelayCommand : ICommand
    {
        private readonly Action<object> _execute;
        private readonly Func<object, bool> _canExecute;

        public RelayCommand(Action<object> execute, Func<object, bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object parameter) => _canExecute?.Invoke(parameter) ?? true;
        public void Execute(object parameter) => _execute(parameter);
    }
}
// Реализация паттерна Command для WPF-приложений.
// Класс позволяет привязывать действия (Action) к кнопкам, меню и другим UI-элементам.
// Поддерживает проверку возможности выполнения (CanExecute) и автоматическое обновление состояния UI.
// Использует CommandManager для отслеживания изменений и инвалидации CanExecute.

using System;
using System.Windows.Input;

namespace Nyet2Hacker
{
    public class DelegateCommand : ICommand
    {
        private readonly Action execute;
        private bool canExecute;

        public DelegateCommand(bool canExecute, Action execute)
        {
            this.canExecute = canExecute;
            this.execute = execute;
        }

        public event EventHandler CanExecuteChanged;

        public void SetCanExecute(bool can)
        {
            bool changed = can != this.canExecute;
            this.canExecute = can;
            if (changed)
            {
                this.CanExecuteChanged?.Invoke(this, new EventArgs());
            }
        }

        public bool CanExecute(object parameter)
        {
            return this.canExecute;
        }

        public void Execute(object parameter)
        {
            this.execute();
        }
    }
}

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
            if (can != this.canExecute)
            {
                this.CanExecuteChanged?.Invoke(this, new EventArgs());
            }

            this.canExecute = can;
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

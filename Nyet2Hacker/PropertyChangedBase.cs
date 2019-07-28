using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Nyet2Hacker
{
    public abstract class PropertyChangedBase : INotifyPropertyChanged
    {
        private static bool Equal<T>(T first, T second)
        {
            return EqualityComparer<T>.Default.Equals(first, second);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void Notify(
            [CallerMemberName] string propertyName = null)
        {
            var args = new PropertyChangedEventArgs(propertyName);
            this.PropertyChanged?.Invoke(this, args);
        }

        protected void Set<T>(
            ref T field,
            T value,
            [CallerMemberName] string propertyName = default)
        {
            if (!Equal(field, value))
            {
                this.Change(out field, value, propertyName);
            }
        }

        protected void Change<T>(
            out T field,
            T value,
            [CallerMemberName] string propertyName = null)
        {
            field = value;
            this.Notify(propertyName);
        }

        /// <summary>
        /// For when you can't use reference parameters
        /// </summary>
        protected void Set<T>(
            Action<T> setter,
            T value,
            T existing,
            [CallerMemberName] string propertyName = null)
        {
            if (!Equal(value, existing))
            {
                this.Change(setter, value, propertyName);
            }
        }

        protected void Change<T>(
            Action<T> setter,
            T value,
            [CallerMemberName] string propertyName = null)
        {
            setter(value);
            this.Notify(propertyName);
        }

        /// <summary>
        /// Sets and notifies if changed. Returns the old value.
        /// </summary>
        protected T Set<T>(
            T value,
            Action<T> setter,
            Func<T> getter,
            [CallerMemberName] string propertyName = null)
        {
            var existing = getter();
            if (!Equal(value, existing))
            {
                this.Change(value, setter, propertyName);
            }

            return existing;
        }

        protected void Change<T>(
            T value,
            Action<T> setter,
            [CallerMemberName] string propertyName = null)
        {
            setter(value);
            this.Notify(propertyName);
        }
    }
}

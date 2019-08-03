using System;
using System.Windows;
using System.Windows.Controls;

namespace Nyet2Hacker
{
    [TemplateVisualState(GroupName = DirtyGroup, Name = PressedState)]
    [TemplateVisualState(GroupName = DirtyGroup, Name = DirtyHoverState)]
    [TemplateVisualState(GroupName = DirtyGroup, Name = CleanHoverState)]
    [TemplateVisualState(GroupName = DirtyGroup, Name = DirtyState)]
    [TemplateVisualState(GroupName = DirtyGroup, Name = NotDirtyState)]
    public class DirtyButton : Button
    {
        public const string DirtyGroup = "Dirtyness";

        public const string PressedState = "Pressed";
        public const string DirtyHoverState = "DirtyHover";
        public const string CleanHoverState = "CleanHover";
        public const string DirtyState = "Dirty";
        public const string NotDirtyState = "NotDirty";

        public DirtyButton()
        {

        }

        public bool Dirty
        {
            get => (bool)GetValue(DirtyProperty);
            set => SetValue(DirtyProperty, value);
        }

        public static readonly DependencyProperty DirtyProperty =
            DependencyProperty.Register(
                nameof(Dirty),
                typeof(bool),
                typeof(DirtyButton)
            );

        protected override void OnPropertyChanged(
            DependencyPropertyChangedEventArgs e)
        {
            if (e.Property == IsPressedProperty
                || e.Property == IsEnabledProperty
                || e.Property == DirtyProperty
                || e.Property == IsMouseOverProperty)
            {
                this.UpdateDirtyStates();
            }

            base.OnPropertyChanged(e);
        }

        private void UpdateDirtyStates()
        {
            string state;
            if (this.IsPressed)
            {
                state = PressedState;
            }
            else
            {
                if (this.Dirty)
                {
                    if (this.IsMouseOver)
                    {
                        state = DirtyHoverState;
                    }
                    else
                    {
                        state = DirtyState;
                    }
                }
                else
                {
                    if (this.IsMouseOver)
                    {
                        state = CleanHoverState;
                    }
                    else
                    {
                        state = NotDirtyState;
                    }
                }
            }

            VisualStateManager.GoToState(this, state, useTransitions: true);
        }
    }
}

using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Nyet2Hacker
{
    public class PathApplicator : ContentPresenter
    {
        public PathApplicator()
        {
        }

        public Brush Fill
        {
            get => (Brush)GetValue(FillProperty);
            set => SetValue(FillProperty, value);
        }

        public static readonly DependencyProperty FillProperty =
            DependencyProperty.Register(
                nameof(Fill),
                typeof(Brush),
                typeof(PathApplicator)
            );

        public Brush Stroke
        {
            get => (Brush)GetValue(StrokeProperty);
            set => SetValue(StrokeProperty, value);
        }

        public static readonly DependencyProperty StrokeProperty =
            DependencyProperty.Register(
                nameof(Stroke),
                typeof(Brush),
                typeof(PathApplicator)
            );

        private class Bindings
        {
            public Bindings(
                FrameworkElement el,
                DependencyProperty fillProp,
                DependencyProperty strokeProp)
            {
                this.El = el;
                this.FillProp = fillProp;
                this.StrokeProp = strokeProp;
            }

            public FrameworkElement El { get; }
            public DependencyProperty FillProp { get; }
            public DependencyProperty StrokeProp { get; }

            public void Clear()
            {
                if (!(this.FillProp is null))
                {
                    BindingOperations.ClearBinding(this.El, FillProp);
                }

                if (!(this.StrokeProp is null))
                {
                    BindingOperations.ClearBinding(this.El, StrokeProp);
                }
            }
        }

        private void Wrap(Bindings b)
        {
            if (b.FillProp is DependencyProperty fillProp)
            {
                b.El.SetBinding(fillProp, new Binding(nameof(Fill))
                {
                    Source = this,
                    Mode = BindingMode.OneWay
                });
            }

            if (b.StrokeProp is DependencyProperty strokeProp)
            {
                b.El.SetBinding(strokeProp, new Binding(nameof(Stroke))
                {
                    Source = this,
                    Mode = BindingMode.OneWay
                });
            }
        }

        private static bool TryGetBindings(object content, out Bindings b)
        {
            if (!(content is FrameworkElement fe))
            {
                b = default;
                return false;
            }

            DependencyProperty fillProp = null;
            DependencyProperty strokeProp = null;
            switch (content)
            {
                case TextBlock _:
                    fillProp = TextBlock.ForegroundProperty;
                    break;
                case Shape _:
                    fillProp = Shape.FillProperty;
                    strokeProp = Shape.StrokeProperty;
                    break;
            }

            b = new Bindings(fe, fillProp, strokeProp);
            return true;
        }

        protected override void OnPropertyChanged(
            DependencyPropertyChangedEventArgs e)
        {
            if (TryGetBindings(e.OldValue, out var oldBindings))
            {
                oldBindings.Clear();
            }

            if (TryGetBindings(e.NewValue, out var newBindings))
            {
                this.Wrap(newBindings);
            }

            base.OnPropertyChanged(e);
        }
    }
}

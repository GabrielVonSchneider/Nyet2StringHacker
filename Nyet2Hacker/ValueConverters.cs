using System;
using System.Globalization;
using System.Windows.Data;

namespace Nyet2Hacker
{
    public class IntToHexStringConverter : IValueConverter
    {
        public object Convert(
            object value,
            Type targetType,
            object parameter,
            CultureInfo culture)
        {
            if (!(value is int intValue))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    "Unable to process anything but ints."
                );
            }

            return intValue.ToString("X");
        }

        public object ConvertBack(
            object value,
            Type targetType,
            object parameter,
            CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

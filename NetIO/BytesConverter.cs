using System;
using System.Globalization;
using System.Windows.Data;

namespace NetIO
{
    internal class BytesConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var lval = value as long?;

            if (lval != null)
            {
                if (lval >= 1073741824)
                    return String.Format("{0:0.00} GB", (double)lval / 1073741824);
                if (lval >= 1048576)
                    return String.Format("{0:0.00} MB", (double)lval / 1048576);
                if (lval >= 1024)
                    return String.Format("{0:0.00} KB", (double)lval / 1024);
                return (double)lval + " B";
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
using System;
using System.Globalization;
using System.Windows.Data;

namespace CameraControl.Converter
{
    public class MultiBoolConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length != 2) return false;
            if (values[0] is bool b)
            {
                if (b)
                {
                    return true;
                }
                else
                {
                    if (values[1] is bool b2 && b2)
                        return true;
                }
            }
            return false;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

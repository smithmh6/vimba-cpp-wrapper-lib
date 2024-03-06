using System;
using System.Globalization;
using System.Windows.Data;

namespace FilterWheelShared.Converter
{
    public class MultiplierConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (parameter is not string s)
            {
                return value;
            }
            switch (value)
            {
                case int iValue:
                    if (int.TryParse(s, out int iParam))
                    {
                        return iValue * iParam;
                    }
                    break;
                case double dValue:
                    if (double.TryParse(s, out double dParam))
                    {
                        return dValue * dParam;
                    }
                    break;
                default:
                    return value;
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

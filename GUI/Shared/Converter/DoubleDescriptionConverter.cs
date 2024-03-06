using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Windows.Data;
using FilterWheelShared.Common;

namespace FilterWheelShared.Converter
{
    public class DoubleDescriptionConverter : IMultiValueConverter, IValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length != 2)
                return string.Empty;

            Enum myEnum = (Enum)values[0];
            if (myEnum == null)
                return string.Empty;

            bool myBool = (bool)values[1];
            return GetDoubleDescription(myEnum, myBool);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            return new[] { string.Empty };
        }

        private string GetDoubleDescription(Enum enumObj, object obj)
        {
            FieldInfo fieldInfo = enumObj.GetType().GetField(enumObj.ToString());
            var doubleDescAttr = fieldInfo
                .GetCustomAttributes(false)
                .OfType<DoubleDescriptionAttribute>()
                .SingleOrDefault();
            if (doubleDescAttr == null)
            {
                return enumObj.ToString();
            }
            else
            {
                if (obj is int index)
                    return index == 0 ? doubleDescAttr.Description1 : doubleDescAttr.Description2;
                if (obj is bool b)
                    return b ? doubleDescAttr.Description1 : doubleDescAttr.Description2;
                return doubleDescAttr.Description1;
            }
        }

        object IValueConverter.Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            Enum myEnum = (Enum)value;
            int index = System.Convert.ToInt32(parameter);
            string description = GetDoubleDescription(myEnum, index);
            return description;
        }

        object IValueConverter.ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return string.Empty;
        }
    }
}

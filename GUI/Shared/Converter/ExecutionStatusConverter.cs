using System;
using System.Globalization;
using System.Windows.Data;

namespace FilterWheelShared.Converter
{
    public enum ExecutionStatus
    {
        Living,
        Snapshoting,
        Capturing,
        None
    }

    public class ExecutionStatusConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isMatch = true;
            if ((ExecutionStatus)value != ExecutionStatus.None && parameter is string status)
            {
                var currentStatus = (ExecutionStatus)Enum.Parse(typeof(ExecutionStatus), status);
                var executeStatus = (ExecutionStatus)value;
                isMatch = currentStatus == executeStatus;
            }
            return isMatch;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

using System;
using System.Globalization;
using System.Windows.Data;

namespace FilterWheelShared.Converter
{
    public enum TimeFormat
    {
        ms,
        us
    }
    public class TimeFormatConverter : IValueConverter
    {
        private double TimeFormatConvert(double value, string src, string dst)
        {
            TimeFormat srcFormat = (TimeFormat)Enum.Parse(typeof(TimeFormat), src, true);
            TimeFormat dstFormat = (TimeFormat)Enum.Parse(typeof(TimeFormat), dst, true);
            if (srcFormat == dstFormat) 
            {
                return value;
            }
            double multiplier = 1.0;
            switch (srcFormat)
            {
                case TimeFormat.ms:
                    {
                        switch (dstFormat)
                        {
                            case TimeFormat.us:
                                multiplier = 1e3;
                                break;
                            default:
                                break;
                        }
                        break;
                    }
                case TimeFormat.us:
                    {
                        switch (dstFormat)
                        {
                            case (TimeFormat.ms):
                                multiplier = 1e-3;
                                break;
                            default:
                                break;
                        }
                        break;
                    }
                default:
                    break;
            }
            return value * multiplier;
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double d && parameter is string s && s.Contains('2'))
            {
                var splits = s.Split('2');
                if (splits.Length != 0 )
                {
                    return value;
                }
                var srcFormat = splits[0];
                var dstFormat = splits[1];

                return TimeFormatConvert(d, srcFormat, dstFormat);
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double d && parameter is string s && s.Contains('2'))
            {
                var splits = s.Split('2');
                if (splits.Length != 0)
                {
                    return value;
                }
                var srcFormat = splits[0];
                var dstFormat = splits[1];

                return TimeFormatConvert(d, srcFormat, dstFormat);
            }
            return value;
        }
    }
}

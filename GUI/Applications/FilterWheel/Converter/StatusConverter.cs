using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using FilterWheel.Localization;
using FilterWheelShared.Common;

namespace FilterWheel.Converter
{
    public class StatusConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Status status && parameter is string s)
            {
                bool isReturnBrush = s == "1";
                if (!isReturnBrush)
                {
                    string StatusStr;
                    var _localizationService = LocalizationService.GetInstance();

                    switch (status)
                    {
                        case Status.Normal:
                        case Status.Ready:
                            StatusStr = _localizationService.GetLocalizationString(ShellLocalizationKey.Normal);
                            break;
                        case Status.Warning:
                            StatusStr = _localizationService.GetLocalizationString(ShellLocalizationKey.Warning);
                            break;
                        case Status.Error:
                            StatusStr = _localizationService.GetLocalizationString(ShellLocalizationKey.Error);
                            break;
                        default:
                            StatusStr = "";
                            break;
                    }
                    return StatusStr;
                }

                switch (status)
                {
                    case Status.Normal:
                    case Status.Ready:
                        return new SolidColorBrush(Colors.Green);
                    case Status.Warning:
                    case Status.Busy:
                        return new SolidColorBrush(Colors.Yellow);
                    case Status.Error:
                        return new SolidColorBrush(Colors.Red);
                }
            }

            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

}

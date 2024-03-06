using DrawingTool.Factory;
using PluginCommon.Localization;
using System;
using System.Globalization;
using System.Windows.Data;

namespace Viewport.Converter
{
    internal class RelativePlacementEnumToDisplayRelativePlacementConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is RelativePlacement _relativePlacement)
            {
                string RPStr;
                var localizationService = PluginLocalizationService.GetInstance();

                switch (_relativePlacement)
                {
                    case RelativePlacement.BottomLeft:
                        RPStr = localizationService.GetLocalizationString(PluginLocalziationKey.BottomLeft);
                        break;
                    case RelativePlacement.BottomRight:
                        RPStr = localizationService.GetLocalizationString(PluginLocalziationKey.BottomRight);
                        break;
                    case RelativePlacement.TopLeft:
                        RPStr = localizationService.GetLocalizationString(PluginLocalziationKey.TopLeft);
                        break;
                    case RelativePlacement.TopRight:
                        RPStr = localizationService.GetLocalizationString(PluginLocalziationKey.TopRight);
                        break;
                    default:
                        RPStr = "";
                        break;
                }
                return RPStr;
            }
            return null;

        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

    }
}

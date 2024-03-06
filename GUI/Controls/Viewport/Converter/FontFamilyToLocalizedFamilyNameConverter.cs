using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;

namespace Viewport.Converter
{
    internal class FontFamilyToLocalizedFamilyNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var _fontFamily = value as FontFamily;
            LanguageSpecificStringDictionary _fontDic = _fontFamily.FamilyNames;

            var _currentLanguage = PluginCommon.Localization.PluginLocalizationService.GetInstance().CurrentLanguage;

            if (_currentLanguage.Contains("中文") && _fontDic.ContainsKey(XmlLanguage.GetLanguage("zh-cn")))
            {
                if (_fontDic.TryGetValue(XmlLanguage.GetLanguage("zh-cn"), out string _fontName))
                {
                    return _fontName;
                }
            }

            return _fontFamily.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

}

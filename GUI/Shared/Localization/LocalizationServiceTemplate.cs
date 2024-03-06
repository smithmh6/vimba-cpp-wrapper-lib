using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using FilterWheelShared.Common;

namespace FilterWheelShared.Localization
{
    public class LocalizationServiceTemplate<TLocalizationKeyEnum> : ILocalizationService
    {
        public event EventHandler<PropertyChangedEventArgs> PropertyChangedEventHandler;
        public static event EventHandler<LanguageChangedEventArgs> LanguageChangedEvent;
        public IReadOnlyList<string> LanguageList { get { return languageDic.Keys.ToList(); } }
        public string CurrentLanguage { get; private set; } = null;

        private Dictionary<string, Dictionary<TLocalizationKeyEnum, string>> languageDic = new Dictionary<string, Dictionary<TLocalizationKeyEnum, string>>();

        protected LocalizationServiceTemplate()
        {
            LoadLanguages();
        }

        protected virtual void LoadLanguages()
        {
            string directory = string.IsNullOrEmpty(ThorlabsProduct.LanguageFileDirectory) ? "Localization" : ThorlabsProduct.LanguageFileDirectory;
            var assmblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var fileDirectory = Path.Combine(assmblyDirectory, directory);
            LoadDictionaryFromDirectory(fileDirectory);
        }

        public void AddLanguage(string _language, Dictionary<TLocalizationKeyEnum, string> dic)
        {
            languageDic.Add(_language, dic);
            if (string.IsNullOrEmpty(CurrentLanguage))
                SetLanguage(_language);
            PropertyChangedEventHandler?.Invoke(null, new PropertyChangedEventArgs(nameof(LanguageList)));
        }

        public void SetLanguage(string language)
        {
            CurrentLanguage = language;
            LanguageChangedEvent?.Invoke(this, new LanguageChangedEventArgs { Language = language });
        }

        public string GetLocalizationString<TLocalizationKeyEnum1>(TLocalizationKeyEnum1 key)
        {
            if (key is TLocalizationKeyEnum tKey)
            {
                if (string.IsNullOrEmpty(CurrentLanguage))
                    return string.Empty;
                return languageDic[CurrentLanguage][tKey];
            }
            throw new ArgumentException("The type of arguments is wrong");
        }

        protected virtual void LoadDictionaryFromDirectory(string directory)
        {
            languageDic.Clear();
            var directoryInfo = new DirectoryInfo(directory);
            foreach (var file in directoryInfo.GetFiles("*.language"))
            {
                using (var stream = file.OpenRead())
                {
                    try
                    {
                        LoadLanguage(stream);
                    }
                    catch (Exception e)
                    {
                        throw new InvalidOperationException($"Cannot load the language file \"{file.FullName}\"", e);
                    }
                }
            }
        }

        protected virtual void LoadLanguage(Stream stream)
        {
            var rootElement = XElement.Load(stream);
            var languge = rootElement.Attribute("Name").Value;
            var resultDic = new Dictionary<TLocalizationKeyEnum, string>();
            var elements = rootElement.Elements("Text");
            foreach (string key in Enum.GetNames(typeof(TLocalizationKeyEnum)))
            {
                var res = elements.FirstOrDefault((i) => i.Attribute("key").Value == key);
                if (res != null)
                {
                    var key_value = res.Attribute("key").Value;
                    var lang_value = res.Attribute("value").Value;
                    resultDic.Add((TLocalizationKeyEnum)Enum.Parse(typeof(TLocalizationKeyEnum), key_value), lang_value);
                }
                else
                {
                    throw new ArgumentException($"The given key \"{key}\" does not exist");
                }
            }
            AddLanguage(languge, resultDic);
            var attribute = rootElement.Attribute("Default");
            if (attribute == null)
                return;
            var isDefaultStr = attribute.Value;
            if (bool.TryParse(isDefaultStr, out bool isDefault))
            {
                if (isDefault)
                    SetLanguage(languge);
            }
        }
    }
}

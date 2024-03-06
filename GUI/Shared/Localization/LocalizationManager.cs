using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace FilterWheelShared.Localization
{
    public class LanguageChangedEventArgs : EventArgs
    {
        public string Language { get; set; }
    }

    public class LocalizationManager
    {
        private static LocalizationManager instance;
        private List<ILocalizationService> serviceList = new List<ILocalizationService>();
        public string CurrentLanguage { get; private set; }
        public ObservableCollection<string> Languages { get; set; }
        public event EventHandler<LanguageChangedEventArgs> LanguageChangedEvent;

        private LocalizationManager() { }
        static LocalizationManager()
        {
            instance = new LocalizationManager();
        }
        public static LocalizationManager GetInstance()
        {
            return instance;
        }

        public void AddListener(ILocalizationService service)
        {
            if (serviceList.Count == 0)
                Languages = new ObservableCollection<string>(service.LanguageList);
            else if (Languages.Count != service.LanguageList.Count || Languages.Any(l => !service.LanguageList.Contains(l)))
                throw new ArgumentException("Languages in added service are different with languages in localization manager");
            serviceList.Add(service);
            if (string.IsNullOrEmpty(CurrentLanguage))
                CurrentLanguage = service.CurrentLanguage;
            else
                service.SetLanguage(CurrentLanguage);
        }

        public void RemoveListener(ILocalizationService service)
        {
            serviceList.Remove(service);
        }

        public void SetLanguage(string language)
        {
            if (CurrentLanguage == language)
                return;
            CurrentLanguage = language;
            foreach (var service in serviceList)
            {
                service.SetLanguage(language);
            }
            LanguageChangedEvent?.Invoke(this, new LanguageChangedEventArgs { Language = language });
        }
    }
}

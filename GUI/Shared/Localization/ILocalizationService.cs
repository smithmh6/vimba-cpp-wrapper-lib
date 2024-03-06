using System.Collections.Generic;

namespace FilterWheelShared.Localization
{
    public interface ILocalizationService
    {
        string GetLocalizationString<TLocalizationKeyEnum>(TLocalizationKeyEnum key);
        void SetLanguage(string language);
        IReadOnlyList<string> LanguageList { get; }
        string CurrentLanguage { get; }
    }
}

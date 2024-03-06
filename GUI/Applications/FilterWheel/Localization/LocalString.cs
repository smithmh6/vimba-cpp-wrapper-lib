using FilterWheelShared.Localization;

namespace FilterWheel.Localization
{
    public class LocalString : LocalizationExtensionTemplate<ShellLocalizationKey>
    {
        public LocalString() : base(LocalizationService.GetInstance())
        {

        }

        public LocalString(ShellLocalizationKey kind) : base(LocalizationService.GetInstance())
        {
            Kind = kind;
        }
    }
}

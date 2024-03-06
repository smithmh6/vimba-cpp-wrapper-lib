using FilterWheelShared.Localization;

namespace PluginCommon.Localization
{
    public class LocalString : LocalizationExtensionTemplate<PluginLocalziationKey>
    {
        public LocalString() : base(PluginLocalizationService.GetInstance())
        {

        }

        public LocalString(PluginLocalziationKey kind) : base(PluginLocalizationService.GetInstance())
        {
            Kind = kind;
        }
    }
}

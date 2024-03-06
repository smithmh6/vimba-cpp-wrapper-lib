using FilterWheelShared.Localization;

namespace PluginCommon.Localization
{
    public class PluginLocalizationShell : LocalizationClassTemplate<PluginLocalziationKey>
    {
        public static void ResiterService(ILocalizationService service)
        {
            serviceInstance = service;
        }
    }
}

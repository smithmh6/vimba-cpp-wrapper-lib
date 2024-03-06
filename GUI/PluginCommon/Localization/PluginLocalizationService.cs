using FilterWheelShared.Localization;

namespace PluginCommon.Localization
{
    public class PluginLocalizationService : LocalizationServiceTemplate<PluginLocalziationKey>
    {
        private PluginLocalizationService() : base()
        {

        }
        private static PluginLocalizationService instance;
        static PluginLocalizationService()
        {
            instance = new PluginLocalizationService();
            PluginLocalizationShell.ResiterService(instance);
        }

        public static PluginLocalizationService GetInstance() { return instance; }
    }
}

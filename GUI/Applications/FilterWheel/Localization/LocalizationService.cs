using FilterWheelShared.Localization;

namespace FilterWheel.Localization
{
    public class LocalizationService : LocalizationServiceTemplate<ShellLocalizationKey>
    {
        private LocalizationService() : base()
        {

        }
        private static LocalizationService instance;
        static LocalizationService()
        {
            instance = new LocalizationService();
            LocalizationShell.ResiterService(instance);
        }
        public static LocalizationService GetInstance() { return instance; }
    }
}

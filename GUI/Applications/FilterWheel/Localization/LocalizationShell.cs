using FilterWheelShared.Localization;

namespace FilterWheel.Localization
{
    public class LocalizationShell : LocalizationClassTemplate<ShellLocalizationKey>
    {
        public static void ResiterService(ILocalizationService service)
        {
            serviceInstance = service;
        }
    }
}

using System;
using System.Windows;
using System.Windows.Markup;

namespace FilterWheelShared.Localization
{
    [MarkupExtensionReturnType(typeof(string))]
    public class LocalizationExtensionTemplate<TLocalizationKeyEnum> : MarkupExtension
    {
        private ILocalizationService localizationService;
        public LocalizationExtensionTemplate(ILocalizationService service)
        {
            localizationService = service;
        }
        public TLocalizationKeyEnum Kind { get; set; }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            if (serviceProvider.GetService(typeof(IProvideValueTarget)) is IProvideValueTarget providerValueTarget)
            {
                var obj = providerValueTarget.TargetObject;
                var type = providerValueTarget.TargetObject.GetType();
                var property = type.GetProperty(providerValueTarget.TargetProperty.ToString());
                if (type.Name.EndsWith("SharedDp") || type == typeof(FrameworkElement))
                    return this;
                else
                {
                    var subscriber = new LocalizationSubscriber<TLocalizationKeyEnum>(obj, localizationService, property, Kind);
                    (obj as DependencyObject).SetValue(LocalizationClassTemplate<TLocalizationKeyEnum>.SubscriberProperty, subscriber);
                }
                return localizationService.GetLocalizationString(Kind);
            }
            else
                throw new InvalidOperationException();
        }
    }
}

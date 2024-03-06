using System.Reflection;
using System.Windows;

namespace FilterWheelShared.Localization
{
    public class LocalizationClassTemplate<TLocalizationEnum> : DependencyObject
    {
        protected static ILocalizationService serviceInstance;

        public static void SetKind(DependencyObject obj, TLocalizationEnum value)
        {
            obj.SetValue(KindProperty, value);
        }

        public static TLocalizationEnum GetKind(DependencyObject obj)
        {
            return (TLocalizationEnum)obj.GetValue(KindProperty);
        }

        public static readonly DependencyProperty KindProperty =
            DependencyProperty.RegisterAttached("Kind", typeof(TLocalizationEnum), typeof(LocalizationClassTemplate<TLocalizationEnum>), new PropertyMetadata(default(TLocalizationEnum), OnKindPropertyAttached));

        internal static readonly DependencyProperty SubscriberProperty =
            DependencyProperty.RegisterAttached("Subscriber", typeof(LocalizationSubscriber<TLocalizationEnum>), typeof(LocalizationClassTemplate<TLocalizationEnum>));

        private static void OnKindPropertyAttached(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var temp = GetPath(d);
            if (string.IsNullOrEmpty(temp))
                return;
            var property = d.GetType().GetProperty(GetPath(d));
            property.SetValue(d, serviceInstance.GetLocalizationString(GetKind(d)));
            var subscriber = new LocalizationSubscriber<TLocalizationEnum>(d, serviceInstance, property, GetKind(d));
            d.SetValue(SubscriberProperty, subscriber);
        }

        public static readonly DependencyProperty PathProperty =
            DependencyProperty.RegisterAttached("Path", typeof(string), typeof(LocalizationClassTemplate<TLocalizationEnum>), new PropertyMetadata(default(string), OnKindPropertyAttached));

        public static void SetPath(DependencyObject d, string value)
        {
            d.SetValue(PathProperty, value);
        }

        public static string GetPath(DependencyObject d)
        {
            return (string)d.GetValue(PathProperty);
        }
    }

    internal class LocalizationSubscriber<TLocalizationKeyEnum>
    {
        private object element;
        private PropertyInfo propertyInfo;
        private ILocalizationService localizationService;
        private TLocalizationKeyEnum kind;
        private static LocalizationManager manager = LocalizationManager.GetInstance();

        public LocalizationSubscriber(object frameworkElement, ILocalizationService _localizationService, PropertyInfo _propertyInfo, TLocalizationKeyEnum _kind)
        {
            element = frameworkElement;
            localizationService = _localizationService;
            propertyInfo = _propertyInfo;
            kind = _kind;
            WeakEventManager<LocalizationManager, LanguageChangedEventArgs>.AddHandler(manager, nameof(manager.LanguageChangedEvent), DoWork);
        }
        private void DoWork(object sender, LanguageChangedEventArgs e)
        {
            propertyInfo.SetValue(element, localizationService.GetLocalizationString(kind));
        }
    }
}

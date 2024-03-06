using Prism.Events;
using System;
using System.Windows;
using System.Windows.Controls;
using Telerik.Windows.Controls;
using FilterWheel.Infrastructure;
using FilterWheel.Localization;
using FilterWheelShared.Common;
using FilterWheelShared.Localization;
using LocalizationManager = FilterWheelShared.Localization.LocalizationManager;
using Settings.Views;

namespace FilterWheel.View
{
    /// <summary>
    /// Interaction logic for OptionWindow.xaml
    /// </summary>
    public partial class OptionWindow : RadWindow
    {
        private static LocalizationManager localizationManager;
        private static LocalizationService localizationService = LocalizationService.GetInstance();
        private static IEventAggregator eventAggregator;

        private CameraSettingsUC _cameraSettingPanel = null;
        private FilterWheelColorSettings _filterWheelColorSettingPanel = null;

        public OptionWindow(LocalizationManager localizationManager, IEventAggregator ea)
        {
            InitializeComponent();

            _cameraSettingPanel = new CameraSettingsUC();
            _cameraSettingPanel.SettingWindowCloseEvent += (s, e) => Close();
            CameraContent.Content = _cameraSettingPanel;

            _filterWheelColorSettingPanel = new FilterWheelColorSettings();
            _filterWheelColorSettingPanel.SettingWindowCloseEvent += (s, e) => Close();
            FilterWheelContent.Content = _filterWheelColorSettingPanel;

            OptionWindow.localizationManager = localizationManager;
            eventAggregator = ea;
            languageListBox.ItemsSource = localizationManager.Languages;
            languageListBox.SelectedItem = localizationManager.CurrentLanguage;
            localizationManager.LanguageChangedEvent += LocalizationManager_LanguageChangedEvent;
            if (!ThorlabsProduct.IsSupportMultiLanguage)
            {
                languageTextBlock.Visibility = Visibility.Hidden;
                languageListBox.Visibility = Visibility.Hidden;
            }
            Closed += OptionWindow_Closed;
            colorStyleListBox.ItemsSource = new string[] { localizationService.GetLocalizationString(ShellLocalizationKey.Dark), localizationService.GetLocalizationString(ShellLocalizationKey.Light) };
            if (Properties.Settings.Default.ColorStyle == ThorlabsProduct.DarkThemeName)
                colorStyleListBox.SelectedIndex = 0;
            else
                colorStyleListBox.SelectedIndex = 1;
        }

        private void OptionWindow_Closed(object sender, EventArgs e)
        {
            localizationManager.LanguageChangedEvent -= LocalizationManager_LanguageChangedEvent;
        }

        private void LocalizationManager_LanguageChangedEvent(object sender, LanguageChangedEventArgs e)
        {
            var themeSelectedIndex = colorStyleListBox.SelectedIndex;
            colorStyleListBox.ItemsSource = new string[] { localizationService.GetLocalizationString(ShellLocalizationKey.Dark), localizationService.GetLocalizationString(ShellLocalizationKey.Light) };
            colorStyleListBox.SelectedIndex = themeSelectedIndex;
        }

        private void ColorStyleListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var listbox = sender as ListBox;
            if (listbox.SelectedIndex == 0)
            {
                ApplicationTheme.SetColorStyle(1);
                if (Properties.Settings.Default.ColorStyle != ThorlabsProduct.DarkThemeName)
                {
                    Properties.Settings.Default.ColorStyle = ThorlabsProduct.DarkThemeName;
                    Properties.Settings.Default.Save();
                }
            }
            else
            {
                ApplicationTheme.SetColorStyle(0);
                if (Properties.Settings.Default.ColorStyle != ThorlabsProduct.LightThemeName)
                {
                    Properties.Settings.Default.ColorStyle = ThorlabsProduct.LightThemeName;
                    Properties.Settings.Default.Save();
                }
            }
        }

        private void LanguageListBox_Loaded(object sender, RoutedEventArgs e)
        {
            var listbox = sender as ListBox;
            listbox.SelectedItem = LocalizationService.GetInstance().CurrentLanguage;
        }

        private void LanguageListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count != 0)
            {
                localizationManager.SetLanguage(e.AddedItems[0].ToString());
                if (Properties.Settings.Default.Language != localizationManager.CurrentLanguage)
                {
                    Properties.Settings.Default.Language = localizationManager.CurrentLanguage;
                    Properties.Settings.Default.Save();
                }
            }
        }

        private bool _allowUpdate = true;
        protected override void OnClosed()
        {
            _allowUpdate = true;
            base.OnClosed();
        }

        public void Update()
        {
            _cameraSettingPanel.UpdateSource();
            _filterWheelColorSettingPanel.UpdateSource();
        }


        //protected override void OnActivated(EventArgs e)
        //{
        //    if (_allowUpdate)
        //    {
        //        _allowUpdate = false;
        //        if (_settingPanel.DataContext is IUpdate @interface)
        //        {
        //            @interface.StartUpdate();
        //        }
        //    }
        //    base.OnActivated(e);
        //}
    }
}

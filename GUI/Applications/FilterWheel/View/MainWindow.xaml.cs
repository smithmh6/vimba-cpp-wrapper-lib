using CameraControl;
using Prism.Events;
using System.Collections.Generic;
using System.Windows;
using Telerik.Windows.Controls;
using FilterWheel.Infrastructure;
using FilterWheel.Localization;
using FilterWheelShared.Common;
using FilterWheelShared.DeviceDataService;
using Viewport;
using LocalizationManager = FilterWheelShared.Localization.LocalizationManager;

namespace FilterWheel.View
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : RadWindow
    {
        private LocalizationManager localizationManager;
        private LocalizationService localizationService = LocalizationService.GetInstance();
        private IEventAggregator eventAggregator;
        private ViewportUC _viewport;

        public MainWindow(IEventAggregator eventAggregator, LocalizationManager localization)
        {
            InitializeComponent();
            this.eventAggregator = eventAggregator;
            localizationManager = localization;
            Application.Current.Resources.MergedDictionaries.Add(this.Resources);

            this.CameraControlContent.Content = new CameraControlUC();   
            _viewport = new ViewportUC();
            this.ViewportContent.Content = _viewport;

            DisplayService.Instance.EventAggregator = eventAggregator;
        }
        // Below functions are for menus in the header of the window
        // You can delete them if you are running the application in StandardRibbonButton=true mode
        #region Header Menu
        private void RadMenuItemExit_Click(object sender, Telerik.Windows.RadRoutedEventArgs e)
        {
            this.Close();
        }

        protected override bool OnClosing()
        {
            _viewport.SaveLocationDoc();
            eventAggregator.GetEvent<FilterWheelShared.Event.CloseApplicationEvent>().Publish(0);
            return base.OnClosing();
        }

        // Language menu
        private void MenuItemLanguage_Click(object sender, Telerik.Windows.RadRoutedEventArgs e)
        {
            if (sender != null)
            {
                localizationManager.SetLanguage(((RadMenuItem)sender).Header.ToString());
                Properties.Settings.Default.Language = localizationManager.CurrentLanguage;
                Properties.Settings.Default.Save();
            }

            // Change checked/unchecked status
            var currentItem = e.OriginalSource as RadMenuItem;
            if (currentItem.IsCheckable && currentItem.Tag != null)
            {
                var siblingItems = this.GetSiblingGroupItems(currentItem);
                if (siblingItems != null)
                {
                    foreach (var item in siblingItems)
                    {
                        if (item != currentItem)
                        {
                            item.IsChecked = false;
                        }
                    }
                }
            }
        }

        private void RadMenuItemLanguage_Loaded(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as RadMenuItem;
            foreach (RadMenuItem item in menuItem.Items)
            {
                if (item.Header.ToString() == localizationManager.CurrentLanguage)
                    item.IsChecked = true;
                else
                    item.IsChecked = false;
            }
        }
        // Theme menu

        private void MenuItemTheme_Click(object sender, Telerik.Windows.RadRoutedEventArgs e)
        {
            if (sender != null)
            {
                if (((RadMenuItem)sender).Header.ToString() == localizationService.GetLocalizationString(ShellLocalizationKey.Dark))
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

            // Change checked/unchecked status
            var currentItem = e.OriginalSource as RadMenuItem;
            if (currentItem.IsCheckable && currentItem.Tag != null)
            {
                var siblingItems = this.GetSiblingGroupItems(currentItem);
                if (siblingItems != null)
                {
                    foreach (var item in siblingItems)
                    {
                        if (item != currentItem)
                        {
                            item.IsChecked = false;
                        }
                    }
                }
            }
        }

        private void RadMenuItemTheme_Loaded(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as RadMenuItem;
            if (Properties.Settings.Default.ColorStyle == ThorlabsProduct.DarkThemeName)
            {
                (menuItem.Items[0] as RadMenuItem).IsChecked = true;
                (menuItem.Items[1] as RadMenuItem).IsChecked = false;
            }
            else
            {
                (menuItem.Items[1] as RadMenuItem).IsChecked = true;
                (menuItem.Items[0] as RadMenuItem).IsChecked = false;
            }
        }

        private List<RadMenuItem> GetSiblingGroupItems(RadMenuItem currentItem)
        {
            var parentItem = currentItem.ParentOfType<RadMenuItem>();
            if (parentItem == null)
            {
                return null;
            }
            List<RadMenuItem> items = new List<RadMenuItem>();
            foreach (var item in parentItem.Items)
            {
                RadMenuItem container = parentItem.ItemContainerGenerator.ContainerFromItem(item) as RadMenuItem;
                if (container == null || container.Tag == null)
                {
                    continue;
                }
                if (container.Tag.Equals(currentItem.Tag))
                {
                    items.Add(container);
                }
            }
            return items;
        }
        #endregion
    }
}

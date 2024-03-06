using System.Globalization;
using System.Windows;
using Viewport.ViewModels;

namespace Viewport
{
    /// <summary>
    /// Interaction logic for ColorImageView.xaml
    /// </summary>
    public partial class ColorImageView
    {
        public ColorImageView()
        {
            Init(true);

            InitializeComponent();

            Name = nameof(ColorImageView);

            this.DataContext = new ColorImageViewModel();
        }

        public static void Init(bool isStandAlone)
        {
            if (!isStandAlone)
            {
                CultureInfo.CurrentCulture =
                CultureInfo.CurrentUICulture =
                CultureInfo.DefaultThreadCurrentCulture =
                CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
            }

            PluginCommon.PluginTheme.LoadPluginTheme(isStandAlone);

            FilterWheelShared.Localization.LocalizationManager.GetInstance().AddListener(PluginCommon.Localization.PluginLocalizationService.GetInstance());
        }

        private bool _apply = false;
        //public void UpdateDataContext()
        //{
        //    _apply = false;
        //    if (this.DataContext is FilterWheelShared.Common.IUpdate @interface)
        //    {
        //        @interface.StartUpdate();
        //    }
        //}

        //protected override bool OnClosing()
        //{
        //    if (this.DataContext is FilterWheelShared.Common.IUpdate @interface)
        //    {
        //        @interface.StopUpdate(_apply);
        //    }

        //    return base.OnClosing();
        //}

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            _apply = false;
            Close();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            _apply = true;
            Close();
        }
    }
}

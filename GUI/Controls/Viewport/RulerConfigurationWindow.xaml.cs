using System.Globalization;
using System.Windows;

namespace Viewport
{
    /// <summary>
    /// Interaction logic for RulerConfigurationWindow.xaml
    /// </summary>
    public partial class RulerConfigurationWindow
    {
        public RulerConfigurationWindow()
        {
            Init(true);
            InitializeComponent();

            Name = nameof(RulerConfigurationWindow);

            NumberFormatInfo nfi = (NumberFormatInfo)System.Threading.Thread.CurrentThread
                       .CurrentCulture.NumberFormat.Clone();
            nfi.NumberGroupSeparator = "";
            LineWidthNumeric.NumberFormatInfo = nfi;
            OpacityNumeric.NumberFormatInfo = nfi;

            //this.DataContext = FilterWheelShared.Common.MVMManager.Instance["RulerConfigurationViewModel"];
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

        private void RadButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}

using System.Windows;
using System.Windows.Controls;
using Telerik.Windows.Controls;
using FilterWheel.Localization;
using FilterWheelShared.DeviceDataService;

namespace FilterWheel.View
{
    /// <summary>
    /// Interaction logic for ReconnectWindow.xaml
    /// </summary>
    public partial class ReconnectWindow : RadWindow, IConnectWindow
    {
        private int _times = 0;
        public int Times
        {
            get => _times;
            set
            {
                _times = value;
                if (!_reconnectSuccessful)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        this.body.Text = string.Format(LocalizationService.GetInstance().GetLocalizationString(ShellLocalizationKey.ReconnectBody), _times);
                    });
                }
            }
        }

        private bool _reconnectSuccessful;

        public bool ReconnectSuccessful
        {
            get => _reconnectSuccessful;
            set
            {
                _reconnectSuccessful = value;
                if (_reconnectSuccessful)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        this.header.Text = LocalizationService.GetInstance().GetLocalizationString(ShellLocalizationKey.ReconnectSuccess);
                        Grid.SetRowSpan(this.header, 2);
                        this.body.Visibility = Visibility.Collapsed;
                    });
                }
            }
        }

        public ReconnectWindow()
        {
            InitializeComponent();
        }

        protected override void OnClosed()
        {
            base.OnClosed();
            this.header.Text = LocalizationService.GetInstance().GetLocalizationString(ShellLocalizationKey.ReconnectHeader);
            Grid.SetRowSpan(this.header, 1);
            this.body.Visibility = Visibility.Visible;
            ReconnectService.StopReconnect();
            _times = 0;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        void IConnectWindow.ShowDialog()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                this.Owner = Application.Current.MainWindow;
                this.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                this.ShowDialog();
            });
        }


        void IConnectWindow.Close()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                this.Close();
            });
        }
    }
}

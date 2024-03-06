using System;
using System.Diagnostics;
using Telerik.Windows.Controls;
using FilterWheel.ViewModel;

namespace FilterWheel.View
{
    /// <summary>
    /// Interaction logic for SupportWindow.xaml
    /// </summary>
    public partial class SupportWindow : RadWindow
    {
        public SupportWindow()
        {
            InitializeComponent();
        }

        public void Refresh()
        {
            (this.DataContext as SupportWindowViewModel).Refresh();
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            try
            {
                var pInfo = new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true };
                pInfo.ErrorDialog = true;

                Process.Start(pInfo);
            }
            catch (Exception)
            {
            }
            e.Handled = true;
        }
    }
}

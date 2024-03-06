using System.Windows;
using Telerik.Windows.Controls;

namespace Viewport.Views
{
    /// <summary>
    /// Interaction logic for CombineWindow.xaml
    /// </summary>
    public partial class CombineWindow : RadWindow
    {
        public CombineWindow()
        {
            InitializeComponent();
        }
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}

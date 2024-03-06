using System.Diagnostics;
using System.Windows;
using System.Windows.Input;

namespace FilterWheelShared.Controls.Helpers
{
    public class LinkHelper : DependencyObject
    {

        // Using a DependencyProperty as the backing store for Link.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty LinkProperty =
            DependencyProperty.RegisterAttached("Link", typeof(string), typeof(LinkHelper), new PropertyMetadata("", LinkPropertyChangedCallback));

        private static void LinkPropertyChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var element = d as UIElement;
            element.MouseLeftButtonDown -= Element_MouseLeftButtonDown;
            element.MouseLeftButtonDown += Element_MouseLeftButtonDown;
            if (element is FrameworkElement fe)
                fe.Cursor = Cursors.Hand;
        }

        private static void Element_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var element = sender as UIElement;
            var uri = GetLink(element);
            Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
        }

        public static void SetLink(UIElement element, string value)
        {
            element.SetValue(LinkProperty, value);
        }

        public static string GetLink(UIElement element)
        {
            return (string)element.GetValue(LinkProperty);
        }
    }

}

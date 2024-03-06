using System.Windows;
using System.Windows.Controls;

namespace CameraControl.Infrastructure
{
    public static class RadioButtonExtension
    {
        public static int GetIndex(DependencyObject obj)
        {
            return (int)obj.GetValue(IndexProperty);
        }

        public static void SetIndex(DependencyObject obj, int value)
        {
            obj.SetValue(IndexProperty, value);
        }

        // Using a DependencyProperty as the backing store for Index.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty IndexProperty =
            DependencyProperty.RegisterAttached("Index", typeof(int), typeof(RadioButton), new PropertyMetadata(0));


    }
}

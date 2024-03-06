using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Telerik.Windows.Controls;
using Telerik.Windows.Controls.RibbonView;

namespace FilterWheel.Infrastructure
{
    public static class RibbonButtonHelper
    {
        public static readonly DependencyProperty AttachedSmallImageProperty = DependencyProperty.RegisterAttached(
            "AttachedSmallImage", typeof(ImageSource), typeof(RibbonButtonHelper), new FrameworkPropertyMetadata(null, AttachedSmallImagePropertyChangedCallback));

        public static void SetAttachedSmallImage(DependencyObject element, ImageSource value)
        {
            element.SetValue(AttachedSmallImageProperty, value);
        }

        public static ImageSource GetAttachedSmallImage(DependencyObject element)
        {
            return (ImageSource)element.GetValue(AttachedSmallImageProperty);
        }

        //When user set RadRibbonToggleButton property IsAutoSize, SmallImage, LargeImage, CurrentSize, 
        //The default function will change image size to 16/32/NaN,
        //So if you want this attached property effective, you cannot do any change of these property
        private static void AttachedSmallImagePropertyChangedCallback(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is IRibbonButton)
            {
                var element = (FrameworkElement)sender;
                if (element.IsLoaded)
                {
                    ApplyImage(element);
                }
                else
                {
                    element.Loaded += (o, args) => { ApplyImage(element); };
                    element.SizeChanged += (o, args) => { ApplyImage(element); };

                }
            }
        }

        public static readonly DependencyProperty SmallImageSizeProperty = DependencyProperty.RegisterAttached(
            "SmallImageSize", typeof(double), typeof(RibbonButtonHelper), new FrameworkPropertyMetadata(16d, SmallImageSizePropertyChangedCallback));

        public static void SetSmallImageSize(DependencyObject element, double value)
        {
            element.SetValue(SmallImageSizeProperty, value);
        }

        public static double GetSmallImageSize(DependencyObject element)
        {
            return (double)element.GetValue(SmallImageSizeProperty);
        }

        private static void SmallImageSizePropertyChangedCallback(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is RadRibbonButton ribbon)
            {
                if (GetAttachedSmallImage(ribbon) == null)
                {
                    ribbon.Loaded += (o, args) =>
                    {
                        if (!ribbon.IsVisible) return;
                        var imageElement = ribbon.ChildrenOfType<Image>().First();
                        imageElement.Stretch = imageElement.Stretch;
                        imageElement.Width = (double)e.NewValue;
                        imageElement.Height = (double)e.NewValue;
                    };
                }
            }
        }

        private static void ApplyImage(FrameworkElement element)
        {
            if (element is IRibbonButton ribbon)
            {
                if (ribbon.Size == ButtonSize.Large) return;
                if (!element.IsVisible) return;
                var imageElement = element.ChildrenOfType<Image>().First();
                double num = GetSmallImageSize(element);
                ImageSource source = GetAttachedSmallImage(element);
                imageElement.Stretch = imageElement.Stretch;
                imageElement.Width = num;
                imageElement.Height = num;
                imageElement.Source = source;
                imageElement.Visibility = ((imageElement.Source == null) ? Visibility.Collapsed : Visibility.Visible);
            }
        }
    }
}

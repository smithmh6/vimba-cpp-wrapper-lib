using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace FilterWheelShared.Controls.MultiImageSelector
{
    public class SelectableImage : Control
    {
        static SelectableImage()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(SelectableImage), new FrameworkPropertyMetadata(typeof(SelectableImage)));
        }

        public ImageSource ImageSource
        {
            get { return (ImageSource)GetValue(ImageSourceProperty); }
            set { SetValue(ImageSourceProperty, value); }
        }

        public static readonly DependencyProperty ImageSourceProperty =
            DependencyProperty.Register("ImageSource", typeof(ImageSource), typeof(SelectableImage), new PropertyMetadata(null));

        public bool IsSelected
        {
            get { return (bool)GetValue(IsSelectedProperty); }
            set { SetValue(IsSelectedProperty, value); }
        }

        public static readonly DependencyProperty IsSelectedProperty =
            DependencyProperty.Register("IsSelected", typeof(bool), typeof(SelectableImage), new FrameworkPropertyMetadata(false, OnSelectedPropertyChanged) { BindsTwoWayByDefault = true });

        private static void OnSelectedPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs args)
        {
            var selectableImage = d as SelectableImage;
            selectableImage.OnVisualStateChanged();
        }

        private void OnVisualStateChanged()
        {
            if (IsEnabled == false)
            {
                VisualStateManager.GoToState(this, "Disabled", true);
            }
            else
            {
                if (IsSelected)
                {
                    VisualStateManager.GoToState(this, "IsSelected", true);
                }
                else
                {
                    VisualStateManager.GoToState(this, "Normal", true);
                }
            }
        }

        public SelectableImage()
        {
            IsEnabledChanged += SelectableImage_IsEnabledChanged;
            MouseLeftButtonDown += SelectableImage_MouseLeftButtonDown;
            MouseLeftButtonUp += SelectableImage_MouseLeftButtonUp;
        }

        private void SelectableImage_IsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            OnVisualStateChanged();
        }

        private void SelectableImage_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (IsMouseCaptured && IsMouseOver)
            {
                IsSelected = !IsSelected;
            }
            ReleaseMouseCapture();
        }

        private void SelectableImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            CaptureMouse();
        }
    }

}

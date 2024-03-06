using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Documents;
using Telerik.Windows.Controls;
using FilterWheel.Localization;

namespace FilterWheel.Infrastructure
{
    internal static class PopupUtils
    {
        private static readonly LocalizationService _localizationService = LocalizationService.GetInstance();

        public static void Confirm(string content, EventHandler<WindowClosedEventArgs> closeEvent, string header = null)
        {
            if (string.IsNullOrEmpty(header))
            {
                header = _localizationService.GetLocalizationString(ShellLocalizationKey.Alert);
            }
            Application.Current?.Dispatcher.Invoke(() =>
            {
                RadWindow.Confirm(new DialogParameters
                {
                    Content = new System.Windows.Controls.TextBlock()
                    {
                        FontSize = 12,
                        Text = content,
                        TextWrapping = TextWrapping.Wrap,
                        MaxWidth = 250,
                        VerticalAlignment = VerticalAlignment.Center,
                    },
                    Owner = Application.Current?.MainWindow,
                    Header = header,
                    OkButtonContent = _localizationService.GetLocalizationString(ShellLocalizationKey.Stop),
                    CancelButtonContent = _localizationService.GetLocalizationString(ShellLocalizationKey.Continue),
                    WindowStyle = Application.Current?.FindResource("RadWindowStyle") as Style,
                    Closed = closeEvent
                });
            });
        }

        public static void Alert(string content, string header = null)
        {
            if (string.IsNullOrEmpty(header))
            {
                header = _localizationService.GetLocalizationString(ShellLocalizationKey.Alert);
            }
            Application.Current?.Dispatcher.Invoke(() =>
            {
                RadWindow.Alert(new DialogParameters
                {
                    Content = new System.Windows.Controls.TextBlock()
                    {
                        FontSize = 12,
                        Text = content,
                        TextWrapping = TextWrapping.Wrap,
                        MaxWidth = 250,
                        VerticalAlignment = VerticalAlignment.Center,
                    },
                    Owner = Application.Current?.MainWindow,
                    Header = header,
                    OkButtonContent = _localizationService.GetLocalizationString(ShellLocalizationKey.OK),
                    WindowStyle = Application.Current?.FindResource("RadWindowStyle") as Style,
                });
            });
        }

        public static void AlertWithoutMain(string content, string header = null, Action closeAction = null)
        {
            if (string.IsNullOrEmpty(header))
            {
                header = _localizationService.GetLocalizationString(ShellLocalizationKey.Alert);
            }

            Thread thread_alert = new Thread(new ThreadStart(() =>
            {
                RadWindow.Alert(new DialogParameters
                {
                    Closed = (s, e) => closeAction?.Invoke(),
                    Opened = (s, e) => (s as RadWindow).IsTopmost = true,
                    Content = new System.Windows.Controls.TextBlock()
                    {
                        FontSize = 12,
                        Text = content,
                        TextWrapping = TextWrapping.Wrap,
                        MaxWidth = 250,
                        VerticalAlignment = VerticalAlignment.Center,
                    },
                    Header = header,
                    OkButtonContent = _localizationService.GetLocalizationString(ShellLocalizationKey.OK),
                    WindowStyle = Application.Current?.FindResource("RadWindowStyle") as Style
                });
            }));
            thread_alert.SetApartmentState(ApartmentState.STA);
            thread_alert.Start();
            thread_alert.Join();
        }

        public static void AlertWithVerticalScroll(IList<string> contents, string header = null)
        {
            if (string.IsNullOrEmpty(header))
            {
                header = _localizationService.GetLocalizationString(ShellLocalizationKey.Alert);
            }

            Application.Current?.Dispatcher.Invoke(() =>
            {
                var text = new System.Windows.Controls.TextBlock()
                {
                    TextWrapping = TextWrapping.NoWrap,
                    VerticalAlignment = VerticalAlignment.Top,
                };
                for (int i = 0; i < contents.Count; i++)
                {
                    var content = contents[i];
                    var run = new Run(content) { FontSize = 12 };
                    if (i % 2 == 0)
                    {
                        run.FontWeight = FontWeights.Bold;
                        run.FontSize = 14;
                    }
                    text.Inlines.Add(run);
                }
                var scroll = new System.Windows.Controls.ScrollViewer()
                {
                    HorizontalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
                    VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
                    MaxWidth = 320,
                    MaxHeight = 200,
                    Padding = new Thickness(4),
                    Content = text,
                };

                RadWindow.Alert(new DialogParameters
                {
                    Content = scroll,
                    Owner = Application.Current?.MainWindow,
                    Header = header,
                    OkButtonContent = _localizationService.GetLocalizationString(ShellLocalizationKey.OK),
                    WindowStyle = Application.Current?.FindResource("RadWindowStyle") as Style
                });
            });
        }

    }
}

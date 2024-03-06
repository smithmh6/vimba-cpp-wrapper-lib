using Microsoft.VisualBasic;
using Microsoft.Win32;
using Prism.Events;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using Telerik.Windows.Controls;
using Telerik.Windows.Controls.ChartView;
using FilterWheelShared.Common;
using FilterWheelShared.DeviceDataService;
using FilterWheelShared.Event;
using FilterWheelShared.ImageProcess;

namespace Viewport
{
    /// <summary>
    /// Interaction logic for ProfileView.xaml
    /// </summary>
    public partial class ProfileView
    {
        private IEventAggregator eventAggregator;
        //private functions

        private void UpdateChartVisiable()
        {
            var channels = DisplayService.Instance.CurrentProcessImgChannels;
            switch(channels)
            {
                case P2dChannels.P2D_CHANNELS_1:
                    if (ProfileMonoChart.Visibility != Visibility.Visible)
                        ProfileMonoChart.Visibility = Visibility.Visible;
                    if (ProfileColorChart.Visibility != Visibility.Collapsed)
                        ProfileColorChart.Visibility = Visibility.Collapsed;
                    break;
                case P2dChannels.P2D_CHANNELS_3:
                    if (ProfileColorChart.Visibility != Visibility.Visible)
                        ProfileColorChart.Visibility = Visibility.Visible;
                    if (ProfileMonoChart.Visibility != Visibility.Collapsed)
                        ProfileMonoChart.Visibility = Visibility.Collapsed;
                    break;
            }          
        }

        private void UpdateImplement(Action action)
        {
            this.Dispatcher.InvokeAsync(action, System.Windows.Threading.DispatcherPriority.Input);
        }

        private void UpdatePopupProfile(P2dChannels channels)
        {
            if (!DisplayService.Instance.IsProfileShown) return;
            Action action = null;
            switch (channels)
            {
                case P2dChannels.P2D_CHANNELS_1:
                    if (DisplayService.Instance.ProfileMono != null && DisplayService.Instance.ProfileMono.Count > 0)
                    {
                        var max = DisplayService.Instance.ProfileMono.Last().XValue;
                        var collection = DisplayService.Instance.ProfileMono;
                        action = new Action(() =>
                        {
                            ExportBtn.IsEnabled = true;
                            ProfileMonoSerise.Visibility = Visibility.Visible;
                            XAxisMono.Maximum = max;
                            ProfileMonoSerise.ItemsSource = collection;
                        });
                    }
                    else
                    {
                        action = new Action(() =>
                        {
                            ExportBtn.IsEnabled = false;
                            ProfileMonoSerise.Visibility = Visibility.Collapsed;
                            ProfileMonoSerise.ItemsSource = null;
                        });
                    }
                    break;
                case P2dChannels.P2D_CHANNELS_3:
                    ScatterLineSeries[] lines = new ScatterLineSeries[3] { ProfileRSerise, ProfileGSerise, ProfileBSerise };
                    ObservableCollection<DoublePoint>[] profiles = new ObservableCollection<DoublePoint>[3] { DisplayService.Instance.ProfileR, DisplayService.Instance.ProfileG, DisplayService.Instance.ProfileB };

                    var length = 0;
                    foreach (var item in profiles)
                    {
                        if (item != null && item.Count > 0)
                        {
                            length = item.Count;
                            break;
                        }
                    }
                    if (length == 0)
                    {
                        action = new Action(() =>
                        {
                            ExportBtn.IsEnabled = false;
                            for (int i = 0; i < 3; i++)
                            {
                                lines[i].Visibility = Visibility.Collapsed;
                                lines[i].ItemsSource = null;
                            }
                        });
                        UpdateImplement(action);
                        return;
                    }

                    action = new Action(() =>
                    {
                        ExportBtn.IsEnabled = true;
                        for (int i = 0; i < 3; i++)
                        {
                            if (lines[i].Visibility != Visibility.Visible)
                                lines[i].Visibility = Visibility.Visible;
                            if (length > 0 && profiles[i] != null)
                                XAxisColor.Maximum = profiles[i].Last().XValue;
                            lines[i].ItemsSource = profiles[i];
                        }
                    });
                    break;
            }

            UpdateImplement(action);
        }

        //public functions
        public ProfileView()
        {
            Init(true);

            InitializeComponent();

            Name = nameof(ProfileView);

            NumberFormatInfo nfi = (NumberFormatInfo)System.Threading.Thread.CurrentThread
                       .CurrentCulture.NumberFormat.Clone();
            nfi.NumberGroupSeparator = "";
            ProfileLineWidth.NumberFormatInfo = nfi;

            UpdateChartVisiable();

            Loaded += ProfileView_Loaded;
            Unloaded += ProfileView_Unloaded;
        }

        private void ProfileView_Unloaded(object sender, RoutedEventArgs e)
        {
            DisplayService.Instance.IsProfileShown = false;
            eventAggregator.GetEvent<UpdatePopupProfileEvent>().Unsubscribe(UpdatePopupProfile);

            DisplayService.Instance.ProfileStartPoint = new IntPoint(-1, -1);
            DisplayService.Instance.ProfileEndPoint = new IntPoint(-1, -1);
            if (!DisplayService.Instance.IsColor)
            {
                DisplayService.Instance.ProfileMono = null;
            }
            else
            {
                DisplayService.Instance.ProfileR = null;
                DisplayService.Instance.ProfileG = null;
                DisplayService.Instance.ProfileB = null;
            }
            eventAggregator.GetEvent<UpdateProfileDrawingWidthEvent>().Publish(-1);
        }

        private void ProfileView_Loaded(object sender, RoutedEventArgs e)
        {
            this.Dispatcher.Invoke(() => UpdateChartVisiable());
            var channels = DisplayService.Instance.CurrentProcessImgChannels;
            UpdatePopupProfile(channels);
            eventAggregator = DisplayService.Instance.EventAggregator;
            eventAggregator.GetEvent<UpdatePopupProfileEvent>().Subscribe(UpdatePopupProfile, ThreadOption.BackgroundThread);
            ProfileLineWidth.Value = DisplayService.Instance.ProfileLineWidth;
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

        private void ProfileLineWidth_ValueChanged(object sender, Telerik.Windows.Controls.RadRangeBaseValueChangedEventArgs e)
        {
            if (e.NewValue == null)
            {
                ProfileLineWidth.Value = e.OldValue;
            }
            else if (e.NewValue != e.OldValue)
            {
                eventAggregator.GetEvent<UpdateProfileDrawingWidthEvent>().Publish((int)e.NewValue);
            }
        }

        private void ExportToExcel(DataTable dt, string fileName)
        {
            if (!fileName.EndsWith(".csv"))
            {
                fileName += ".csv";
            }

            if (File.Exists(fileName))
            {
                try
                {
                    File.Delete(fileName);
                }
                catch (Exception)
                {
                    eventAggregator.GetEvent<ThorCamStatusEvent>().Publish(new ThorCamStatusEventArgs(fileName, ErrorType.FileOccupied));
                    return;
                }
            }

            try
            {

                using (var fs = new FileStream(fileName, FileMode.Create, FileAccess.Write))
                {
                    using (var sw = new StreamWriter(fs, new UnicodeEncoding()))
                    {
                        for (var i = 0; i < dt.Columns.Count; i++)
                        {
                            sw.Write(dt.Columns[i].ColumnName);
                            sw.Write("\t");
                        }
                        sw.WriteLine("");

                        for (int i = 0; i < dt.Rows.Count; i++)
                        {
                            for (int j = 0; j < dt.Columns.Count; j++)
                            {
                                sw.Write(dt.Rows[i][j].ToString());
                                sw.Write("\t");
                            }
                            sw.WriteLine("");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                eventAggregator.GetEvent<ThorCamStatusEvent>().Publish(new ThorCamStatusEventArgs($"Profile, {fileName}", ErrorType.ExportFileFailed));
            }
            eventAggregator.GetEvent<ThorCamStatusEvent>().Publish(new ThorCamStatusEventArgs(ThorCamStatus.Export, new Exception($"Profile, {fileName}")));
        }

        private bool GenerateMonoTable(ref DataTable dt)
        {
            var line = DisplayService.Instance.ProfileMono.ToList();
            if (line == null || line.Count <= 0) return false;

            //string sLen = "Length";
            //if (DisplayService.Instance.IsPhysicalProfile)
            //    sLen += "(um)";
            //else
            //    sLen += "(px)";
            string sLen = "Length(px)";

            dt.Columns.Add(sLen);
            dt.Columns.Add("Intensity");
            var datalength = line.Count;

            for (var i = 0; i < datalength; i++)
            {
                var row = dt.NewRow();
                row[0] = line[i].XValue;
                row[1] = line[i].YValue;
                dt.Rows.Add(row);
            }
            return true;
        }

        private bool GenerateColorTable(ref DataTable dt)
        {
            List<Tuple<string, List<DoublePoint>>> lines = new List<Tuple<string, List<DoublePoint>>>();
            if (DisplayService.Instance.IsCheckedR)
                lines.Add(new Tuple<string, List<DoublePoint>>("R", DisplayService.Instance.ProfileR.ToList()));
            if (DisplayService.Instance.IsCheckedG)
                lines.Add(new Tuple<string, List<DoublePoint>>("G", DisplayService.Instance.ProfileG.ToList()));
            if (DisplayService.Instance.IsCheckedB)
                lines.Add(new Tuple<string, List<DoublePoint>>("B", DisplayService.Instance.ProfileB.ToList()));

            bool isAllowGenerate = false;
            foreach (var line in lines)
            {
                if (lines != null || lines.Count > 0)
                    isAllowGenerate = true;
            }
            if (!isAllowGenerate) return false;

            //For coloumn header
            //string sLen = "Length";
            //if (DisplayService.Instance.IsPhysicalProfile)
            //    sLen += "(um)";
            //else
            //    sLen += "(px)";
            string sLen = "Length(px)";

            dt.Columns.Add(sLen);
            int count = 1;
            int datalength = 0;
            int linesCount = lines.Count;
            for (int i = 0; i < linesCount; i++)
            {
                dt.Columns.Add("Intensity" + lines[i].Item1);
                count++;
                datalength = lines[i].Item2.Count;
            }

            for (var i = 0; i < datalength; i++)
            {
                var row = dt.NewRow();
                row[0] = lines[0].Item2[i].XValue;
                for (var j = 0; j < linesCount; j++)
                {
                    row[j + 1] = lines[j].Item2[i].YValue;
                }
                dt.Rows.Add(row);
            }

            return true;
        }

        private void GenerateExcel(string fileName)
        {
            var dt = new DataTable();
            var isAllowGenerateTable = false;
            if (!DisplayService.Instance.IsColor)
                isAllowGenerateTable = GenerateMonoTable(ref dt);
            else
                isAllowGenerateTable = GenerateColorTable(ref dt);

            if (isAllowGenerateTable)
                ExportToExcel(dt, fileName);
        }
        private void BT_Export_Click(object sender, RoutedEventArgs e)
        {
            var saveDialog = new SaveFileDialog
            {
                RestoreDirectory = true,
                Filter = "Csv File|*.csv"
            };
            if (saveDialog.ShowDialog() == true)
            {
                GenerateExcel(saveDialog.FileName);
            }
        }

        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            PxMonoTB.Visibility = Visibility.Collapsed;
            PxColorTB.Visibility = Visibility.Collapsed;
            UmMonoTB.Visibility = Visibility.Visible;
            UmColorTB.Visibility = Visibility.Visible;
        }

        private void CheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            PxMonoTB.Visibility = Visibility.Visible;
            PxColorTB.Visibility = Visibility.Visible;
            UmMonoTB.Visibility = Visibility.Collapsed;
            UmColorTB.Visibility = Visibility.Collapsed;
        }

        private void RadComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            var isMicro = (sender as RadComboBox).SelectedIndex == 0;
            if (isMicro)
            {
                DisplayService.Instance.IsPhysicalProfile = true;
                PxMonoTB.Visibility = Visibility.Collapsed;
                PxColorTB.Visibility = Visibility.Collapsed;
                UmMonoTB.Visibility = Visibility.Visible;
                UmColorTB.Visibility = Visibility.Visible;
            }
            else
            {
                DisplayService.Instance.IsPhysicalProfile = false;
                PxMonoTB.Visibility = Visibility.Visible;
                PxColorTB.Visibility = Visibility.Visible;
                UmMonoTB.Visibility = Visibility.Collapsed;
                UmColorTB.Visibility = Visibility.Collapsed;
            }
        }
    }
}

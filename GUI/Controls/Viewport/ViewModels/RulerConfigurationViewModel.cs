using DrawingTool.Factory;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using FilterWheelShared.Controls.DrawingTools.Factory.Materials;

namespace Viewport.ViewModels
{
    public class RulerConfigurationViewModel : BindableBase
    {
        private Dictionary<string, PropertyInfo> _properties = new Dictionary<string, PropertyInfo>();

        public PropertyInfo GetPropertyInfo(string propertyName)
        {
            PropertyInfo myPropInfo = null;
            if (!_properties.TryGetValue(propertyName, out myPropInfo))
            {
                myPropInfo = typeof(RulerConfigurationViewModel).GetProperty(propertyName);
                if (null != myPropInfo)
                {
                    _properties.Add(propertyName, myPropInfo);
                }
            }
            return myPropInfo;
        }

        public List<FontFamily> FontFamilyList { get; } = new List<FontFamily>();

        public CustomScalerViewModel Scaler { get; set; }

        private FontFamily _selectedFontFamily;
        public FontFamily SelectedFontFamily
        {
            get { return _selectedFontFamily; }
            set
            {
                if (SetProperty(ref _selectedFontFamily, value))
                {
                    Properties.RulerConfigurationSettings.Default.FontFamily = _selectedFontFamily;
                    Properties.RulerConfigurationSettings.Default.Save();
                    Scaler.FontFamily = _selectedFontFamily;
                }
            }
        }

        public List<double> FontSizeList { get; } = new List<double> { 8, 9, 10, 10.5, 11, 12, 14, 16, 18, 20, 24, 28, 32 };

        private double _selectedFontSize;
        public double SelectedFontSize
        {
            get { return _selectedFontSize; }
            set
            {
                if (SetProperty(ref _selectedFontSize, value))
                {
                    Properties.RulerConfigurationSettings.Default.FontSize = _selectedFontSize;
                    Properties.RulerConfigurationSettings.Default.Save();
                    Scaler.FontSize = _selectedFontSize;
                }
            }
        }

        private Color _fontColor = Colors.Black;
        public Color FontColor
        {
            get { return _fontColor; }
            set
            {
                if (SetProperty(ref _fontColor, value))
                {
                    Properties.RulerConfigurationSettings.Default.FontColor = _fontColor;
                    Properties.RulerConfigurationSettings.Default.Save();
                    Scaler.FontColor = _fontColor;
                }
            }
        }

        private int _lineWidth = 1;
        public int LineWidth
        {
            get { return _lineWidth; }
            set
            {
                if (SetProperty(ref _lineWidth, value))
                {
                    Properties.RulerConfigurationSettings.Default.LineWidth = _lineWidth;
                    Properties.RulerConfigurationSettings.Default.Save();
                    Scaler.LineWidth = _lineWidth;
                }
            }
        }

        private Color _lineColor = Colors.White;
        public Color LineColor
        {
            get { return _lineColor; }
            set
            {
                if (SetProperty(ref _lineColor, value))
                {
                    Properties.RulerConfigurationSettings.Default.LineColor = _lineColor;
                    Properties.RulerConfigurationSettings.Default.Save();
                    Scaler.LineColor = _lineColor;
                }
            }
        }

        private int _panelOpacity = 100;
        public int PanelOpacity
        {
            get { return _panelOpacity; }
            set
            {
                if (SetProperty(ref _panelOpacity, value))
                {
                    Properties.RulerConfigurationSettings.Default.PanelOpacity = _panelOpacity;
                    Properties.RulerConfigurationSettings.Default.Save();
                    Scaler.PanelOpacity = _panelOpacity;
                }
            }
        }

        private Color _panelColor = Colors.Gray;
        public Color PanelColor
        {
            get { return _panelColor; }
            set
            {
                if (SetProperty(ref _panelColor, value))
                {
                    Properties.RulerConfigurationSettings.Default.PanelColor = _panelColor;
                    Properties.RulerConfigurationSettings.Default.Save();
                    Scaler.PanelColor = _panelColor;
                }
            }
        }

        public List<RelativePlacement> AvailableRelateivePlacement { get; } = new List<RelativePlacement>();

        private RelativePlacement _scalerPlacement;
        public RelativePlacement ScalerPlacement
        {
            get { return _scalerPlacement; }
            set
            {
                if (SetProperty(ref _scalerPlacement, value))
                {
                    Properties.RulerConfigurationSettings.Default.ScalerPlacement = _scalerPlacement;
                    Properties.RulerConfigurationSettings.Default.Save();
                    Scaler.ScalerPlacement = _scalerPlacement;
                }
            }
        }

        public RulerConfigurationViewModel()
        {
            Scaler = ViewportViewModel.CustomScalerViewModelInstance;

            GetDesiredSystemFontFamily();

            //Load Parameters from Settings
            _selectedFontFamily = Properties.RulerConfigurationSettings.Default.FontFamily;
            _selectedFontSize = Properties.RulerConfigurationSettings.Default.FontSize;

            _fontColor = Properties.RulerConfigurationSettings.Default.FontColor;
            _lineWidth = Properties.RulerConfigurationSettings.Default.LineWidth;
            _lineColor = Properties.RulerConfigurationSettings.Default.LineColor;
            _panelOpacity = Properties.RulerConfigurationSettings.Default.PanelOpacity;
            _panelColor = Properties.RulerConfigurationSettings.Default.PanelColor;

            _scalerPlacement = Properties.RulerConfigurationSettings.Default.ScalerPlacement;

            // Get Available Ruler Placement.
            GetAvailableRelateivePlacement();
        }

        private void GetAvailableRelateivePlacement()
        {
            foreach (RelativePlacement _relativePlacement in Enum.GetValues(typeof(RelativePlacement)))
            {
                AvailableRelateivePlacement.Add(_relativePlacement);
            }
        }

        private void GetDesiredSystemFontFamily()
        {
            foreach (var fontfamily in Fonts.SystemFontFamilies)
            {
                var ffStr = fontfamily.ToString();
                if (DesiredFontFamilyList.Contains(ffStr))
                {
                    FontFamilyList.Add(fontfamily);
                }

            }
        }

        private static readonly List<string> DesiredFontFamilyList = new List<string>()
        {
            "Arial",
            "Bahnschrift",
            "Calibri",
            "Cambria",
            "Cambria Math",
            "Candara",
            "Comic Sans MS",
            "Consolas",
            "Constantia",
            "Corbel",
            "Courier New",
            "Ebrima",
            "Franklin Gothic",
            "Gabriola",
            "Gadugi",
            "Georgia",
            "Impact",
            "Ink Free",
            "Javanese Text",
            "Leelawadee UI",
            "Lucida Console",
            "Lucida Sans Unicode",
            "Malgun Gothic",
            "Microsoft Himalaya",
            "Microsoft JhengHei",
            "Microsoft JhengHei UI",
            "Microsoft New Tai Lue",
            "Microsoft PhagsPa",
            "Microsoft Sans Serif",
            "Microsoft Tai Le",
            "Microsoft YaHei",
            "Microsoft YaHei UI",
            "Microsoft Yi Baiti",
            "MingLiU-ExtB",
            "PMingLiU-ExtB",
            "MingLiU_HKSCS-ExtB",
            "Mongolian Baiti",
            "MS Gothic",
            "MS UI Gothic",
            "MS PGothic",
            "MV Boli",
            "Myanmar Text",
            "Nirmala UI",
            "Palatino Linotype",
            "Segoe Print",
            "Segoe Script",
            "Segoe UI",
            "Segoe UI Emoji",
            "Segoe UI Historic",
            "Segoe UI Symbol",
            "SimSun",
            "NSimSun",
            "SimSun-ExtB",
            "Sitka Small",
            "Sitka Text",
            "Sitka Subheading",
            "Sitka Heading",
            "Sitka Display",
            "Sitka Banner",
            "Sylfaen",
            "Tahoma",
            "Times New Roman",
            "Trebuchet MS",
            "Verdana",
            "Yu Gothic",
            "Yu Gothic UI",
            "DengXian",
            "FangSong",
            "KaiTi",
            "SimHei",
            "Quartz",
            "Global User Interface",
            "Global Monospace",
            "Global Sans Serif",
            "Global Serif",
        };
    }
}

using System;
using FilterWheelShared.ImageProcess;

namespace FilterWheelShared.Common
{
    public class StatisticItem : Prism.Mvvm.BindableBase, IComparable<StatisticItem>, IEquatable<StatisticItem>
    {
        public int Index { get; set; }
        public System.Windows.Media.SolidColorBrush ROIBrush { get; set; }
        public ChannelType ChannelType { get; set; }
        public bool IsChannelEnable { get; set; } = true;
        public bool IsSelected { get; set; }
        //For ROI
        public P2dRoiType ROIType { get; set; }
        public P2dRect ROIPixel { get; set; }
        public System.Windows.Rect ROIPhysical { get; set; }

        //For display
        private bool _show = false;
        public bool ShowIndexAndColor
        {
            get => _show;
            set => SetProperty(ref _show, value);
        }

        private double _area;
        public double Area
        {
            get => _area;
            set => SetProperty(ref _area, value);
        }

        private PhysicalUnit _unit = PhysicalUnit.um;
        public PhysicalUnit Unit
        {
            get => _unit;
            set => SetProperty(ref _unit, value);
        }

        private double _perimeter;
        public double Perimeter
        {
            get => _perimeter;
            set => SetProperty(ref _perimeter, value);
        }

        private ushort _min;
        public ushort Min
        {
            get => _min;
            set => SetProperty(ref _min, value);
        }

        private ushort _max;
        public ushort Max
        {
            get => _max;
            set => SetProperty(ref _max, value);
        }

        private double _mean;
        public double Mean
        {
            get => _mean;
            set => SetProperty(ref _mean, value);
        }

        private double _stdDev;
        public double StdDev
        {
            get => _stdDev;
            set => SetProperty(ref _stdDev, value);
        }

        public int CompareTo(StatisticItem other)
        {
            if (this.Index > other.Index)
                return 1;
            if (this.Index == other.Index)
            {
                if (this.ChannelType != ChannelType.Mono && other.ChannelType != ChannelType.Mono)
                {
                    if ((int)this.ChannelType > (int)other.ChannelType)
                        return 1;
                }
            }
            return -1;
        }

        public bool Equals(StatisticItem other)
        {
            return this.Index == other.Index && this.ChannelType == other.ChannelType && this.IsChannelEnable == other.IsChannelEnable;
        }
    }
}

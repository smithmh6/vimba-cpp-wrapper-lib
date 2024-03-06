using System.Windows;
using System.Windows.Controls;

namespace Viewport.Infrastructure
{
    public class ColorStyleSelector : StyleSelector
    {
        public Style ShowColorStyle { get; set; }
        public Style NotShowColorStyle { get; set; }

        public override Style SelectStyle(object item, DependencyObject container)
        {
            if (item is FilterWheelShared.Common.StatisticItem statistic)
                return statistic.ShowIndexAndColor ? ShowColorStyle : NotShowColorStyle;
            else return base.SelectStyle(item, container);
        }
    }
}

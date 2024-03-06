using System;
using System.ComponentModel;

namespace FilterWheelShared.Common
{
    [AttributeUsage(AttributeTargets.All)]
    public class DoubleDescriptionAttribute : DescriptionAttribute
    {
        public DoubleDescriptionAttribute(string description1, string description2)
        {
            this.Description1 = description1;
            this.Description2 = description2;
        }

        public string Description1 { get; set; }
        public string Description2 { get; set; }
    }
}

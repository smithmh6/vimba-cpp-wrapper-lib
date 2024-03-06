using System.ComponentModel;
using FilterWheelShared.Converter;

namespace FilterWheelShared.Common
{
    [TypeConverter(typeof(EnumDescriptionTypeConverter))]
    public enum SingleFrameFormat
    {
        [Description("jpg")]
        Jpeg = 2,
        [Description("single frame tif")]
        SingleFrameTif = 3,
    }

    [TypeConverter(typeof(EnumDescriptionTypeConverter))]
    public enum MultiFramesFormat
    {
        [Description("multi-frames tif")]
        MultiFramesTif = 0,
        [Description("mp4")]
        Avi = 1,
    }

    [TypeConverter(typeof(EnumDescriptionTypeConverter))]
    public enum ChannelType
    {
        [Description("R")]
        R,
        [Description("G")]
        G,
        [Description("B")]
        B,
        [Description("Mono")]
        Mono,
        COMBINE,
    }
}

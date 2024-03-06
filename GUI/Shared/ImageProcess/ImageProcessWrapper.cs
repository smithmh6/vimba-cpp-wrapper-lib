using System;
using System.Runtime.InteropServices;

namespace FilterWheelShared.ImageProcess
{
    public enum P2dDataFormat
    {
        P2D_8U = 1,
        P2D_16U = 2,
        P2D_16S = 3,
        P2D_32F = 4
    };

    public enum P2dChannels
    {
        P2D_CHANNELS_1 = 1,
        P2D_CHANNELS_3 = 3
    };

    [StructLayout(LayoutKind.Sequential)]
    public struct P2dRect
    {
        public int x;
        public int y;
        public int width;
        public int height;
    };

    [StructLayout(LayoutKind.Sequential)]
    public struct P2dPoint
    {
        public int x;
        public int y;
    };

    [StructLayout(LayoutKind.Sequential)]
    public struct P2dRegion
    {
        public int w;// width; 
        public int h;// height;
    };

    public enum P2dInterpolationType
    {
        P2D_Nearest = 1,
        P2D_Linear = 2,
        P2D_Cubic = 6,
        P2D_Lanczos = 16,
        P2D_Hahn = 0,
        P2D_Super = 8
    };

    public enum P2dAxis
    {
        P2D_AxsHorizontal,
        P2D_AxsVertical,
        P2D_AxsBoth,
    };

    [StructLayout(LayoutKind.Sequential)]
    public struct P2dInfo
    {
        public int x_size;
        public int y_size;
        public float x_physical_um;
        public float y_physical_um;
        public int line_bytes;
        public P2dDataFormat pix_type;
        public int valid_bits;
        public P2dChannels channels;
        public IntPtr data_buf;
    };

    public enum P2dRoiType
    {
        P2D_Rectangle = 0,
        P2D_Ellipse,
        P2D_Polygon
    };

    public enum P2dMorphShape
    {
        P2D_MORPH_RECT = 0,
        P2D_MORPH_CROSS = 1,
        P2D_MORPH_ELLIPSE = 2
    };

    public enum P2dMorphType
    {
        P2D_MORPH_ERODE = 0,
        P2D_MORPH_DILATE = 1,
        P2D_MORPH_OPEN = 2,
        P2D_MORPH_CLOSE = 3,

        //P2D_MORPH_GRADIENT = 4, 
        P2D_MORPH_TOPHAT = 5,
        P2D_MORPH_BLACKHAT = 6,
        //P2D_MORPH_HITMISS  = 7  
    };

    public enum P2dProjectionAxes
    {
        P2D_AXES_XY = 0,
        P2D_AXES_YZ,
        P2D_AXES_XZ
    };

    [StructLayout(LayoutKind.Sequential)]
    public struct P2dMorph
    {
        public P2dMorphShape shape;
        public P2dMorphType type;
        public uint mask_size_width;
        public uint mask_size_height;
        public double max_value;
    };


    public class P2DWrapper
    {
        private const string DllName = "p2d_lib.dll";

        [DllImport(DllName, EntryPoint = "p2d_create_image", CallingConvention = CallingConvention.Cdecl)]
        public static extern int CreateImage();

        [DllImport(DllName, EntryPoint = "p2d_init_image", CallingConvention = CallingConvention.Cdecl)]
        public static extern sbyte InitImage(int hdl, int x, int y, P2dDataFormat px_type, P2dChannels channels, int valid_bits);

        [DllImport(DllName, EntryPoint = "p2d_init_image_ex", CallingConvention = CallingConvention.Cdecl)]
        public static extern sbyte InitImageExternal(int hdl, ref P2dInfo info);

        [DllImport(DllName, EntryPoint = "p2d_get_info", CallingConvention = CallingConvention.Cdecl)]
        public static extern sbyte GetInfo(int hdl, ref P2dInfo info);

        [DllImport(DllName, EntryPoint = "p2d_get_buf", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr GetIntPtr(int hdl);

        [DllImport(DllName, EntryPoint = "p2d_get_data_type", CallingConvention = CallingConvention.Cdecl)]
        public static extern sbyte GetDataType(int hdl);

        [DllImport(DllName, EntryPoint = "p2d_clear", CallingConvention = CallingConvention.Cdecl)]
        public static extern sbyte Reset(int hdl);

        [DllImport(DllName, EntryPoint = "p2d_destroy_image", CallingConvention = CallingConvention.Cdecl)]
        public static extern sbyte FreeImage(int hdl);

        [DllImport(DllName, EntryPoint = "p2d_copy", CallingConvention = CallingConvention.Cdecl)]
        public static extern sbyte CopyImage(int hdlSrc, int hdlDst);

        //int8_t p2d_copy_color2channels(int hdl_src, int hdl_dst_array[3]);
        [DllImport(DllName, EntryPoint = "p2d_copy_color2channels", CallingConvention = CallingConvention.Cdecl)]
        public static extern sbyte CopyColorTo3Channels(int hdlSrc, int[] hdlDst);

        [DllImport(DllName, EntryPoint = "p2d_sum_rect", CallingConvention = CallingConvention.Cdecl)]
        public static extern sbyte SumRectImage(int hdlSrc, P2dRect rect, ref double sum);

        [DllImport(DllName, EntryPoint = "p2d_LShift", CallingConvention = CallingConvention.Cdecl)]
        public static extern sbyte ShiftLBuffer(int hdlSrc, int hdlDst, int nBits);

        [DllImport(DllName, EntryPoint = "p2d_RShift", CallingConvention = CallingConvention.Cdecl)]
        public static extern sbyte ShiftRBuffer(int hdlSrc, int hdlDst, int nBits);

        [DllImport(DllName, EntryPoint = "p2d_addI", CallingConvention = CallingConvention.Cdecl)]
        public static extern sbyte AddToImage(int hdlSrc, int hdlDst);

        [DllImport(DllName, EntryPoint = "p2d_mulCI", CallingConvention = CallingConvention.Cdecl)]
        public static extern sbyte Multiply(int hdl, double[] value);

        [DllImport(DllName, EntryPoint = "p2d_scale", CallingConvention = CallingConvention.Cdecl)]
        public static extern sbyte Scale(int hdSrc, int hdlDst, double mValue, double aValue);

        [DllImport(DllName, EntryPoint = "p2d_scaleI", CallingConvention = CallingConvention.Cdecl)]
        public static extern sbyte ScaleI(int hdl, double mValue, double aValue);
        [DllImport(DllName, EntryPoint = "p2d_adjust", CallingConvention = CallingConvention.Cdecl)]
        public static extern sbyte Adjust(int hdlSrc, int hdlDst, double[] min, double[] max);

        [DllImport(DllName, EntryPoint = "p2d_resize", CallingConvention = CallingConvention.Cdecl)]
        public static extern sbyte Resize(int hdlSrc, P2dPoint srcPoint, P2dRegion srcRegion, int hdlDst, P2dPoint dstPoint, P2dRegion dstRegion, P2dInterpolationType interprolationType = P2dInterpolationType.P2D_Lanczos);

        [DllImport(DllName, EntryPoint = "p2d_get_fast_profile", CallingConvention = CallingConvention.Cdecl)]
        public static extern sbyte P2dGetFastProfile16u(int hdl_src, P2dPoint startPoint, P2dPoint endPoint, uint line_width, ushort[] result, ref uint plength);
        [DllImport(DllName, EntryPoint = "p2d_get_fast_profile", CallingConvention = CallingConvention.Cdecl)]
        public static extern sbyte P2dGetFastProfile8u(int hdl_src, P2dPoint startPoint, P2dPoint endPoint, uint line_width, byte[] result, ref uint plength);

        [DllImport(DllName, EntryPoint = "p2d_get_profile", CallingConvention = CallingConvention.Cdecl)]
        public static extern sbyte GetProfile(int hdlSrc, uint xStart, uint yStart, uint xEnd, uint yEnd, uint lineWidth, ushort[] profile, ref uint profileLength);

        [DllImport(DllName, EntryPoint = "p2d_lnI", CallingConvention = CallingConvention.Cdecl)]
        public static extern sbyte Ln(int hdSrc);

        [DllImport(DllName, EntryPoint = "p2d_color_to_gray", CallingConvention = CallingConvention.Cdecl)]
        public static extern sbyte ColorToGray(int hdSrc, int hdDst);

        [DllImport(DllName, EntryPoint = "p2d_gray_to_color", CallingConvention = CallingConvention.Cdecl)]
        public static extern sbyte GrayToColor(int hdSrc, int hdDst);

        [DllImport(DllName, EntryPoint = "p2d_convert", CallingConvention = CallingConvention.Cdecl)]
        public static extern sbyte Convert(int hdSrc, int hdDst);

        [DllImport(DllName, EntryPoint = "p2d_histogram", CallingConvention = CallingConvention.Cdecl)]
        public static extern sbyte GetHistogram(int hdSrc, float lower_level, float upper_level, uint[] histData, uint histDataSize);

        [DllImport(DllName, EntryPoint = "p2d_copy_gray2color", CallingConvention = CallingConvention.Cdecl)]
        public static extern sbyte CopyGrayToColor(int hdl_src, int hdl_dst, int channel);

        [DllImport(DllName, EntryPoint = "p2d_otsu_get_threshold", CallingConvention = CallingConvention.Cdecl)]
        public static extern sbyte GetOtsuThreshold(int hdl_src, int[] pthresh);

        [DllImport(DllName, EntryPoint = "p2d_img_min_max", CallingConvention = CallingConvention.Cdecl)]
        public static extern sbyte GetMinMax_8u(int hdl_src, byte[] min, byte[] max);
        [DllImport(DllName, EntryPoint = "p2d_img_min_max", CallingConvention = CallingConvention.Cdecl)]
        public static extern sbyte GetMinMax_16u(int hdl_src, ushort[] min, ushort[] max);

        [DllImport(DllName, EntryPoint = "p2d_projection_mean", CallingConvention = CallingConvention.Cdecl)]
        public static extern sbyte ProjectionMean(int hdlSrc, int hdlDst, uint srcZ, uint sizeZ, P2dProjectionAxes dstAxes);

        [DllImport(DllName, EntryPoint = "p2d_computer_color_bc", CallingConvention = CallingConvention.Cdecl)]
        public static extern sbyte ComputerColorBC(int hdlSrc, int hdlDst, P2dRect srcRect, byte[] colorTable, double alpha, int beta);

        [DllImport(DllName, EntryPoint = "p2d_lut", CallingConvention = CallingConvention.Cdecl)]
        public static extern sbyte ColorLookUpTableNew(int hdlSrc, int hdlDst, byte[] colorTable);

        [DllImport(DllName, EntryPoint = "p2d_mirrorI", CallingConvention = CallingConvention.Cdecl)]
        public static extern sbyte MirrorI(int hdlSrc, P2dAxis flip);

        [DllImport(DllName, EntryPoint = "p2d_get_one_pixel", CallingConvention = CallingConvention.Cdecl)]
        public static extern sbyte GetOnePixel(int hdlSrc, P2dPoint index, IntPtr pixelValue);

        [DllImport(DllName, EntryPoint = "p2d_get_one_pixel", CallingConvention = CallingConvention.Cdecl)]
        public static extern sbyte GetOnePixel8U(int hdlSrc, P2dPoint index, out byte pixelValue);
        [DllImport(DllName, EntryPoint = "p2d_get_one_pixel", CallingConvention = CallingConvention.Cdecl)]
        public static extern sbyte GetOnePixel16U(int hdlSrc, P2dPoint index, out ushort pixelValue);

        [DllImport(DllName, EntryPoint = "p2d_generate_mask", CallingConvention = CallingConvention.Cdecl)]
        public static extern sbyte GenerateMask(int hdl_mask, P2dRoiType mask_type, P2dPoint[] point_list, int point_list_length);
        [DllImport(DllName, EntryPoint = "p2d_mask_mean_stddev", CallingConvention = CallingConvention.Cdecl)]
        public static extern sbyte MaskMeanStddev(int hdl_src, int hdl_mask, int x_offset, int yOffset, out double pmean, out double pstddev);
        [DllImport(DllName, EntryPoint = "p2d_mask_min_max", CallingConvention = CallingConvention.Cdecl)]
        public static extern sbyte MaskMinMax(int hdl_src, int hdl_mask, int x_offset, int y_offset, out float pmin, out float pmax);

    }

}

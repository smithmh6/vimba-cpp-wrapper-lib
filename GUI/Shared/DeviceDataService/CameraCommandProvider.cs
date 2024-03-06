using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using FilterWheelShared.Common;

namespace FilterWheelShared.DeviceDataService
{
    public enum ErrorCode
    {
        ERR_STATUS_OK = 0,
        ERR_UNKNOWN = -1,
        ERR_NOTSUPPORT = -2,

        ERR_CAMSYS_MALLOCBUFFER = -101,

        ERR_CAMSYS_MANUFACTURE = -201,
        ERR_CAMSYS_BITDEPTH_NOT_SUPPORT = -205,
        ERR_CAMSYS_COLORMODE_NOT_SUPPORT = -206,
        ERR_CAMSYS_SWTRIGGER_FAILED = -207,
        ERR_CAMSYS_CREATE_P2D_FAILED = -208,
        ERR_CAMSYS_INIT_P2D_FAILED = -209,
        ERR_CAMSYS_P2D_OPERATE_FAILED = -210,
        ERR_CAMSYS_P2D_GETINFO_FAILED = -211,
        ERR_CAMSYS_P2D_SETTAG_FAILED = -212,

        ERR_CREATE_JPEG_FILE_FALIED = -501,
        ERR_INIT_TURBOJPEG_COMPRESS_FAILED = -502,
        ERR_TIFF_HANDLE_NOT_VALID = -503,
        ERR_JPEG_FILE_NOT_VALID = -504,
        ERR_INIT_TURBOJPEG_DECOMPRESS_FAILED = -505,
    }

    public enum PixelTypes
    {
        PixelType_INT8 = 0,
        PixelType_UINT8 = 1,
        PixelType_UINT16 = 2,
        PixelType_INT16 = 3,
        PixelType_FLOAT = 4,
    }

    public enum ImageTypes
    {
        GRAY = 0,
        RGB = 1,
    }

    public enum CaptureSaveType
    {
        CaptrueMultiTif,
        CaptureVideo,
        CaptureJpeg,
        CaptureSingleTif,
    }


    public enum CompressionMode
    {
        COMPRESSION_NONE = 0,
        //COMPRESSION_LZ4 = 1,
        COMPRESSION_LZW = 2,
        COMPRESSION_JPEG = 3,
        COMPRESSION_ZIP = 4
    }

    public enum CameraTriggerMode
    {
        SoftwareTrigger,
        HarderwareTrigger
    }

    public enum AcquisitionMode
    {
        Continuous,
        Times,
    }

    public enum SerialBaudMode
    {
        Baud9600,
        Baud115200,
        Baud230400
    }

    [TypeConverter(typeof(Converter.EnumDescriptionTypeConverter))]
    public enum AcquisitionDelayUnit
    {
        [Description("m")]
        Minute,
        [Description("h")]
        Hour,
    }

    [TypeConverter(typeof(Converter.EnumDescriptionTypeConverter))]
    public enum TriggerMode
    {
        [Description("Internal Trigger")]
        InternalTrigger,
        [Description("SW Trigger")]
        SoftwareTrigger,
        [Description("HW Trigger First")]
        HardwareTriggerFirst,
        [Description("HW Trigger Each")]
        HardwareTriggerEach,
        [Description("Bulb")]
        HardwareTriggerBulb
    };

    public enum HardwareTriggerPolarity
    {
        [DoubleDescription("High Level", "Rising Edge")]
        TriggerPolarityActiveHigh,
        [DoubleDescription("Low Level", "Falling Edge")]
        TriggerPolarityActiveLow,
    }

    [TypeConverter(typeof(Converter.EnumDescriptionTypeConverter))]
    public enum PolarImageTypes
    {
        [Description("Unprocessed")]
        UNPROCESSED,
        [Description("Intensity")]
        TOTAL_OPTICAL_POWER,
        [Description("Azimuth")]
        AZIMUTH,
        [Description("DoLP")]
        DEGREE_OF_LINEAR_POLARIZATION,
        [Description("Quad View")]
        QUAD_VIEW
    }

    [TypeConverter(typeof(Converter.EnumDescriptionTypeConverter))]
    public enum ColorImageTypes
    {
        UNPROCESSED,
        RGB
    }

    public enum CorrectionMode
    {
        DPC,
        FPNC,
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct SnapshotInfo
    {
        public int slotIndex;
        public string fileName;
        public uint averageFrames;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public double[] min;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public double[] max;

        public SnapshotInfo(int slotIndex, string fileName, 
            uint averageFrames, double[] min, double[] max)
        {
            this.slotIndex = slotIndex;
            this.fileName = fileName;
            this.averageFrames = averageFrames;
            this.min = min;
            this.max = max;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CaptureSlotSetting
    {
        public double exposureTime;
        public double gain;
        public bool isAutoExposure;
        public bool isAutoGain;
        public CaptureSlotSetting(double exp, double gain, bool isAutoExp, bool isAutoGain)
        {
            this.exposureTime = exp;
            this.gain = gain;
            this.isAutoExposure = isAutoExp;
            this.isAutoGain = isAutoGain;
        }
        public CaptureSlotSetting(SlotSetting setting)
        {
            this.exposureTime = setting.ExposureTime;
            this.gain = setting.Gain;
            this.isAutoExposure = setting.IsAutoExposure;
            this.isAutoGain = setting.IsAutoGain;
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct CaptureInfo
    {
        public string folderName;
        public string prefixName;

        public CaptureSaveType saveType;
        public uint averageFrames;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public double[] min;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public double[] max;

        public IntPtr captureSlotSettings;
        public int captureSlotSettingsCount;
        public int currentSlotIndex;

        [MarshalAs(UnmanagedType.I1)]
        public bool isEnableCaptureImageUpdate;

        public CaptureInfo(string folderName, string prefixName,
            CaptureSaveType saveType, uint averageFrames,
            double[] min, double[] max, 
            List<CaptureSlotSetting> captureSlotSettings,
            int currentSlotIndex,
            bool isEnableCaptureImageUpdate = true)
        {
            this.folderName = folderName;
            this.prefixName = prefixName;

            this.averageFrames = averageFrames;
            this.saveType = saveType;

            this.min = min;
            this.max = max;

            IntPtr ptr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(CaptureSlotSetting)) * captureSlotSettings.Count);
            for (int i = 0; i < captureSlotSettings.Count; i++)
            {
                Marshal.StructureToPtr(captureSlotSettings[i], ptr + i * Marshal.SizeOf(typeof(CaptureSlotSetting)), false);
            }
            this.captureSlotSettings = ptr;
            this.captureSlotSettingsCount = captureSlotSettings.Count;

            this.currentSlotIndex = currentSlotIndex;
            this.isEnableCaptureImageUpdate = isEnableCaptureImageUpdate;
        }

        public void Dispose()
        {
            Marshal.FreeHGlobal(captureSlotSettings);
        }
    };

    [StructLayout(LayoutKind.Sequential)]
    public struct ImageInfo
    {
        public uint Width;
        public uint Height;
        public uint LineBytes;
        public PixelTypes PixelType;
        public ushort validBits;
        public ImageTypes ImageType;
        public CompressionMode CompressionMode;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct TiffImageSimpleInfo
    {
        public int p2d_img_hdl;
        public uint valid_bits;
        public uint slot_index;
    };

    [StructLayout(LayoutKind.Sequential)]
    public struct DoubleParams
    {
        public double min_value;
        public double max_value;
        public double increment;
        public DoubleParams(double min, double max, double inc)
        {
            this.min_value = min;
            this.max_value = max;
            this.increment = inc;
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void PreparedCallback();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void ImageCallback(int p2dImageHdl, uint frameNum, long frameClock, int CorrespondingSlotIndex, byte IsThumbnailNeed);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void DisconnectCallback();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void AutoExposureCallback(byte is_stable, double exposure);

    public static class CameraLIbCommand
    {
        private const string dllName = "CameraLib.dll";


        [DllImport(dllName, EntryPoint = "SMNC_List", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int List(StringBuilder cameraIds, int size);


        [DllImport(dllName, EntryPoint = "SMNC_Open", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int Open(string sn);

        [DllImport(dllName, EntryPoint = "SMNC_Close", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int Close();

        [DllImport(dllName, EntryPoint = "SMNC_GetFirmwareVersion", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int GetFirmwareVersion(StringBuilder firmwareVersion, int size);

        [DllImport(dllName, EntryPoint = "SMNC_GetModelName", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int GetModelName(StringBuilder modelName, int size);

        [DllImport(dllName, EntryPoint = "SMNC_Release", CallingConvention = CallingConvention.Cdecl)]
        public static extern int Release();

        [DllImport(dllName, EntryPoint = "SMNC_SetExposureTime", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int SetExposureTime(double exposure_us);

        [DllImport(dllName, EntryPoint = "SMNC_GetExposureTime", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int GetExposureTime(out double exposure_us);

        [DllImport(dllName, EntryPoint = "SMNC_GetExposureTimeRange", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int GetExposureTimeRange(ref DoubleParams exposure_us_params);

        [DllImport(dllName, EntryPoint = "SMNC_SetGain", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int SetGain(double gain);

        [DllImport(dllName, EntryPoint = "SMNC_GetGain", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int GetGain(out double gain);

        [DllImport(dllName, EntryPoint = "SMNC_GetGainRange", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int GetGainRange(out double gainMin, out double gainMax);

        [DllImport(dllName, EntryPoint = "SMNC_SetBlackLevel", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int SetBlackLevel(int blackLevel);

        [DllImport(dllName, EntryPoint = "SMNC_GetBlackLevel", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int GetBlackLevel(out int blackLevel);

        [DllImport(dllName, EntryPoint = "SMNC_GetBlackLevelRange", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int GetBlackLevelRange(out int min, out int max);

        [DllImport(dllName, EntryPoint = "SMNC_SetBinX", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int SetBinX(int xbin);

        [DllImport(dllName, EntryPoint = "SMNC_GetBinX", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int GetBinX(out int binx);

        [DllImport(dllName, EntryPoint = "SMNC_GetBinXRange", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int GetBinXRange(out int hbinMin, out int hbinMax);

        [DllImport(dllName, EntryPoint = "SMNC_SetBinY", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int SetBinY(int ybin);

        [DllImport(dllName, EntryPoint = "SMNC_GetBinY", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int GetBinY(out int biny);

        [DllImport(dllName, EntryPoint = "SMNC_GetBinYRange", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int GetBinYRange(out int vbinMin, out int vbinMax);

        [DllImport(dllName, EntryPoint = "SMNC_SetROI", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int SetROI(int x, int y, int width, int height);

        [DllImport(dllName, EntryPoint = "SMNC_GetROI", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int GetROI(out int x, out int y, out int width, out int height);

        [DllImport(dllName, EntryPoint = "SMNC_GetROIRange", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int GetROIRange(out int upperLeftXPixelsMin, out int upperLeftYPixelsMin, out int lowerRightXPixelsMin, out int lowerRightYPixelsMin,
            out int upperLeftXPixelsMax, out int upperLeftYPixelsMax, out int lowerRightXPixelsMax, out int lowerRightYPixelsMax);

        [DllImport(dllName, EntryPoint = "SMNC_StartPreview", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int StartPreview();

        [DllImport(dllName, EntryPoint = "SMNC_StopPreview", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int StopPreview();

        [DllImport(dllName, EntryPoint = "SMNC_StartSnapshot", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int StartSnapshot(SnapshotInfo info);

        [DllImport(dllName, EntryPoint = "SMNC_StopSnapshot", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int StopSnapshot();

        [DllImport(dllName, EntryPoint = "SMNC_StartCapture", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int StartCapture(CaptureInfo info, PreparedCallback callback);

        [DllImport(dllName, EntryPoint = "SMNC_StopCapture", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int StopCapture();

        [DllImport(dllName, EntryPoint = "SMNC_SetDisconnectCallback", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int SetDisconnectCallback(DisconnectCallback callback);

        [DllImport(dllName, EntryPoint = "SMNC_SetCallback", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int SetCallback(ImageCallback imageCallback, DisconnectCallback disconnectCallback, AutoExposureCallback autoExposureCallback);

        [DllImport(dllName, EntryPoint = "SMNC_GetBitDepth", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int GetBitDepth(out int bitDepth);

        //image related
        [DllImport(dllName, EntryPoint = "SMNC_GetImageCount", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        internal static extern int GetImageCount(string fileName, ref int imageHandle, ref uint imageCount);

        [DllImport(dllName, EntryPoint = "SMNC_GetImageData", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int GetImageData(int imageHandle, uint imageNumber, ref TiffImageSimpleInfo simpleInfo);

        [DllImport(dllName, EntryPoint = "SMNC_CloseImage", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int CloseImage(int imageHandle);

        [DllImport(dllName, EntryPoint = "SMNC_GetJpegData", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        internal static extern int GetJpegData(string fileName, ref int p2dImgHdl);

        [DllImport(dllName, EntryPoint = "SMNC_SaveImage", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        internal static extern int SaveImageData(string fileName, int p2dImgHdl, int slotIndex);


        [DllImport(dllName, EntryPoint = "SMNC_GetImageWidthAndHeight", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int GetImageWidthAndHeight(out int width, out int height);

        [DllImport(dllName, EntryPoint = "SMNC_GetColorMode", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int GetColorMode(out int colorType);

        [DllImport(dllName, EntryPoint = "SMNC_SetAutoExposure", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int SetAutoExposure(int enbale);

        [DllImport(dllName, EntryPoint = "SMNC_GetAutoExposure", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int GetAutoExposure(out int enbale);


        [DllImport(dllName, EntryPoint = "SMNC_GetFrameRateControlValueRange", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int GetFrameRateControlValueRange(out double min, out double max);
        [DllImport(dllName, EntryPoint = "SMNC_SetFrameRateControlValue", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int SetFrameRateControlValue(double framerateFps);
        [DllImport(dllName, EntryPoint = "SMNC_GetIsOperationModeSupported", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int GetIsOperationModeSupported(CameraTriggerMode mode, out int is_operation_mode_supported);


        [DllImport(dllName, EntryPoint = "SMNC_GetIsLEDSupported", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int GetIsLEDSupported(out int isLedSupported);
        [DllImport(dllName, EntryPoint = "SMNC_GetLEDStatus", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int GetLEDStatus(out int isLedOn);
        [DllImport(dllName, EntryPoint = "SMNC_SetLEDStatus", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int SetLEDStatus(int isLedOn);

        [DllImport(dllName, EntryPoint = "SMNC_SwitchAutoExposureStatus", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int SwitchAutoExposureStatus(bool isAutoExposure);

        [DllImport(dllName, EntryPoint = "SMNC_GetHotPixelCorrectionEnabled", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int GetHotPixelCorrectionEnabled(out int isEnabled);

        [DllImport(dllName, EntryPoint = "SMNC_SetHotPixelCorrectionEnabled", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int SetHotPixelCorrectionEnabled(int isEnabled);

        [DllImport(dllName, EntryPoint = "SMNC_GetHotPixelCorrectionThresholdRange", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int GetHotPixelCorrectionThresholdRange(out int min, out int max);

        [DllImport(dllName, EntryPoint = "SMNC_GetHotPixelCorrectionThreshold", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int GetHotPixelCorrectionThreshold(out int threshold);

        [DllImport(dllName, EntryPoint = "SMNC_SetHotPixelCorrectionThreshold", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int SetHotPixelCorrectionThreshold(int threshold);

        [DllImport(dllName, EntryPoint = "SMNC_GetSensorPixelWidth", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int GetSensorPixelWidth(out double pixelWidth);

        [DllImport(dllName, EntryPoint = "SMNC_GetSensorPixelHeight", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int GetSensorPixelHeight(out double pixelHeight);

        [DllImport(dllName, EntryPoint = "SMNC_GetCurrentPolarizationImageType", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int GetCurrentPolarImageType(out int polarizationImageType);

        [DllImport(dllName, EntryPoint = "SMNC_SetCurrentPolarizationImageType", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int SetCurrentPolarImageType(int polarizationImageType);


        [DllImport(dllName, EntryPoint = "SMNC_GetCorrectionModeEnabled", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int GetCorrectionModeEnabled([MarshalAs(UnmanagedType.I1)] out bool isEnabled);

        [DllImport(dllName, EntryPoint = "SMNC_SetCorrectionModeEnabled", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int SetCorrectionModeEnabled([MarshalAs(UnmanagedType.I1)] bool isEnabled);

        [DllImport(dllName, EntryPoint = "SMNC_GetCorrectionMode", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int GetCorrectionMode(out int mode);

        [DllImport(dllName, EntryPoint = "SMNC_SetCorrectionMode", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int SetCorrectionMode(int mode);

        [DllImport(dllName, EntryPoint = "SMNC_GetGainAutoEnabled", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int GetGainAutoEnabled([MarshalAs(UnmanagedType.I1)] out bool isEnabled);

        [DllImport(dllName, EntryPoint = "SMNC_SetGainAutoEnabled", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int SetGainAutoEnabled([MarshalAs(UnmanagedType.I1)] bool isEnabled);

        [DllImport(dllName, EntryPoint = "SMNC_GetExposureAutoEnabled", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int GetExposureAutoEnabled(out int mode);

        [DllImport(dllName, EntryPoint = "SMNC_SetExposureAutoEnabled", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int SetExposureAutoEnabled(int mode);

        [DllImport(dllName, EntryPoint = "SMNC_GetReverseXEnabled", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int GetReverseXEnabled([MarshalAs(UnmanagedType.I1)] out bool isEnabled);

        [DllImport(dllName, EntryPoint = "SMNC_SetReverseXEnabled", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int SetReverseXEnabled([MarshalAs(UnmanagedType.I1)] bool isEnabled);

        [DllImport(dllName, EntryPoint = "SMNC_GetReverseYEnabled", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int GetReverseYEnabled([MarshalAs(UnmanagedType.I1)] out bool isEnabled);

        [DllImport(dllName, EntryPoint = "SMNC_SetReverseYEnabled", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int SetReverseYEnabled([MarshalAs(UnmanagedType.I1)] bool isEnabled);

        // Serial commands
        [DllImport(dllName, EntryPoint = "SMNC_SetSerialHubEnabled", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int SetSerialHubEnabled([MarshalAs(UnmanagedType.I1)] bool isEnabled);

        [DllImport(dllName, EntryPoint = "SMNC_GetSerialHubEnabled", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int GetSerialHubEnabled([MarshalAs(UnmanagedType.I1)] out bool isEnabled);

        [DllImport(dllName, EntryPoint = "SMNC_JogClockwise", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int JogClockwise();

        [DllImport(dllName, EntryPoint = "SMNC_JogCounterClockwise", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int JogCounterClockwise();
       
        [DllImport(dllName, EntryPoint = "SMNC_JogToSlot", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int JogToSlot(int targetSlotIndex);
        
        [DllImport(dllName, EntryPoint = "SMNC_IsMoving", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int IsMoving();

        [DllImport(dllName, EntryPoint = "SMNC_StopMotion", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int StopMotion();

        [DllImport(dllName, EntryPoint = "SMNC_MoveConstant", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int MoveConstant();

        [DllImport(dllName, EntryPoint = "SMNC_FindHomePosition", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int FindHomePosition();
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MEMORY_INFO
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong dwTotalPhys;
        public ulong dwAvailPhys;
        public ulong dwTotalPageFile;
        public ulong dwAvailPageFile;
        public ulong dwTotalVirtual;
        public ulong dwAvailVirtual;
    }

    public class UnsafeNativeMethods
    {
        [DllImport("kernel32")]
        internal static extern void GlobalMemoryStatus(ref MEMORY_INFO meminfo);
    }

}

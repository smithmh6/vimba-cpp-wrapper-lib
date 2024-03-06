#pragma once
#ifdef DLL_EXPORT
#define DLLAPI __declspec(dllexport)
#else
#define DLLAPI __declspec(dllimport)
#endif

#ifdef __cplusplus
extern "C"
{
#endif

	enum class OperateMode
	{
		SoftwareTrigger,
		HardwareTrigger
	};

	enum class TriggerMode
	{
		InternalTrigger,
		SoftwareTrigger,
		HardwareTriggerFirst,
		HardwareTriggerEach,
		HardwareBulb
	};

	enum class HardwarePolarity
	{
		TriggerPolarityActiveHigh,
		TriggerPolarityActiveLow,
	};

	enum class CaptureSaveType
	{
		CaptrueMultiTif,
		CaptureVideo,
		CaptureJpeg,
		CaptureSingleTif
	};
	
	enum class SerialBaudMode
	{
		Baud9600,
		Baud115200,
		Baud230400
	};

	enum class AcquisitionMode
	{
		Continuous,
		Times,
	};

	enum class AcquisitionDelayUnit
	{
		Minute,
		Hour,
	};

	struct CaptureSlotSetting
	{
		double exposureTime;
		double gain;
		bool isAutoExposure;
		bool isAutoGain;
	};

	struct CaptureInfo
	{
		const wchar_t* folderName;
		const wchar_t* prefixName;

		CaptureSaveType saveType;
		unsigned int averageFrames;

		double min[3];
		double max[3];

		CaptureSlotSetting* captureSlotSettings;
		int captureSlotSettingsCount;
		int currentSlotIndex;

		bool isEnableCaptureImageUpdate;
	};

	struct SnapshotInfo
	{
		int slotIndex;
		const wchar_t* fileName;
		unsigned int averageFrames;

		double min[3];
		double max[3];
	};

	struct ImageInfo
	{
		unsigned int width;
		unsigned int height;
		unsigned int line_bytes;
		int pixel_type;
		unsigned short valid_bits;
		int image_type;
		int compression_mode;

		int slot_index;
	};

	struct TiffImageSimpleInfo
	{
		int p2d_img_hdl;
		unsigned int valid_bits;
		unsigned int slot_index;
	};

	struct DoubleParams
	{
		double min_value;
		double max_value;
		double increment;
	};

	typedef void(__cdecl* PreparedCallback)();
	typedef void(__cdecl* ImageCallback)(int p2d_img_hdl, unsigned int frameNum, long long frameClock, int CorrespondingSlotIndex, unsigned char IsThumbnailNeed);
	typedef void(__cdecl* DisconnectCallback)();
	typedef void(__cdecl* AutoExposureCallback)(unsigned char is_stable, double exposure);

	DLLAPI int SMNC_Release();
	DLLAPI int SMNC_List(char *buffer, int bufferLength);
	DLLAPI int SMNC_Open(const char *serialNumber);
	DLLAPI int SMNC_Close();

	DLLAPI int SMNC_GetFirmwareVersion(char* firmware_version, int str_length);
	DLLAPI int SMNC_GetModelName(char* model_name, int str_length);
	DLLAPI int SMNC_SetCallback(ImageCallback image_callback, DisconnectCallback disconnect_callback, AutoExposureCallback auto_exposure_callback);
	DLLAPI int SMNC_StartPreview();
	DLLAPI int SMNC_StopPreview();
	DLLAPI int SMNC_StartSnapshot(SnapshotInfo info);
	DLLAPI int SMNC_StopSnapshot();
	DLLAPI int SMNC_StartCapture(CaptureInfo info, PreparedCallback callback);
	DLLAPI int SMNC_StopCapture();

	//image related
	DLLAPI int SMNC_GetImageCount(const wchar_t* file_name, int* tiff_handle, unsigned int* image_counut);
	DLLAPI int SMNC_GetImageData(int tiff_handle, unsigned int image_number, TiffImageSimpleInfo* simple_info);
	DLLAPI int SMNC_CloseImage(int tiff_handle);
	DLLAPI int SMNC_GetJpegData(const wchar_t* file_name, int* p2d_img_hdl);
	DLLAPI int SMNC_SaveImage(const wchar_t* file_name, int p2d_img_hdl, int slotIndex);

	//Others settings that will not effect the capture logic. e.g. ExposureTime
	DLLAPI int SMNC_SetExposureTime(double exposure_us);
	DLLAPI int SMNC_GetExposureTime(double* exposure_us);
	DLLAPI int SMNC_GetExposureTimeRange(DoubleParams* exposure_us_params);

	DLLAPI int SMNC_SetGain(double gain);
	DLLAPI int SMNC_GetGain(double* gain);
	DLLAPI int SMNC_GetGainRange(double* gain_min, double* gain_max);

	DLLAPI int SMNC_SetBlackLevel(int black_level);
	DLLAPI int SMNC_GetBlackLevel(int* black_level);
	DLLAPI int SMNC_GetBlackLevelRange(int* black_level_min, int* black_level_max);

	DLLAPI int SMNC_SetBinX(int binx);
	DLLAPI int SMNC_GetBinX(int* binx);
	DLLAPI int SMNC_GetBinXRange(int* binx_min, int* binx_max);

	DLLAPI int SMNC_SetBinY(int biny);
	DLLAPI int SMNC_GetBinY(int* biny);
	DLLAPI int SMNC_GetBinYRange(int* biny_min, int* biny_max);

	DLLAPI int SMNC_SetROI(int x, int y, int width, int height);
	DLLAPI int SMNC_GetROI(int* x, int* y, int* width, int* height);
	DLLAPI int SMNC_GetROIRange(int* upper_left_x_min, int* upper_left_y_min, int* lower_right_x_min, int* lower_right_y_min, int* upper_left_x_max, int*  upper_left_y_max, int* lower_right_x_max, int* lower_right_y_max);

	DLLAPI int SMNC_GetBitDepth(int* bit_depth);
	DLLAPI int SMNC_GetImageWidthAndHeight(int* width, int* height);

	DLLAPI int SMNC_GetColorMode(int* color_type);

	DLLAPI int SMNC_SetAutoExposure(int enable);
	DLLAPI int SMNC_GetAutoExposure(int *enable);

	DLLAPI int SMNC_GetFrameRateControlValueRange(double* min, double*max);
	DLLAPI int SMNC_SetFrameRateControlValue(double frame_rate_fps);
	DLLAPI int SMNC_GetIsOperationModeSupported(OperateMode mode, int *is_operation_mode_supported);

	DLLAPI int SMNC_GetIsLEDSupported(int* is_led_supported);
	DLLAPI int SMNC_GetLEDStatus(int* is_led_on);
	DLLAPI int SMNC_SetLEDStatus(int is_led_on);

	DLLAPI int SMNC_SwitchAutoExposureStatus(bool isAutoExposure);

	DLLAPI int SMNC_GetHotPixelCorrectionEnabled(int* is_enabled);
	DLLAPI int SMNC_SetHotPixelCorrectionEnabled(int is_enabled);
	DLLAPI int SMNC_GetHotPixelCorrectionThresholdRange(int* min, int* max);	
	DLLAPI int SMNC_GetHotPixelCorrectionThreshold(int* threshold);	
	DLLAPI int SMNC_SetHotPixelCorrectionThreshold(int threshold);
	DLLAPI int SMNC_GetSensorPixelWidth(double* pixel_width);
	DLLAPI int SMNC_GetSensorPixelHeight(double* pixel_height);

	DLLAPI int SMNC_SetCurrentPolarizationImageType(int polar_image_type);
	DLLAPI int SMNC_GetCurrentPolarizationImageType(int* polar_image_type);
	DLLAPI int SMNC_SetCurrentColorImageType(int color_image_type);
	DLLAPI int SMNC_GetCurrentColorImageType(int* color_image_type);


	DLLAPI int SMNC_GetCorrectionModeEnabled(bool* is_enabled);
	DLLAPI int SMNC_SetCorrectionModeEnabled(bool is_enabled);

	DLLAPI int SMNC_GetCorrectionMode(int* Mode);
	DLLAPI int SMNC_SetCorrectionMode(int Mode);

	DLLAPI int SMNC_GetGainAutoEnabled(bool* is_enabled);
	DLLAPI int SMNC_SetGainAutoEnabled(bool is_enabled);

	DLLAPI int SMNC_GetExposureAutoEnabled(int* is_enabled);
	DLLAPI int SMNC_SetExposureAutoEnabled(int is_enabled);

	DLLAPI int SMNC_GetReverseXEnabled(bool* is_enabled);
	DLLAPI int SMNC_SetReverseXEnabled(bool is_enabled);

	DLLAPI int SMNC_GetReverseYEnabled(bool* is_enabled);
	DLLAPI int SMNC_SetReverseYEnabled(bool is_enabled);


	// serial commands
	DLLAPI int SMNC_SetSerialHubEnabled(bool is_enabled);
	DLLAPI int SMNC_GetSerialHubEnabled(bool* is_enabled);
	DLLAPI int SMNC_JogClockwise();
	DLLAPI int SMNC_JogCounterClockwise();
	DLLAPI int SMNC_JogToSlot(int targetSlotIndex);
	DLLAPI int SMNC_IsMoving(bool* is_moving);
	DLLAPI int SMNC_StopMotion();
	DLLAPI int SMNC_FindHomePosition();

	/* NOTE: Need to determine how to send speed as parameter */
	DLLAPI int SMNC_MoveConstant();

	// DLLAPI int SMNC_SetSerialBaudRate(SerialBaudMode baud_mode);
	// DLLAPI int SMNC_GetSerialBaudRate(SerialBaudMode* baud_mode);
	// DLLAPI int SMNC_SetSerialTxSize(int n_bytes);
	// DLLAPI int SMNC_SetSerialTxData(unsigned char* tx_data);
	// DLLAPI int SMNC_SetSerialTxLockEnabled(int is_enabled);
	// DLLAPI int SMNC_GetSerialTxLockEnabled(int* is_enabled);
	// DLLAPI int SMNC_SetSerialRxSize(int n_bytes);
	// DLLAPI int SMNC_GetSerialRxWaiting(int* n_bytes);
	// DLLAPI int SMNC_GetSerialRxData(unsigned char* rx_data);
	
#ifdef __cplusplus
}
#endif
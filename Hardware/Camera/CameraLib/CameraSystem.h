#pragma once
#include <thread>
#include <queue>
#include <atomic>
#include <vector>
#include "classic_tiff_library.h"
#include "p2d_lib.h"
#include "CameraLib.h"
#include "..\AlliedCamera\AlliedCamera.h"
//#include "..\video_process_lib\src\video_process_lib.h"

#ifndef CHECK_STATUS
#define CHECK_STATUS(status) { auto result = status; if(result != 0) return result; }
#endif

struct CameraSystemFrameInfo
{
	int p2d_img_hdl;
	unsigned int frame_num;
	long long frame_clock;
};

typedef void(__cdecl* PreparedCallback)();
typedef void(__cdecl* ImageCallback)(int p2d_img_hdl, unsigned int frameNum, long long frameClock, int CorrespondingSlotIndex, unsigned char IsThumbnailNeed);
typedef void(__cdecl* DisconnectCallback)();
typedef void(__cdecl* AutoExposureCallback)(unsigned char is_stable, double exposure);

enum class CameraType
{
	CamMono,
	CamColorBayer,
	CamPolar,
	CamColor
};

struct CaptureInfoEx
{
	unsigned int frameCount;
	CaptureInfo* baseInfo;
	CaptureInfoEx(CaptureInfo* info)
	{
		baseInfo = new CaptureInfo();
		memcpy(baseInfo, info, sizeof(CaptureInfo));
		frameCount = baseInfo->averageFrames * baseInfo->captureSlotSettingsCount;
	}
};

class CameraSystem
{
	std::string _sn;
	enum CameraType m_cameraType;
	bool m_isFrameRateControlSupport;
	bool m_isHardwareTriggerSupport;

	DisconnectCallback m_disconnectCallback;
	ImageCallback m_imageCallback;
	AutoExposureCallback m_autoExposureCallback;

	unsigned int m_callbackFrame;
	unsigned int m_processedFrameCount;
	unsigned int m_savedFrameCount;

	CaptureInfoEx* m_pCaptureInfo;
	long long m_preFrameTime;
	std::atomic<bool> m_callbackArrrived;
	int m_hdl_AutoExposuring;
	std::atomic<bool> m_isAutoExposuring;
	std::atomic<bool> m_isAutoImageReady;

	//long test_call_back_time = 0;
	//bool m_isDisconnected = false;

	SnapshotInfo* m_pSnapshotInfo;
	bool m_isLiving;
	bool m_isSnapshoting;
	std::atomic<bool> m_isCapturing;
	std::atomic<int> m_CaptureErrorCode;

	AlliedCamera* m_pCamera;

	//backup for image callback from camera
	int m_backup_p2d_img_handle;
	p2d_info* m_backup_p2d_img_info;

	//variables for average
	int m_average_temp_handle = -1;

	//variables for bayer
	//int m_bayer_temp_16u1c_handle;
	//int m_bayer_temp_8u3c_handle;
	//unsigned short* m_bayer_temp_16u3c_buffer;

	//variables for polar 
	enum PolarImageType current_polar_image_type_selection = (PolarImageType)0;
	//int m_polar_temp_16u1c_handle;
	//int m_polar_temp_processed_16u1c_handle;

	//For video saving
	//bool m_video_started = false;
	//vpl_data frameData = { 0 };
	//uint64_t m_video_frame_duration;

	//For multiframe-tif saving
	int m_multi_tif_hdl = -1;

	std::thread* thread_deal;
	std::thread* thread_proc;
	std::thread* thread_auto_exposure;

	std::queue<CameraSystemFrameInfo> m_callback_image_queue;
	std::queue<CameraSystemFrameInfo> m_processed_image_queue;

	std::queue<int> m_callback_transfer_p2d_handle_queue;

	static CameraSystem* s_currentCamera;

	void ProcessCaptureImages();
	void ProcessLiveImages();
	void ProcessSnapshotImage();

	void DealProcessedImages();
	int AutoExposure();
	//int ProcessOriginalDataCamColorBayer(int* hdl, p2d_info* info);
	//int ProcessOriginalDataCamPolar(int* hdl, p2d_info* info);

	static void CameraCallback(void* buffer, unsigned int frameNum, int width, int height);

	long PreExecutionCore(unsigned int averageFrames = 0);
	long PostExecutionCore();

	long PreCapture(CaptureInfo* info);
	long PostCapture(CaptureInfo* info);

	//int StartCapture_InteralTrigger(CaptureInfo info);
	int StartCapture_SoftwareTrigger();
	//int StartCapture_HardwareTriggerFirst(CaptureInfo info);
	//int StartCapture_HardwareTriggerEach(CaptureInfo info);
	//int StartCapture_HardwareBulb(CaptureInfo info);
	int StartLive_Software();

	static int CalculateNextExposure(int hdl_calc, double preExposureTime, double* nextExposureTime, double exposureMin, double exposureMax);

	static long LoadImageCount(const wchar_t* file_name, int* tiff_handle, unsigned int* image_count);
	static long LoadImageInfo(int tiff_handle, unsigned int frame_number, ImageInfo* image_info);
	static long SaveImage(int tiff_handle, int p2d_img_hdl, ImageInfo* image_info);

	////Video save
	//int VideoSaving_Start(uint32_t width, uint32_t height, p2d_channels colorType, uint32_t fps, const wchar_t* path);
	//int VideoSaving_SingleFrame(int hdl_img, uint64_t video_frame_duration);
	//int VideoSaving_Stop();
	static void CameraDisconnectCallback(const char* sn);

public:
	CameraSystem();
	~CameraSystem();

	static int Init();
	static int Release();
	static int List(char* buffer, int bufferLength);

	int Open(const char* serialNumber);
	int Close();

	int StartCapture(CaptureInfo info, PreparedCallback callback);
	int StopCapture();

	int StartLive();
	int StopLive();

	int StartSnapshot(SnapshotInfo info);
	int StopSnapshot();

	int AutoExposureStatusChange(bool isAutoExposure);

	inline int GetFirmwareVersion(char* firmware_version, int str_length)
	{
		return m_pCamera->GetFirmware(firmware_version, str_length);
	}

	inline int GetModelName(char* model_name, int str_length)
	{
		return m_pCamera->GetModelName(model_name, str_length);
	}

	inline void SetCallback(ImageCallback image_callback, DisconnectCallback disconnect_callback, AutoExposureCallback auto_exposure_callback)
	{
		m_imageCallback = image_callback;
		m_disconnectCallback = disconnect_callback;
		m_autoExposureCallback = auto_exposure_callback;
	}

	inline int SetExposureTime(double exposure_us)
	{
		return m_pCamera->SetExposureTime(exposure_us);
	}
	inline int GetExposureTime(double* exposure_us)
	{
		return m_pCamera->GetExposureTime(exposure_us);
	}
	inline int GetExposureTimeRange(DoubleParams* exposure_us_params)
	{
		return m_pCamera->GetExposureTimeRange(&exposure_us_params->min_value, &exposure_us_params->max_value, &exposure_us_params->increment);
	}

	inline int SetGain(double gain)
	{
		return m_pCamera->SetGain(gain);
	}
	inline int GetGain(double* gain)
	{
		return m_pCamera->GetGain(gain);
	}
	inline int GetGainRange(double* gain_min, double* gain_max)
	{
		return m_pCamera->GetGainRange(gain_min, gain_max);
	}

	inline int SetBlackLevel(int black_level)
	{
		return m_pCamera->SetBlackLevel(black_level);
	}
	inline int GetBlackLevel(int* black_level)
	{
		return m_pCamera->GetBlackLevel(black_level);
	}
	inline int GetBlackLevelRange(int* min, int* max)
	{
		return m_pCamera->GetBlackLevelRange(min, max);
	}

	inline int SetBinX(int binx)
	{
		return m_pCamera->SetBinX(binx);
	}
	inline int GetBinX(int* binx)
	{
		return m_pCamera->GetBinX(binx);
	}
	inline int GetBinXRange(int* binx_min, int* binx_max)
	{
		return m_pCamera->GetBinXRange(binx_min, binx_max);
	}

	inline int SetBinY(int biny)
	{
		return m_pCamera->SetBinY(biny);
	}
	inline int GetBinY(int* biny)
	{
		return m_pCamera->GetBinY(biny);
	}
	inline int GetBinYRange(int* biny_min, int* biny_max)
	{
		return m_pCamera->GetBinYRange(biny_min, biny_max);
	}

	inline int SetROI(int x, int y, int width, int height)
	{
		return m_pCamera->SetROI(x, y, width, height);
	}
	inline int GetROI(int* x, int* y, int* width, int* height)
	{
		return m_pCamera->GetROI(x, y, width, height);
	}

	inline int GetROIRange(int* upper_left_x_pixels_min, int* upper_left_y_pixels_min, int* lower_right_x_pixels_min, int* lower_right_y_pixels_min, int* upper_left_x_pixels_max, int* upper_left_y_pixels_max, int* lower_right_x_pixels_max, int* lower_right_y_pixels_max)
	{
		return m_pCamera->GetROIRange(upper_left_x_pixels_min, upper_left_y_pixels_min, lower_right_x_pixels_min, lower_right_y_pixels_min, upper_left_x_pixels_max, upper_left_y_pixels_max, lower_right_x_pixels_max, lower_right_y_pixels_max);
	}

	inline int GetSensorWidthAndHeight(int* width, int* height)
	{
		return m_pCamera->GetSensorWidth(width) | m_pCamera->GetSensorHeight(height);
	}

	inline int GetImageWidthAndHeight(int* width, int* height)
	{
		return m_pCamera->GetImageWidthAndHeight(width, height);
	}

	inline int GetBitDepth(int* bit_depth)
	{
		return m_pCamera->GetBitDepth(bit_depth);
	}

	inline int GetColorMode(int* color_type)
	{
		return m_pCamera->GetColorMode(color_type);
	}

	inline int SetAutoExposure(int enable)
	{
		return m_pCamera->SetAutoExposure(enable);
	}

	inline int GetAutoExposure(int* enable)
	{
		return m_pCamera->GetAutoExposure(enable);
	}


	inline int GetFrameRateControlValueRange(double* min, double* max)
	{
		return m_pCamera->GetFrameRateControlValueRange(min, max);
	}

	inline int SetFrameRateControlValue(double frame_rate_fps)
	{
		return m_pCamera->SetFrameRateControlValue(frame_rate_fps);
	}

	inline int GetIsOperationModeSupported(CameraTriggerMode mode, int* is_operation_mode_supported)
	{
		return m_pCamera->GetIsOperationModeSupported(mode, is_operation_mode_supported);
	}

	inline int GetIsLEDSupported(int* is_led_supported)
	{
		return m_pCamera->GetIsLEDSupported(is_led_supported);
	}

	inline int GetLEDStatus(int* is_led_on)
	{
		return m_pCamera->GetLEDStatus(is_led_on);
	}

	inline int SetLEDStatus(int is_led_on)
	{
		return m_pCamera->SetLEDStatus(is_led_on);
	}

	inline int GetHotPixelCorrectionEnabled(int* is_enabled)
	{
		return m_pCamera->GetHotPixelCorrectionEnabled(is_enabled);
	}

	inline int SetHotPixelCorrectionEnabled(int is_enabled)
	{
		return m_pCamera->SetHotPixelCorrectionEnabled(is_enabled);
	}

	inline int GetHotPixelCorrectionThresholdRange(int* min, int* max)
	{
		return m_pCamera->GetHotPixelCorrectionThresholdRange(min, max);
	}

	inline int GetHotPixelCorrectionThreshold(int* threshold)
	{
		return m_pCamera->GetHotPixelCorrectionThreshold(threshold);
	}

	inline int SetHotPixelCorrectionThreshold(int threshold)
	{
		return m_pCamera->SetHotPixelCorrectionThreshold(threshold);
	}

	inline int GetSensorPixelWidth(double* pixel_width)
	{
		return m_pCamera->GetSensorPixelWidth(pixel_width);
	}

	inline int GetSensorPixelHeight(double* pixel_height)
	{
		return m_pCamera->GetSensorPixelHeight(pixel_height);
	}



	inline int GetCorrectionModeEnabled(bool* is_enabled)
	{
		return m_pCamera->GetCorrectionModeEnabled(is_enabled);
	}
	inline int SetCorrectionModeEnabled(bool is_enabled)
	{
		return m_pCamera->SetCorrectionModeEnabled(is_enabled);
	}

	inline int GetCorrectionMode(int* mode)
	{
		return m_pCamera->GetCorrectionMode(mode);
	}
	inline int SetCorrectionMode(int mode)
	{
		return m_pCamera->SetCorrectionMode(mode);
	}

	inline int GetGainAutoEnabled(bool* is_enabled)
	{
		return m_pCamera->GetGainAutoEnabled(is_enabled);
	}
	inline int SetGainAutoEnabled(bool is_enabled)
	{
		return m_pCamera->SetGainAutoEnabled(is_enabled);
	}

	inline int GetExposureAutoEnabled(int* is_enabled)
	{
		return m_pCamera->GetExposureAutoEnabled(is_enabled);
	}
	inline int SetExposureAutoEnabled(int is_enabled)
	{
		return m_pCamera->SetExposureAutoEnabled(is_enabled);
	}

	inline int GetReverseXEnabled(bool* is_enabled)
	{
		return m_pCamera->GetReverseXEnabled(is_enabled);
	}
	inline int SetReverseXEnabled(bool is_enabled)
	{
		return m_pCamera->SetReverseXEnabled(is_enabled);
	}

	inline int GetReverseYEnabled(bool* is_enabled)
	{
		return m_pCamera->GetReverseYEnabled(is_enabled);
	}
	inline int SetReverseYEnabled(bool is_enabled)
	{
		return m_pCamera->SetReverseYEnabled(is_enabled);
	}


	// Serial Commands
	inline int SetSerialHubEnabled(bool is_enabled)
	{
		return m_pCamera->SetSerialHubEnabled(is_enabled);
	}

	inline int GetSerialHubEnabled(bool* is_enabled)
	{
		return m_pCamera->GetSerialHubEnabled(is_enabled);
	}

	inline int SetSerialBaudRate(SerialBaudRate baud_rate)
	{
		return m_pCamera->SetSerialBaudRate(baud_rate);
	}

	inline int GetSerialBaudRate(SerialBaudRate* baud_rate)
	{
		return m_pCamera->GetSerialBaudRate(baud_rate);
	}

	inline int SetSerialTxSize(int n_bytes)
	{
		return m_pCamera->SetSerialTxSize(n_bytes);
	} 

	inline int SetSerialTxData(unsigned char* tx_data)
	{
		return m_pCamera->SetSerialTxData(tx_data);
	}

	inline int SetSerialTxLockEnabled(int is_enabled)
	{
		return m_pCamera->SetSerialTxLockEnabled(is_enabled);
	}

	inline int GetSerialTxLockEnabled(int* is_enabled)
	{
		return m_pCamera->GetSerialTxLockEnabled(is_enabled);
	}

	inline int SetSerialRxSize(int n_bytes)
	{
		return m_pCamera->SetSerialRxSize(n_bytes);
	}

	inline int GetSerialRxWaiting(int* n_bytes)
	{
		return m_pCamera->GetSerialRxWaiting(n_bytes);
	}

	inline int GetSerialRxData(unsigned char* rx_data)
	{
		return m_pCamera->GetSerialRxData(rx_data);
	}


	//Image save and load
	static int GetImageCount(const wchar_t* file_name, int* tiff_handle, unsigned int* image_count);

	static int GetImageBuffer(int tiff_handle, unsigned int frame_number, TiffImageSimpleInfo* simple_info);

	static int CloseImage(int tiff_handle);

	static int SaveImage(const wchar_t* file_name, int p2d_img_hdl, int slotIndex);

	static int GetJpegBuffer(const wchar_t* file_name, int* p2d_img_hdl);

	int SetCurrentPolarizationImageType(int polar_image_type);
	int GetCurrentPolarizationImageType(int* polar_image_type);
};


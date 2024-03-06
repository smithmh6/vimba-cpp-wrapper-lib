#pragma once

#ifdef CAMERA_LIB
#define CAMERA_API __declspec(dllexport)
#else
#define CAMERA_API __declspec(dllimport)
#endif


#include <string>
#include "VmbCPP/VmbCPP.h"
using namespace VmbCPP;

#define SUCCESS_ 0
#define ERROR_ -1
#define NOTSUPPORT_ -2
#define INVALID_ -3

enum class ColorPhase
{
	PHASE_BAYER_RED /*!< A red pixel. */
	, PHASE_BAYER_BLUE /*!< A blue pixel. */
	, PHASE_BAYER_GREEN_LEFT_OF_RED /*!< A green pixel next to a red pixel. */
	, PHASE_BAYER_GREEN_LEFT_OF_BLUE /*!< A green pixel next to a blue pixel. */
};

enum class PolarImageType
{
	UNPROCESSED,
	TOTAL_OPTICAL_POWER,
	AZIMUTH,
	DEGREE_OF_LINEAR_POLARIZATION,
	QUAD_VIEW
};

enum class CameraTriggerMode
{
	SOFTWARE_TRIGGER,
	HARDWARE_TRIGGER,
	BULB
};

enum class HardwareTriggerPolarity
{
	RISING,
	FALLING
};

enum class SerialBaudRate
{
	Baud_9600,
	Baud_115200,
	Baud_230400
};

typedef void(__cdecl* RecieveImage)(void* bufferPtr, unsigned int frameNum, int width, int height);

class MyFrameObserver : public IFrameObserver
{
private:
	void* _context = nullptr;
	void(*_frame_callback)(void*, int, int, int, void*);

public:
	MyFrameObserver() = delete;

	MyFrameObserver(CameraPtr pCamera, void* context, void(*frame_callback)(void*, int, int, int, void*)) :IFrameObserver(pCamera) 
	{
		this->_context = context;
		this->_frame_callback = frame_callback;
	};

	void FrameReceived(const FramePtr pFrame)
	{
		if (_frame_callback == nullptr) return;

		VmbFrameStatusType status;
		if (pFrame->GetReceiveStatus(status) == VmbErrorSuccess && status == VmbFrameStatusComplete)
		{
			VmbUchar_t* image_data;
			VmbUint32_t width, height;
			VmbUint64_t frame_number;
			if (pFrame->GetImage(image_data) == VmbErrorSuccess
				&& pFrame->GetFrameID(frame_number) == VmbErrorSuccess
				&& pFrame->GetWidth(width) == VmbErrorSuccess
				&& pFrame->GetHeight(height) == VmbErrorSuccess)
			{
				_frame_callback(image_data, (int)frame_number, (int)width, (int)height, _context);
			}
		}

		m_pCamera->QueueFrame(pFrame);
	}
};

class ConnectObserver : public ICameraListObserver
{
private:
	void(__cdecl* _disconnect_callback)(const char*) = nullptr;

public:
	ConnectObserver() = delete;

	ConnectObserver(void(__cdecl* callback)(const char*)) : ICameraListObserver()
	{
		this->_disconnect_callback = callback;
	}

	void CameraListChanged(CameraPtr pCam, UpdateTriggerType reason)
	{
		if (_disconnect_callback == nullptr) return;

		if (reason == UpdateTriggerPluggedOut)
		{
			std::string id;
			if (pCam->GetID(id) != VmbErrorSuccess) return;
			_disconnect_callback(id.c_str());
		}
	}
};

class CAMERA_API AlliedCamera
{
public:
	AlliedCamera() {};
	~AlliedCamera();

	static int ListCamera(char* camera_ids, int size);
	static int InitializeSdk(void(__cdecl* disconnect_callback)(const char*));
	static int ReleaseSDK();

	bool IsRemoved = false;
	bool IsColorCamera() { return _isColorCamera; }

	//Implement
	int Open(const char* sn);
	int Close();

	int GetModelName(char* model_name, int str_length);
	int GetFirmware(char* firmware_version, int str_length);

	int SetExposureTime(double exposure_us);
	int GetExposureTime(double* exposure_us);
	int GetExposureTimeRange(double* exposure_us_min, double* exposure_us_max, double* exposure_us_inc);

	int SetGain(double gain);
	int GetGain(double* gain);
	int GetGainRange(double* gain_min, double* gain_max);

	int SetBlackLevel(int black_level);
	int GetBlackLevel(int* black_level);
	int GetBlackLevelRange(int* min, int* max);

	int SetBinX(int xbin);
	int GetBinX(int* binx);
	int GetBinXRange(int* hbin_min, int* hbin_max);

	int SetBinY(int ybin);
	int GetBinY(int* biny);
	int GetBinYRange(int* vbin_min, int* vbin_max);

	int SetROI(int x, int y, int width, int height);
	int GetROI(int* x, int* y, int* width, int* height);
	int GetROIRange(int* upper_left_x_pixels_min, int* upper_left_y_pixels_min, int* lower_right_x_pixels_min, int* lower_right_y_pixels_min, int* upper_left_x_pixels_max, int* upper_left_y_pixels_max, int* lower_right_x_pixels_max, int* lower_right_y_pixels_max);

	int GetBitDepth(int* pixel_bit_depth);
	int GetImageHeight(int* height_pixels);
	int GetImageWidth(int* width_pixels);
	int GetImageWidthAndHeight(int* width_pixels, int* height_pixels);
	int GetSensorHeight(int* height_pixels);
	int GetSensorWidth(int* width_pixels);

	int GetFrameRate(float* frameRate);

	int SetRecieveImageCallback(RecieveImage callback);
	//mode: 0:software trigger 1:hardware trigger
	int SetTriggerMode(CameraTriggerMode mode, HardwareTriggerPolarity plority = HardwareTriggerPolarity::RISING);

	virtual int SetFramePerTrigger(const unsigned int&);
	virtual int GetFramePerTrigger(unsigned int&);

	int IssueSoftwareTirggle();
	int AcquisitionStop();

	int GetColorMode(int* color_type);
	int SetAutoExposure(int enable);
	int GetAutoExposure(int* enable);

	int GetVFlip(int* bVFlip);
	int SetVFlip(int bVFlip);

	int GetHFlip(int* bHFlip);
	int SetHFlip(int bHFlip);

	bool GetIsSimulator() { return false; }

	int BayerTransform(unsigned short* imageBuffer, unsigned short* outputBuffer);
	int ColorTransform48to24(unsigned short* inputBuffer, unsigned char* outputBuffer);
	int PolarTransform(unsigned short* inputBuffer, PolarImageType imageType, unsigned short* outputBuffer);

	int CameraArm();
	int CameraDisarm();

	int SetFrameRateControlValue(double frame_rate_fps);
	int GetFrameRateControlValue(double* frame_rate_fps);
	int GetFrameRateControlValueRange(double* min, double* max);

	int SetFrameRateControlEnabled(int is_enabled);
	int GetFrameRateControlEnabled(int* is_enabled);

	int GetHotPixelCorrectionEnabled(int* is_enabled);
	int SetHotPixelCorrectionEnabled(int is_enabled);
	int GetHotPixelCorrectionThresholdRange(int* min, int* max);
	int GetHotPixelCorrectionThreshold(int* threshold);
	int SetHotPixelCorrectionThreshold(int threshold);

	int GetIsOperationModeSupported(CameraTriggerMode mode, int* is_operation_mode_supported);

	int GetIsLEDSupported(int* is_led_supported);
	int GetLEDStatus(int* is_led_on);
	int SetLEDStatus(int is_led_on);

	int GetSensorPixelWidth(double* pixel_width);
	int GetSensorPixelHeight(double* pixel_height);

	//Umcommon Functions
	int GetCorrectionModeEnabled(bool* is_enabled);
	int SetCorrectionModeEnabled(bool is_enabled);

	int GetCorrectionMode(int* Mode);
	int SetCorrectionMode(int Mode);

	int GetGainAutoEnabled(bool* is_enabled);
	int SetGainAutoEnabled(bool is_enabled);

	int GetExposureAutoEnabled(int* mode);
	int SetExposureAutoEnabled(int mode);

	int GetReverseXEnabled(bool* is_enabled);
	int SetReverseXEnabled(bool is_enabled);

	int GetReverseYEnabled(bool* is_enabled);
	int SetReverseYEnabled(bool is_enabled);

	// serial commands
	int SetSerialHubEnabled(bool is_enabled);
	int GetSerialHubEnabled(bool* is_enabled);
	int SetSerialBaudRate(SerialBaudRate baud_rate);
	int GetSerialBaudRate(SerialBaudRate* baud_rate);
	int SetSerialTxSize(int n_bytes);
	int SetSerialTxData(unsigned char* tx_data);
	int SetSerialTxLockEnabled(int is_enabled);
	int GetSerialTxLockEnabled(int* is_enabled);
	int SetSerialRxSize(int n_bytes);
	int GetSerialRxWaiting(int* n_bytes);
	int GetSerialRxData(unsigned char* rx_data);


private:
	static bool _isInitialized;
	static char* _sn;

	bool _isRunning = false;
	bool _isColorCamera = false;

	RecieveImage _callback = nullptr;

	int _pixel_bit_depth = 0;
	//int _roi_start_x = 0;
	//int _roi_start_y = 0;
	//int _roi_end_x = 0;
	//int _roi_end_y = 0;

	// 0 for software trigger. 1 for hardware trigger
	enum CameraTriggerMode _triggerMode;

	//Properties for color camera
	void* _colorProcessHandle = nullptr;
	bool _is_color_sdk_open = false;
	bool _is_demosaic_sdk_open = false;
	enum ColorPhase _color_filter_array_phase;

	float _color_correction_matrix[9] = { 0 };
	float _default_white_balance_matrix[9] = { 0 };

	//Properties for polar camera
	void* _polar_processor_handle = nullptr;
	bool _is_polar_processor_open = false;
	bool _is_polar_camera = false;

	static void FrameAvailableCallback(void* image_buffer, int frame_count, int width, int height, void* context);
	static void CameraDisconnectCallback(const char* cameraSerialNumber, void* context);

	//Vmb
	CameraPtr _camera;
	static VmbSystem& _system;
	int _frames_per_trigger = 0;
	std::vector<FramePtr> _frames;

	template <typename T>
	int GetFeatureValue(std::string feature_name, T& value);
	template <typename T>
	int SetFeatureValue(std::string feature_name, T value);
};
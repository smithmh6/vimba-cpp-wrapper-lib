#include "pch.h"
#include "CameraSystem.h"
#include <iostream>
#include <string>
#include "error_code.h"

static CameraSystem camera;

static bool s_isInitialized = false;

int32_t InvokeServiceFunc(const char* serviceName, void* serviceParams)
{
	if (strcmp(serviceName, "Log") == 0)
	{
		std::cout << (char*)serviceParams << std::endl;
	}
	return 0;
}

int SMNC_Release()
{
	if (s_isInitialized)
	{
		s_isInitialized = false;
		return CameraSystem::Release();
	}
	return 0;
}

int SMNC_List(char* buffer, int bufferLength)
{
	auto result = CameraSystem::Init();
	if (result != 0)
		return result;
	s_isInitialized = true;
	result = CameraSystem::List(buffer, bufferLength);
	return result;
}

static bool s_isOpened = false;

int SMNC_Open(const char* serialNumber)
{
	if (s_isOpened || !s_isInitialized)
		return -1;
	int ret = camera.Open((char*)serialNumber);
	if (ret == 0)
		s_isOpened = true;
	return ret;
}

int SMNC_Close()
{
	if (!s_isOpened || !s_isInitialized)
		return -1;
	s_isOpened = false;
	camera.StopLive();
	camera.StopCapture();
	return camera.Close();
}

#define CHECK_CAMERA if(!s_isInitialized || !s_isOpened) return -1

int SMNC_GetFirmwareVersion(char* firmware_version, int str_length)
{
	CHECK_CAMERA;
	return camera.GetFirmwareVersion(firmware_version, str_length);
}

int SMNC_GetModelName(char* model_name, int str_length)
{
	CHECK_CAMERA;
	return camera.GetModelName(model_name, str_length);
}

int SMNC_SetCallback(ImageCallback image_callback, DisconnectCallback disconnect_callback, AutoExposureCallback auto_exposure_callback)
{
	CHECK_CAMERA;
	camera.SetCallback(image_callback, disconnect_callback, auto_exposure_callback);
	return 0;
}

int SMNC_StartPreview()
{
	CHECK_CAMERA;
	return camera.StartLive();
}

int SMNC_StopPreview()
{
	CHECK_CAMERA;
	return camera.StopLive();
}

int SMNC_StartSnapshot(SnapshotInfo info)
{
	CHECK_CAMERA;
	return camera.StartSnapshot(info);
}

int SMNC_StopSnapshot()
{
	CHECK_CAMERA;
	return camera.StopSnapshot();
}

int SMNC_StartCapture(CaptureInfo info, PreparedCallback callback)
{
	CHECK_CAMERA;
	return camera.StartCapture(info, callback);
}

int SMNC_StopCapture()
{
	CHECK_CAMERA;
	return camera.StopCapture();
}

int SMNC_SetExposureTime(double exposure_us)
{
	CHECK_CAMERA;
	return camera.SetExposureTime(exposure_us);
}

int SMNC_GetExposureTime(double* exposure_us)
{
	CHECK_CAMERA;
	return camera.GetExposureTime(exposure_us);
}

int SMNC_GetExposureTimeRange(DoubleParams* exposure_us_params)
{
	CHECK_CAMERA;
	return camera.GetExposureTimeRange(exposure_us_params);
}

int SMNC_SetGain(double gain)
{
	CHECK_CAMERA;
	return camera.SetGain(gain);
}

int SMNC_GetGain(double* gain)
{
	CHECK_CAMERA;
	return camera.GetGain(gain);
}

int SMNC_GetGainRange(double* gain_min, double* gain_max)
{
	CHECK_CAMERA;
	return camera.GetGainRange(gain_min, gain_max);
}

int SMNC_SetBlackLevel(int black_level)
{
	CHECK_CAMERA;
	return camera.SetBlackLevel(black_level);
}

int SMNC_GetBlackLevel(int* black_level)
{
	CHECK_CAMERA;
	return camera.GetBlackLevel(black_level);
}

int SMNC_GetBlackLevelRange(int* black_level_min, int* black_level_max)
{
	CHECK_CAMERA;
	return camera.GetBlackLevelRange(black_level_min, black_level_max);
}

int SMNC_SetBinX(int binx)
{
	CHECK_CAMERA;
	return camera.SetBinX(binx);
}

int SMNC_GetBinX(int* binx)
{
	CHECK_CAMERA;
	return camera.GetBinX(binx);
}

int SMNC_GetBinXRange(int* binx_min, int* binx_max)
{
	CHECK_CAMERA;
	return camera.GetBinXRange(binx_min, binx_max);
}

int SMNC_SetBinY(int biny)
{
	CHECK_CAMERA;
	return camera.SetBinY(biny);
}

int SMNC_GetBinY(int* biny)
{
	CHECK_CAMERA;
	return camera.GetBinY(biny);
}

int SMNC_GetBinYRange(int* biny_min, int* biny_max)
{
	CHECK_CAMERA;
	return camera.GetBinYRange(biny_min, biny_max);
}

int SMNC_SetROI(int x, int y, int width, int height)
{
	CHECK_CAMERA;
	return camera.SetROI(x, y, width, height);
}

int SMNC_GetROI(int* x, int* y, int* width, int* height)
{
	CHECK_CAMERA;
	return camera.GetROI(x, y, width, height);
}

int SMNC_GetROIRange(int* upper_left_x_min, int* upper_left_y_min, int* lower_right_x_min, int* lower_right_y_min, int* upper_left_x_max, int* upper_left_y_max, int* lower_right_x_max, int* lower_right_y_max)
{
	CHECK_CAMERA;
	return camera.GetROIRange(upper_left_x_min, upper_left_y_min, lower_right_x_min, lower_right_y_min, upper_left_x_max, upper_left_y_max, lower_right_x_max, lower_right_y_max);
}

int SMNC_GetBitDepth(int* bit_depth)
{
	CHECK_CAMERA;
	return camera.GetBitDepth(bit_depth);
}

int SMNC_GetImageWidthAndHeight(int* width, int* height)
{
	CHECK_CAMERA;
	return camera.GetImageWidthAndHeight(width, height);
}

int SMNC_GetImageCount(const wchar_t* file_name, int* tiff_handle, unsigned int* image_counut)
{
	return CameraSystem::GetImageCount(file_name, tiff_handle, image_counut);
}

int SMNC_GetImageData(int tiff_handle, unsigned int image_number, TiffImageSimpleInfo* simple_info)
{
	return CameraSystem::GetImageBuffer(tiff_handle, image_number, simple_info);
}

int SMNC_CloseImage(int image_handle)
{
	return CameraSystem::CloseImage(image_handle);
}

int SMNC_GetJpegData(const wchar_t* file_name, int* p2d_img_hdl)
{
	return CameraSystem::GetJpegBuffer(file_name, p2d_img_hdl);
}

int SMNC_SaveImage(const wchar_t* file_name, int p2d_img_hdl, int slotIndex)
{
	return CameraSystem::SaveImage(file_name, p2d_img_hdl, slotIndex);
}

int SMNC_GetColorMode(int* color_type)
{
	CHECK_CAMERA;
	return camera.GetColorMode(color_type);
}

int SMNC_SetAutoExposure(int enable)
{
	CHECK_CAMERA;
	return camera.SetAutoExposure(enable);
}

int SMNC_GetAutoExposure(int* enable)
{
	CHECK_CAMERA;
	return camera.GetAutoExposure(enable);
}

int SMNC_GetFrameRateControlValueRange(double* min, double* max)
{
	CHECK_CAMERA;
	return camera.GetFrameRateControlValueRange(min, max);
}

int SMNC_SetFrameRateControlValue(double frame_rate_fps)
{
	CHECK_CAMERA;
	return camera.SetFrameRateControlValue(frame_rate_fps);
}

int SMNC_GetIsOperationModeSupported(OperateMode mode, int* is_operation_mode_supported)
{
	CHECK_CAMERA;
	return camera.GetIsOperationModeSupported((CameraTriggerMode)mode, is_operation_mode_supported);
}

int SMNC_GetIsLEDSupported(int* is_led_supported)
{
	CHECK_CAMERA;
	return camera.GetIsLEDSupported(is_led_supported);
}

int SMNC_GetLEDStatus(int* is_led_on)
{
	CHECK_CAMERA;
	return camera.GetLEDStatus(is_led_on);
}

int SMNC_SetLEDStatus(int is_led_on)
{
	CHECK_CAMERA;
	return camera.SetLEDStatus(is_led_on);
}

int SMNC_SwitchAutoExposureStatus(bool isAutoExposure)
{
	CHECK_CAMERA;
	return camera.AutoExposureStatusChange(isAutoExposure);
}

int SMNC_GetHotPixelCorrectionEnabled(int* is_enabled)
{
	CHECK_CAMERA;
	return camera.GetHotPixelCorrectionEnabled(is_enabled);
}

int SMNC_SetHotPixelCorrectionEnabled(int is_enabled)
{
	CHECK_CAMERA;
	return camera.SetHotPixelCorrectionEnabled(is_enabled);
}


int SMNC_GetHotPixelCorrectionThresholdRange(int* min, int* max)
{
	CHECK_CAMERA;
	return camera.GetHotPixelCorrectionThresholdRange(min, max);
}

int SMNC_GetHotPixelCorrectionThreshold(int* threshold)
{
	CHECK_CAMERA;
	return camera.GetHotPixelCorrectionThreshold(threshold);
}

int SMNC_SetHotPixelCorrectionThreshold(int threshold)
{
	CHECK_CAMERA;
	return camera.SetHotPixelCorrectionThreshold(threshold);
}

int SMNC_GetSensorPixelWidth(double* pixel_width)
{
	CHECK_CAMERA;
	return camera.GetSensorPixelWidth(pixel_width);
}

int SMNC_GetSensorPixelHeight(double* pixel_height)
{
	CHECK_CAMERA;
	return camera.GetSensorPixelHeight(pixel_height);
}

int SMNC_SetCurrentPolarizationImageType(int polar_image_type)
{
	CHECK_CAMERA;
	return camera.SetCurrentPolarizationImageType(polar_image_type);
}

int SMNC_GetCurrentPolarizationImageType(int* polar_image_type)
{
	CHECK_CAMERA;
	return camera.GetCurrentPolarizationImageType(polar_image_type);
}


int SMNC_GetCorrectionModeEnabled(bool* is_enabled)
{
	CHECK_CAMERA;
	return camera.GetCorrectionModeEnabled(is_enabled);
}

int SMNC_SetCorrectionModeEnabled(bool is_enabled)
{
	CHECK_CAMERA;
	return camera.SetCorrectionModeEnabled(is_enabled);
}

int SMNC_GetCorrectionMode(int* Mode)
{
	CHECK_CAMERA;
	return camera.GetCorrectionMode(Mode);
}

int SMNC_SetCorrectionMode(int Mode)
{
	CHECK_CAMERA;
	return camera.SetCorrectionMode(Mode);
}

int SMNC_GetGainAutoEnabled(bool* is_enabled)
{
	CHECK_CAMERA;
	return camera.GetGainAutoEnabled(is_enabled);
}

int SMNC_SetGainAutoEnabled(bool is_enabled)
{
	CHECK_CAMERA;
	return camera.SetGainAutoEnabled(is_enabled);
}

int SMNC_GetExposureAutoEnabled(int* is_enabled)
{
	CHECK_CAMERA;
	return camera.GetExposureAutoEnabled(is_enabled);
}

int SMNC_SetExposureAutoEnabled(int is_enabled)
{
	CHECK_CAMERA;
	return camera.SetExposureAutoEnabled(is_enabled);
}

int SMNC_GetReverseXEnabled(bool* is_enabled)
{
	CHECK_CAMERA;
	return camera.GetReverseXEnabled(is_enabled);
}

int SMNC_SetReverseXEnabled(bool is_enabled)
{
	CHECK_CAMERA;
	return camera.SetReverseXEnabled(is_enabled);
}

int SMNC_GetReverseYEnabled(bool* is_enabled)
{
	CHECK_CAMERA;
	return camera.GetReverseYEnabled(is_enabled);
}

int SMNC_SetReverseYEnabled(bool is_enabled)
{
	CHECK_CAMERA;
	return camera.SetReverseYEnabled(is_enabled);
}

int SMNC_SetSerialHubEnabled(bool is_enabled)
{
	CHECK_CAMERA;
	return camera.SetSerialHubEnabled(is_enabled);
}

int SMNC_GetSerialHubEnabled(bool* is_enabled)
{
	CHECK_CAMERA;
	return camera.GetSerialHubEnabled(is_enabled);
}

int SMNC_JogClockwise()
{
	CHECK_CAMERA;
	unsigned char* buf = (unsigned char*)"1";

	// set serial TX buffer size
	int _n_bytes = strlen((const char*)buf);
	camera.SetSerialTxSize(_n_bytes);

	// transmit the serial data
	return camera.SetSerialTxData(buf);
} 

int SMNC_JogCounterClockwise()
{
	CHECK_CAMERA;
	unsigned char* buf = (unsigned char*)"2";

	// set serial TX buffer size
	int _n_bytes = strlen((const char*)buf);
	camera.SetSerialTxSize(_n_bytes);

	// transmit the serial data
	return camera.SetSerialTxData(buf);
}

int SMNC_JogToSlot(int targetSlotIndex)
{
	CHECK_CAMERA;

	// command to pass to microcontroller
	unsigned char* buf = (unsigned char*)"3";

	// write the slot number to the controller

	std::vector<unsigned char>::size_type _size = strlen((const char*)buf);
	std::vector<unsigned char> _rx_vector(buf, buf + _size);

	// set serial TX buffer size
	int _n_bytes = strlen((const char*)buf);
	camera.SetSerialTxSize(_n_bytes);

	// transmit the serial data
	return 0;// camera.SetSerialTxData(_rx_vector);
}

int SMNC_IsMoving(bool* is_moving)
{
	CHECK_CAMERA;
	return 0;
}

int SMNC_StopMotion()
{
	CHECK_CAMERA;
	return 0;
}

int SMNC_FindHomePosition()
{
	CHECK_CAMERA;

	// command to pass to microcontroller
	unsigned char* buf = (unsigned char*)"FindHomePosition&";

	std::vector<unsigned char>::size_type _size = strlen((const char*)buf);
	std::vector<unsigned char> _rx_vector(buf, buf + _size);

	// set serial TX buffer size
	int _n_bytes = strlen((const char*)buf);
	camera.SetSerialTxSize(_n_bytes);

	// transmit the serial data
	return 0;// camera.SetSerialTxData(_rx_vector);
}

int SMNC_MoveConstant()
{
	CHECK_CAMERA;
	return 0;
}
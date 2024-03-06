#include "pch.h"
#include "CameraSystem.h"
#include "ThorLogger.h"
#include <condition_variable>
#include "error_code.h"
#include <vector>
#include <cmath>
#include "turbojpeg.h"
#include <combaseapi.h>
#include <filesystem>
#include "HighPerfTimer.h"
#ifdef _DEBUG
#define TEST_SPEED
#endif

#ifdef TEST_SPEED
#include "HighPerfTimer.h"
static HighPerfTimer timer_callback;
static HighPerfTimer timer_process;
static HighPerfTimer timer_save;
static HighPerfTimer timer;
#endif
#include <string>
#include <vector>
//static HighPerfTimer sofwareTriggerTimer;
//#define SAVECOLOR 0
#define MANUFACTURE_TAG 271
#define MODEL_TAG 272
#define VALIDBITS_TAG 10588
#define SLOTINDEX_TAG 10599
//#define ZOOM_TAG 10588
//#define COLOR_TAG 10599

//#define MODEL_TAG_FIXED_SIZE 64

#define THORIMAGECAM_MANU "THORIMAGECAM"

using namespace tiff;
using namespace std::chrono_literals;

CameraSystem* CameraSystem::s_currentCamera;

static std::condition_variable cv;
static std::mutex cv_mut;

#pragma region camera system static functions

int CameraSystem::Init()
{
	auto result = AlliedCamera::InitializeSdk(&CameraSystem::CameraDisconnectCallback);
	return result;
}

int CameraSystem::Release()
{
	auto result = AlliedCamera::ReleaseSDK();
	return result;
}

int CameraSystem::List(char* buffer, int bufferLength)
{
	memset(buffer, 0, bufferLength);
	auto result = AlliedCamera::ListCamera(buffer, bufferLength);
	return result;
}

#pragma endregion

#pragma region camera system private functions

long CameraSystem::PreExecutionCore(unsigned int averageFrames)
{
	int width, height, bitDepth;
	long status = m_pCamera->GetImageWidthAndHeight(&width, &height);
	if (status != 0)
	{
		std::string errorMessage = "Get camera image width or height failed.";
		WriteLog(LogLevel::Error, errorMessage.c_str());
		return status;
	}

	status = m_pCamera->GetBitDepth(&bitDepth);
	if (status != 0)
	{
		std::string errorMessage = "Get camera valid bits failed.";
		WriteLog(LogLevel::Error, errorMessage.c_str());
		return status;
	}
	p2d_data_format camera_format;
	int bytes = (bitDepth + 7) / 8;
	if (bytes == 1)
		camera_format = p2d_data_format::P2D_8U;
	else if (bytes == 2)
		camera_format = p2d_data_format::P2D_16U;
	else
	{
		std::string errorMessage = "Camera bit depth" + std::to_string(bitDepth) + " is not supported.";
		WriteLog(LogLevel::Error, errorMessage.c_str());
		return ERR_CAMSYS_BITDEPTH_NOT_SUPPORT;
	}

	p2d_channels camera_channels;
	switch (m_cameraType)
	{
	case CameraType::CamMono:
		camera_channels = p2d_channels::P2D_CHANNELS_1;
		break;
	case CameraType::CamColor:
		camera_channels = p2d_channels::P2D_CHANNELS_3;
		break;
	case CameraType::CamColorBayer:
		camera_channels = p2d_channels::P2D_CHANNELS_1;
		break;
	default:
		std::string errorMessage = "Camera color mode" + std::to_string((int)m_cameraType) + " is not supported.";
		WriteLog(LogLevel::Error, errorMessage.c_str());
		return ERR_CAMSYS_COLORMODE_NOT_SUPPORT;
	}

	m_backup_p2d_img_handle = p2d_create_image();
	if (m_backup_p2d_img_handle < 0)
		return ERR_CAMSYS_CREATE_P2D_FAILED;

	m_backup_p2d_img_info = new p2d_info();
	m_backup_p2d_img_info->x_size = width;
	m_backup_p2d_img_info->y_size = height;
	m_backup_p2d_img_info->x_physical_um = 1.0;
	m_backup_p2d_img_info->y_physical_um = 1.0;
	m_backup_p2d_img_info->line_bytes = width * (int)camera_channels * bytes;
	m_backup_p2d_img_info->pix_type = camera_format;
	m_backup_p2d_img_info->valid_bits = bitDepth;
	m_backup_p2d_img_info->channels = camera_channels;

	if (averageFrames > 1)
	{
		p2d_data_format average_format = p2d_data_format::P2D_16U;
		int32_t average_valid_bits = bitDepth;
		switch (camera_format)
		{
		case p2d_data_format::P2D_8U:
		{
			average_format = p2d_data_format::P2D_16U;
			average_valid_bits = 16;
			break;
		}
		case p2d_data_format::P2D_16U:
		{
			average_format = p2d_data_format::P2D_32F;
			average_valid_bits = 32;
			break;
		}
		default:
			return ERR_CAMSYS_BITDEPTH_NOT_SUPPORT;
		}

		m_average_temp_handle = p2d_create_image();
		if (m_average_temp_handle < 0)
		{
			p2d_destroy_image(m_backup_p2d_img_handle);
			m_backup_p2d_img_handle = -1;
			return ERR_CAMSYS_CREATE_P2D_FAILED;
		}
		int8_t res = p2d_init_image(m_average_temp_handle, width, height, average_format, camera_channels, average_valid_bits);
		if (STATUS_OK != res)
		{
			p2d_destroy_image(m_backup_p2d_img_handle);
			m_backup_p2d_img_handle = -1;
			p2d_destroy_image(m_average_temp_handle);
			m_average_temp_handle = -1;
			return ERR_CAMSYS_INIT_P2D_FAILED;
		}
	}

	size_t count = width * height * (int)camera_channels;
	for (int i = 0; i < 4; i++)
	{
		int temp_hdl = p2d_create_image();
		int8_t res = p2d_init_image(temp_hdl, width, height, camera_format, camera_channels, bitDepth);
		if (STATUS_OK != res)
		{
			while (!m_callback_transfer_p2d_handle_queue.empty())
			{
				int hdl = m_callback_transfer_p2d_handle_queue.front();
				m_callback_image_queue.pop();
				if (hdl >= 0)
					p2d_destroy_image(hdl);
			}
			p2d_destroy_image(m_backup_p2d_img_handle);
			m_backup_p2d_img_handle = -1;
			p2d_destroy_image(m_average_temp_handle);
			m_average_temp_handle = -1;
			return ERR_CAMSYS_INIT_P2D_FAILED;
		}
		m_callback_transfer_p2d_handle_queue.push(temp_hdl);
	}

	m_callbackFrame = 0;
	m_processedFrameCount = 0;

	return 0;
}

long CameraSystem::PostExecutionCore()
{
	auto ret = m_pCamera->CameraDisarm();
	//if (ret != 0)
	//{
	//	std::string msg = "Disarm camera failed with error code : " + std::to_string(ret) + ".";
	//	WriteLog(LogLevel::Error, msg.c_str());
	//}
	if (thread_auto_exposure != nullptr && thread_auto_exposure->joinable())
	{
		thread_auto_exposure->join();
		delete thread_auto_exposure;
		thread_auto_exposure = nullptr;
	}
	if (thread_deal != nullptr && thread_deal->joinable())
	{
		thread_deal->join();
		delete thread_deal;
		thread_deal = nullptr;
	}
	if (thread_proc != nullptr && thread_proc->joinable())
	{
		thread_proc->join();
		delete thread_proc;
		thread_proc = nullptr;
	}

	while (!m_callback_image_queue.empty())
	{
		CameraSystemFrameInfo info = m_callback_image_queue.front();
		m_callback_image_queue.pop();
		if (info.p2d_img_hdl >= 0)
			p2d_destroy_image(info.p2d_img_hdl);
	}

	while (!m_processed_image_queue.empty())
	{
		CameraSystemFrameInfo info = m_processed_image_queue.front();
		m_processed_image_queue.pop();
		if (info.p2d_img_hdl >= 0)
			p2d_destroy_image(info.p2d_img_hdl);
	}

	while (!m_callback_transfer_p2d_handle_queue.empty())
	{
		int temp_hdl = m_callback_transfer_p2d_handle_queue.front();
		m_callback_transfer_p2d_handle_queue.pop();
		if (temp_hdl >= 0)
			p2d_destroy_image(temp_hdl);
	}

	if (m_backup_p2d_img_handle != -1)
	{
		p2d_destroy_image(m_backup_p2d_img_handle);
		m_backup_p2d_img_handle = -1;

	}
	if (m_backup_p2d_img_info != nullptr)
	{
		delete m_backup_p2d_img_info;
		m_backup_p2d_img_info = nullptr;
	}

	if (m_hdl_AutoExposuring != -1)
	{
		p2d_destroy_image(m_hdl_AutoExposuring);
		m_hdl_AutoExposuring = -1;
	}

	if (m_average_temp_handle != -1)
	{
		p2d_destroy_image(m_average_temp_handle);
		m_average_temp_handle = -1;
	}

	return 0;
}

//int CameraSystem::StartCapture_InteralTrigger(CaptureInfo info)
//{
//	//Camera Init
//	CHECK_STATUS(m_pCamera->CameraDisarm());
//	CHECK_STATUS(m_pCamera->SetFramePerTrigger(0));
//
//	CHECK_STATUS(m_pCamera->SetTriggerMode(CameraTriggerMode::SOFTWARE_TRIGGER));
//	CHECK_STATUS(m_pCamera->CameraArm());
//	CHECK_STATUS(m_pCamera->IssueSoftwareTirggle());
//
//	//int checktime = info.frameCount;
//
//	while (true)
//	{
//		if (!m_isCapturing || m_CaptureError || (m_callbackFrame > info.frameCount) /*|| (checktime <= 0)*/)
//			break;
//	}
//	return SUCCESS_;
//}

int CameraSystem::StartCapture_SoftwareTrigger()
{
	CHECK_STATUS(m_pCamera->CameraDisarm());
	CHECK_STATUS(m_pCamera->SetTriggerMode(CameraTriggerMode::SOFTWARE_TRIGGER));
	CHECK_STATUS(m_pCamera->SetFramePerTrigger(1));
	CHECK_STATUS(m_pCamera->CameraArm());

	//std::chrono::steady_clock::time_point trigger_start = std::chrono::steady_clock::now();
	CHECK_STATUS(m_pCamera->IssueSoftwareTirggle());
	//timer.Start();

	while (true)
	{
		if (!m_isCapturing)
			return 101;
		if (m_CaptureErrorCode != 0)
			return m_CaptureErrorCode;

		if (m_callbackArrrived)
		{
			m_callbackArrrived = false;
			
			if (!m_isCapturing)
				return 101;
			if (m_CaptureErrorCode != 0)
				return m_CaptureErrorCode;			
			if (m_callbackFrame >= m_pCaptureInfo->frameCount)
				return SUCCESS_;

			auto result = m_pCamera->AcquisitionStop();
			if (result != 0)
			{
				std::string errorMessage = "Stop acquisition failed. Error is " + std::to_string(result) + ".";
				WriteLog(LogLevel::Error, errorMessage.c_str());
				return ERR_CAMSYS_SWTRIGGER_FAILED;
			}

			//Move filter wheel and set new settings
			if (m_callbackFrame % m_pCaptureInfo->baseInfo->averageFrames == 0)
			{
				int next_slot_index = m_callbackFrame / m_pCaptureInfo->baseInfo->averageFrames;
				next_slot_index += m_pCaptureInfo->baseInfo->currentSlotIndex;
				if (next_slot_index >= m_pCaptureInfo->baseInfo->captureSlotSettingsCount)
					next_slot_index -= m_pCaptureInfo->baseInfo->captureSlotSettingsCount;

				CaptureSlotSetting setting = m_pCaptureInfo->baseInfo->captureSlotSettings[next_slot_index];
				SetExposureAutoEnabled(setting.isAutoExposure ? 1 : 0);
				if (!setting.isAutoExposure)
				{
					SetExposureTime(setting.exposureTime);
					//long long wait_us = (long long)(setting.exposureTime * 1.1);
					//std::string wait_msg = "Wait for : " + std::to_string(wait_us) + " us.";
					//WriteLog(LogLevel::Error, wait_msg.c_str());
					//std::this_thread::sleep_for(std::chrono::microseconds(wait_us));
				}

				SetGainAutoEnabled(setting.isAutoGain);
				if (!setting.isAutoGain)
					SetGain(setting.gain);

				//wait
				//std::this_thread::sleep_for(std::chrono::milliseconds(100));

				//std::string slotMessage = "Set slot setting with slot index : " + std::to_string(slotIndex) + ".";
				//WriteLog(LogLevel::Error, slotMessage.c_str());

				//TODO : Move filter to target slot, and wait it stable
				//simulate
				//std::this_thread::sleep_for(std::chrono::milliseconds(2000));
			}

			//std::string triggerMessage = "Trigger with current frame count : " + std::to_string(m_callbackFrame) + ".";
			//WriteLog(LogLevel::Error, triggerMessage.c_str());
			//trigger_start = std::chrono::steady_clock::now();
			result = m_pCamera->IssueSoftwareTirggle();
			if (result != 0)
			{
				std::string errorMessage = "Software trigger the camera failed. Error is " + std::to_string(result) + ".";
				WriteLog(LogLevel::Error, errorMessage.c_str());
				return ERR_CAMSYS_SWTRIGGER_FAILED;
			}
		}
		else
		{
			std::this_thread::sleep_for(std::chrono::milliseconds(1));
		}

	}

	//if (info.frameCount > m_savedFrameCount)
	//{
	//	for (int i = 0; i < 300; i++)
	//	{
	//		if (info.frameCount <= m_savedFrameCount || !m_isCapturing || m_CaptureError)
	//			break;
	//		else
	//			std::this_thread::sleep_for(std::chrono::milliseconds(10));
	//	}
	//}

	//m_callback_image_queue.push({ -1, 0 });

	return SUCCESS_;
}

//int CameraSystem::StartCapture_HardwareTriggerFirst(CaptureInfo info)
//{
//	//Camera Init
//	if (!m_isHardwareTriggerSupport)return NOTSUPPORT_;
//
//	CHECK_STATUS(m_pCamera->CameraDisarm());
//	CHECK_STATUS(m_pCamera->SetFramePerTrigger(1));
//
//	CHECK_STATUS(m_pCamera->SetTriggerMode(CameraTriggerMode::HARDWARE_TRIGGER, (HardwareTriggerPolarity)info.hwTrigPolarity));
//	CHECK_STATUS(m_pCamera->CameraArm());
//	auto result = 0;
//	while (true)
//	{
//		if (!m_isCapturing || m_CaptureError || m_callbackFrame >= 1)
//		{
//			result = m_pCamera->CameraDisarm();
//			break;
//		}
//	}
//	return result;
//}
//
//int CameraSystem::StartCapture_HardwareTriggerEach(CaptureInfo info)
//{
//	if (!m_isHardwareTriggerSupport)return NOTSUPPORT_;
//	//Camera Init
//	CHECK_STATUS(m_pCamera->CameraDisarm());
//	CHECK_STATUS(m_pCamera->SetFramePerTrigger(1));
//
//	CHECK_STATUS(m_pCamera->SetTriggerMode(CameraTriggerMode::HARDWARE_TRIGGER, (HardwareTriggerPolarity)info.hwTrigPolarity));
//	CHECK_STATUS(m_pCamera->CameraArm());
//
//	auto result = 0;
//	while (true)
//	{
//		Sleep(1);
//		if (!m_isCapturing || m_CaptureError || m_callbackFrame >= info.frameCount)
//		{
//			result = m_pCamera->CameraDisarm();
//			break;
//		}
//	}
//	return result;
//}
//
//int CameraSystem::StartCapture_HardwareBulb(CaptureInfo info)
//{
//	//Camera Init
//	if (!m_isHardwareTriggerSupport)return NOTSUPPORT_;
//
//	CHECK_STATUS(m_pCamera->CameraDisarm());
//	CHECK_STATUS(m_pCamera->SetFramePerTrigger(1));
//
//	CHECK_STATUS(m_pCamera->SetTriggerMode(CameraTriggerMode::BULB, (HardwareTriggerPolarity)info.hwTrigPolarity));
//	CHECK_STATUS(m_pCamera->CameraArm());
//	auto result = 0;
//	while (true)
//	{
//		Sleep(1);
//		if (!m_isCapturing || m_CaptureError || m_callbackFrame >= info.frameCount)
//		{
//			result = m_pCamera->CameraDisarm();
//			break;
//		}
//	}
//	return result;
//}

int CameraSystem::AutoExposure()
{
	double eMin, eMax, eInc;
	CHECK_STATUS(m_pCamera->GetExposureTimeRange(&eMin, &eMax, &eInc));

	double eTime;
	CHECK_STATUS(m_pCamera->GetExposureTime(&eTime));
	double preExposureTime = eTime;//ms
	double nextExposureTime = 0;

	CHECK_STATUS(m_pCamera->SetTriggerMode(CameraTriggerMode::SOFTWARE_TRIGGER));
	CHECK_STATUS(m_pCamera->SetFramePerTrigger(1));
	CHECK_STATUS(m_pCamera->CameraArm());
	CHECK_STATUS(m_pCamera->IssueSoftwareTirggle());

	while (true)
	{
		if (m_isAutoExposuring && m_isLiving)
		{
			if (m_isAutoImageReady)
			{
				//auto is_hdl_valid = m_hdl_AutoExposuring >= 0;
				//std::string errorMessage = "Pre exposure time is: " + std::to_string(eTime) + ".";
				//WriteLog(LogLevel::Error, errorMessage.c_str());
				//if (is_hdl_valid)
				//{
					CHECK_STATUS(CalculateNextExposure(m_hdl_AutoExposuring, preExposureTime, &nextExposureTime, eMin, eMax));
				//}

				m_isAutoImageReady = false;

				//if (is_hdl_valid)
				//{
					bool is_stable = abs(nextExposureTime - preExposureTime) <= 0.01;
					//AutoExposureImpl.cpp _line_264
					// 3000 is the limit we will allow AE to push exposure
					// use us 
					if (nextExposureTime > 3000 * 1000)
						nextExposureTime = 3000 * 1000;
					if (nextExposureTime < eMin)
						nextExposureTime = eMin;
					if (!is_stable)
					{
						CHECK_STATUS(m_pCamera->CameraDisarm());
						//CHECK_STATUS(m_pCamera->SetFramePerTrigger(1));
						CHECK_STATUS(m_pCamera->SetExposureTime(nextExposureTime));
						CHECK_STATUS(m_pCamera->CameraArm());
						preExposureTime = nextExposureTime;
					}

					if (m_autoExposureCallback != nullptr)
					{
						//std::string errorMessage = "Exposure time is: " + std::to_string(nextExposureTime) + ".";
						//WriteLog(LogLevel::Error, errorMessage.c_str());
						m_autoExposureCallback(is_stable ? 1 : 0, nextExposureTime);
					}
				//}
					/*CHECK_STATUS(m_pCamera->IssueSoftwareTirggle());*/
				CHECK_STATUS(m_pCamera->IssueSoftwareTirggle());
			}
			//else
			//{
			//	Sleep(5);
			//}
		}
		else
			break;
	}

	return SUCCESS_;
}

int CameraSystem::StartLive_Software()
{
	if (m_isAutoExposuring)
	{
		if (m_hdl_AutoExposuring < 0) return ERROR_;
		thread_auto_exposure = new std::thread(&CameraSystem::AutoExposure, this);		
	}
	else
	{
		CHECK_STATUS(m_pCamera->SetTriggerMode(CameraTriggerMode::SOFTWARE_TRIGGER));
		CHECK_STATUS(m_pCamera->SetFramePerTrigger(0));
		CHECK_STATUS(m_pCamera->CameraArm());
		CHECK_STATUS(m_pCamera->IssueSoftwareTirggle());
	}
	return SUCCESS_;
}

int CameraSystem::CalculateNextExposure(int hdl_calc, double preExposureTime, double* nextExposureTime, double exposureMin, double exposureMax)
{
	p2d_info info;
	CHECK_STATUS(p2d_get_info(hdl_calc, &info));


	int histSize = 1 << info.valid_bits;
	unsigned int* hist = (unsigned int*)calloc(histSize, sizeof(unsigned int));
	if (hist == nullptr)
		return ERR_CAMSYS_MALLOCBUFFER;
	CHECK_STATUS(p2d_histogram(hdl_calc, 0.0f, (float)histSize, hist, histSize));

	// todo: There is an issue where a current exposure of 0.000 will always return a target exposure of 0.000
	auto exposureTime_ms = max(preExposureTime, 0.001);
	auto maximumIntensityBasedOnBitDepth = histSize - 1;

	// The target exposure specified by ming is 20% below max, presumably because it was assumed that some population above the 90th percentile would be at or near saturation.
	//auto targetIntensity = (int)(0.8 * maximumIntensityBasedOnBitDepth); // WHERE DO WE WANT TO PUSH THOSE VALUES? LET'S SAY 1033 VALUE IS WHERE WE HAVE 96% OF PIXELS BELOW IT.
	auto targetIntensity = (int)(0.8 * maximumIntensityBasedOnBitDepth);

	//auto totalNumberOfPixels = autoExposureParams.RoiWidth_pixels * autoExposureParams.RoiHeight_pixels; //roiWidth_pixels * roiHeight_pixels; // TODO: channels
	auto totalNumberOfPixels = info.x_size * info.y_size; //roiWidth_pixels * roiHeight_pixels; // TODO: channels

	// TODO: assuming histogram is averaged, in the case of multi-channel data

	// Determine if the image is heavily saturated by looking only at the upper Nth percentile, where N is defined by targetSaturationFactor
	int numberOfSaturatedPixels = 0;
	// Define the effective saturation level, slightly less than max.
	int targetSaturationIntensity = (int)(maximumIntensityBasedOnBitDepth * 0.98);

	// start accumulating the histogram values starting from the maximum intensity and decrementing to the target saturation intensity MP
	for (int intensity = maximumIntensityBasedOnBitDepth; intensity >= targetSaturationIntensity; intensity--)
	{
		numberOfSaturatedPixels += (unsigned)hist[intensity];
	}

	// TODO: rename: allowableNumSaturatedPixels
	auto numberOfPixelsThatCanBeSaturated = 0.25 * totalNumberOfPixels;

	// Special case to speed up the algorithm:
	// If the number of saturated pixels exceeds the allowed
	// fraction of the total, there are too many. So set exposure
	// to 100ms and gain to minimum.
	if (numberOfSaturatedPixels > numberOfPixelsThatCanBeSaturated)
	{
		const double scaleFactorForQuickExposureJump = 0.2;//0.05;
		*nextExposureTime = preExposureTime * scaleFactorForQuickExposureJump;
		return SUCCESS_;
	}

	//else there are not too many saturated pixels, and we can
	//proceed with creating the cumulative histogram, starting
	//from the minimum value find the index at which the cumulative
	//number of pixels equals the desired percentile of the total. That
	//value will be considered the current intensity.
	//initialize the variable that will be used to hold the current intensity of the Nth percentile of all pixels
	int calculatedThresholdIntensity = 0;

	//the image intensity statistic to be used to establish aims is a percentile. The intensity of the Nth percentile is considered the image intensity. MP
	// Determines how far up the histogram we want to go before we stop, allowing us to throw away the upper values. It is the point at which the cumulative histogram contains 96% of the pixels. This avoids outliers/hot pixels. In other words, at what point do I have enough pixels to represent the overall image "intensity."
	int numberOfPixelsUpToOutliersThreshold = (int)(0.96 * totalNumberOfPixels);

	int numberOfPixelsSoFar = 0;
	for (int intensity = 0; intensity < histSize; intensity++)
	{
		// TODO: summed channels messes this up
		numberOfPixelsSoFar += (int)hist[intensity];
		if (numberOfPixelsSoFar > numberOfPixelsUpToOutliersThreshold)
		{
			calculatedThresholdIntensity = intensity;
			break;
		}
	}
	double intensityRatioTargetToCalculated = targetIntensity / (double)calculatedThresholdIntensity;
	double intensityTolerance = targetIntensity * 0.02;
	// If the image is almost black, let's not guess what to do.
	if (calculatedThresholdIntensity == 0 || abs(targetIntensity - calculatedThresholdIntensity) <= intensityTolerance)
	{
		*nextExposureTime = preExposureTime;
		return SUCCESS_;
	}

	////finds the difference, since these values are related to dB, and difference in log space is a ratio in linear space, which is to be computed
	//double currentGainOffsetFromMinimum_dB = autoExposureParams.Gain_dB - autoExposureParams.GainMin_dB;

	//// Convert dB to linear space so that gain can be compared to exposure in the effect on the image intensity.
	//double currentGainRatio = pow(10, currentGainOffsetFromMinimum_dB / 20);
	//// autoExposureResultParameters.currentGainRatio = currentGainRatio;

	double minimumExposureTime_ms_notZero = max(exposureMin, 0.001);

	// since we'll be extrapolating exposures and gains we must account for their offsets (the minimum)
	//MP: next calculate the exposure ratio
	double currentExposureTimeRatio = exposureTime_ms / minimumExposureTime_ms_notZero;

	/*double totalTargetRatio = autoExposureParams.IsGainAdjustable ? intensityRatioTargetToCalculated * currentGainRatio * currentExposureTimeRatio : intensityRatioTargetToCalculated * currentExposureTimeRatio;*/
	double totalTargetRatio = intensityRatioTargetToCalculated * currentExposureTimeRatio;

	double maxAvailableExposureTimeRatio = exposureMax / minimumExposureTime_ms_notZero;

	//double targetExposureTime_ms;
	//double targetGain_dB;

	// Priorities: minimize gain -> use exposure time to compensate for image intensity until it reaches maximum allowed by the algorithm (not necessarily the maximum possible)
	if (totalTargetRatio < maxAvailableExposureTimeRatio)
	{
		//targetGain_dB = autoExposureParams.GainMin_dB;
		// This multiplies back out the minimum exposure time that was used in the ratio.
		//targetExposureTime_ms = minimumExposureTime_ms_notZero * totalTargetRatio;
		*nextExposureTime = minimumExposureTime_ms_notZero * totalTargetRatio;
	}
	else
	{
		*nextExposureTime = exposureMax;
		//targetExposureTime_ms = exposureMax;
		//double finalRatio = totalTargetRatio / maxAvailableExposureTimeRatio;
		//targetGain_dB = 20.0 * log10(finalRatio);
	}

	return SUCCESS_;
}

int CameraSystem::AutoExposureStatusChange(bool isAutoExposure)
{
	if (m_isLiving)
	{
		if (m_isAutoExposuring == isAutoExposure)return SUCCESS_;
		//must be prepared before
		if (m_hdl_AutoExposuring < 0) return ERROR_;
		m_isAutoExposuring = isAutoExposure;

		if (thread_auto_exposure != nullptr && thread_auto_exposure->joinable())
		{
			thread_auto_exposure->join();
			delete thread_auto_exposure;
			thread_auto_exposure = nullptr;
		}

		m_isAutoImageReady = false;
		CHECK_STATUS(m_pCamera->CameraDisarm());

		CHECK_STATUS(StartLive_Software());
	}
	return SUCCESS_;
}
#pragma endregion

int CameraSystem::Open(const char* serialNumber)
{
	if (m_pCamera == nullptr)
	{
		m_pCamera = new AlliedCamera();
		s_currentCamera = this;
	}
	CHECK_STATUS(m_pCamera->Open(serialNumber));
	CHECK_STATUS(m_pCamera->SetRecieveImageCallback(&CameraSystem::CameraCallback));

	int colorMode = 0;
	CHECK_STATUS(m_pCamera->GetColorMode(&colorMode));
	m_cameraType = (CameraType)colorMode;

	double fps_min, fps_max;
	CHECK_STATUS(m_pCamera->GetFrameRateControlValueRange(&fps_min, &fps_max));
	m_isFrameRateControlSupport = fps_max > 0;
	if (m_isFrameRateControlSupport)
	{
		auto result = m_pCamera->SetFrameRateControlEnabled(0);
		if (result != 0)
			return result;
	}

	int isHdSupport = 0;
	CHECK_STATUS(m_pCamera->GetIsOperationModeSupported(CameraTriggerMode::HARDWARE_TRIGGER, &isHdSupport));
	m_isHardwareTriggerSupport = isHdSupport == 1;

	//m_isDisconnected = false;
	_sn = serialNumber;
	return SUCCESS_;
}

int CameraSystem::Close()
{
	_sn = "";
	if (m_pCamera != nullptr)
	{
		PostExecutionCore();
		delete m_pCamera;
		m_pCamera = nullptr;
	}

	return 0;
}

long CameraSystem::PreCapture(CaptureInfo* info)
{
	PostExecutionCore();

	m_savedFrameCount = 0;
	m_CaptureErrorCode = 0;
	m_callbackArrrived = false;
	m_pCaptureInfo = new CaptureInfoEx(info);
	m_preFrameTime = 0;

	CHECK_STATUS(PreExecutionCore(info->averageFrames));

	return SUCCESS_;
}

long CameraSystem::PostCapture(CaptureInfo* info)
{
	//if (!m_isCapturing) return SUCCESS_;
	//m_isCapturing = false;

	//WriteLog(LogLevel::Error, "Post capture.");
	//std::this_thread::sleep_for(std::chrono::milliseconds(10));

	PostExecutionCore();

	if (m_isCapturing)
		m_isCapturing = false;

	if (m_pCaptureInfo)
	{
		delete m_pCaptureInfo;
		m_pCaptureInfo = nullptr;
	}

	std::unique_lock<std::mutex> lk(cv_mut);
	lk.unlock();
	cv.notify_one();

	return SUCCESS_;
}


int CameraSystem::StartCapture(CaptureInfo info, PreparedCallback callback)
{
	auto result = PreCapture(&info);
	if (result != 0)
	{
		callback();
		return result;
	}

	m_isCapturing = true;
	thread_deal = new std::thread(&CameraSystem::DealProcessedImages, this);
	thread_proc = new std::thread(&CameraSystem::ProcessCaptureImages, this);

	callback();

	//switch (info.trigMode)
	//{
	//case TriggerMode::InternalTrigger:
	//	result = StartCapture_InteralTrigger(info);
	//	break;
	//case TriggerMode::SoftwareTrigger:
		result = StartCapture_SoftwareTrigger();
	//	break;
	//case TriggerMode::HardwareTriggerFirst:
	//	result = StartCapture_HardwareTriggerFirst(info);
	//	if (result == 0)
	//	{
	//		auto leftCount = (int)info.frameCount - 1;
	//		if (leftCount > 0)
	//			result = StartCapture_SoftwareTrigger(info);
	//	}
	//	break;
	//case TriggerMode::HardwareTriggerEach:
	//	result = StartCapture_HardwareTriggerEach(info);
	//	break;
	//case TriggerMode::HardwareBulb:
	//	result = StartCapture_HardwareBulb(info);
	//	break;
	//default: break;
	//}

	//exit thread
	//m_callback_image_queue.push({ -1, 0 });

	//101 means stop manual
	if (result != 0 && result != 101)
	{
		std::string errorMessage = "Start snapshot failed. Error is " + std::to_string(result) + ".";
		WriteLog(LogLevel::Error, errorMessage.c_str());
	}

	CHECK_STATUS(PostCapture(&info));
	return result;
}

int CameraSystem::StopCapture()
{
	if (!m_isCapturing) return SUCCESS_;
	m_isCapturing = false;
	//m_callback_image_queue.push({ -1, 0 });
	//std::this_thread::sleep_for(std::chrono::milliseconds(5));
	//cv.notify_all();
	//if (thread_deal.joinable())
	//	thread_deal.join();
	//if (thread_proc.joinable())
	//	thread_proc.join();
	//CHECK_STATUS(m_pCamera->CameraDisarm());
	//CHECK_STATUS(PostCapture(m_pCurrentSeriesInfo));
	//WriteLog(LogLevel::Error, "Wait post.");

	std::unique_lock<std::mutex> lock(cv_mut);
	cv.wait(lock);

	//WriteLog(LogLevel::Error, "Post done.");

	return SUCCESS_;
}

int CameraSystem::StartLive()
{
	PostExecutionCore();
	CHECK_STATUS(PreExecutionCore());

	if (m_hdl_AutoExposuring >= 0)
	{
		CHECK_STATUS(p2d_destroy_image(m_hdl_AutoExposuring));
		m_hdl_AutoExposuring = -1;
	}

	switch (m_cameraType)
	{
	case CameraType::CamMono:
	{
		m_hdl_AutoExposuring = p2d_create_image();
		p2d_init_image(m_hdl_AutoExposuring, m_backup_p2d_img_info->x_size, m_backup_p2d_img_info->y_size, m_backup_p2d_img_info->pix_type, m_backup_p2d_img_info->channels, m_backup_p2d_img_info->valid_bits);
		break;
	}
	case CameraType::CamColorBayer:
	{
		m_hdl_AutoExposuring = p2d_create_image();
		p2d_init_image(m_hdl_AutoExposuring, m_backup_p2d_img_info->x_size, m_backup_p2d_img_info->y_size, p2d_data_format::P2D_8U, p2d_channels::P2D_CHANNELS_1, 8);
		break;
	}
	case CameraType::CamPolar:
	{
		m_hdl_AutoExposuring = p2d_create_image();
		p2d_init_image(m_hdl_AutoExposuring, m_backup_p2d_img_info->x_size, m_backup_p2d_img_info->y_size, m_backup_p2d_img_info->pix_type, m_backup_p2d_img_info->channels, m_backup_p2d_img_info->valid_bits);
		break;
	}
	}

	if (m_hdl_AutoExposuring < 0)
		return ERROR_;

	m_isLiving = true;
	m_isAutoImageReady = false;
	//thread_deal = std::thread(&CameraSystem::DealProcessedImages, this);
	thread_proc = new std::thread(&CameraSystem::ProcessLiveImages, this);

	auto result = 0;
	//switch (info.trigMode)
	//{
	//case TriggerMode::InternalTrigger:
	//case TriggerMode::SoftwareTrigger:
	//case TriggerMode::HardwareTriggerFirst:
	//case TriggerMode::HardwareTriggerEach:
	//case TriggerMode::HardwareBulb:
		//CHECK_STATUS(m_pCamera->SetTriggerMode(CameraTriggerMode::SOFTWARE_TRIGGER));
		////CHECK_STATUS(CheckCameraFrameRateStatus());
		//CHECK_STATUS(m_pCamera->SetFramePerTrigger(0));
		//CHECK_STATUS(m_pCamera->CameraArm());
		//CHECK_STATUS(m_pCamera->IssueSoftwareTirggle());
	m_isAutoExposuring = false;//info.isAutoExposure;
	CHECK_STATUS(StartLive_Software());
	//break;
	//case TriggerMode::HeardwareTriggerFirst:
	//	if (m_isHardwareTriggerSupport)
	//	{
	//		CHECK_STATUS(m_pCamera->SetTriggerMode(CameraTriggerMode::HARDWARE_TRIGGER, (HardwareTriggerPolarity)info.hwTrigPolarity));
	//		//CHECK_STATUS(CheckCameraFrameRateStatus());
	//		CHECK_STATUS(m_pCamera->SetFramePerTrigger(1));
	//		CHECK_STATUS(m_pCamera->CameraArm());
	//		while (true)
	//		{
	//			if (!m_isLiving || m_CaptureError || m_callbackFrame >= 1)
	//			{
	//				result = m_pCamera->CameraDisarm();
	//				break;
	//			}
	//		}
	//		if (!m_isLiving || m_CaptureError || result != 0)
	//			return result;
	//		CHECK_STATUS(m_pCamera->SetTriggerMode(CameraTriggerMode::SOFTWARE_TRIGGER));
	//		//CHECK_STATUS(CheckCameraFrameRateStatus());
	//		CHECK_STATUS(m_pCamera->SetFramePerTrigger(0));
	//		CHECK_STATUS(m_pCamera->CameraArm());
	//		CHECK_STATUS(m_pCamera->IssueSoftwareTirggle());
	//	}

	//	break;
	//case TriggerMode::HeardwareTriggerEach:
	//	if (m_isHardwareTriggerSupport)
	//	{
	//		CHECK_STATUS(m_pCamera->SetTriggerMode(CameraTriggerMode::HARDWARE_TRIGGER, (HardwareTriggerPolarity)info.hwTrigPolarity));
	//		//CHECK_STATUS(CheckCameraFrameRateStatus());
	//		CHECK_STATUS(m_pCamera->SetFramePerTrigger(1));
	//		CHECK_STATUS(m_pCamera->CameraArm());
	//	}
	//	break;
//default: break;
//}
	return result;
}

int CameraSystem::StopLive()
{
	if (!m_isLiving) return 0;
	m_isAutoExposuring = false;
	m_isLiving = false;

	PostExecutionCore();
	return SUCCESS_;
}

int CameraSystem::StartSnapshot(SnapshotInfo info)
{
	PostExecutionCore();
	CHECK_STATUS(PreExecutionCore(info.averageFrames));
	m_callbackArrrived = false;
	m_isSnapshoting = true;
	m_pSnapshotInfo = &info;
	thread_proc = new std::thread(&CameraSystem::ProcessSnapshotImage, this);

	CHECK_STATUS(m_pCamera->SetTriggerMode(CameraTriggerMode::SOFTWARE_TRIGGER));
	CHECK_STATUS(m_pCamera->SetFramePerTrigger(1));
	CHECK_STATUS(m_pCamera->CameraArm());
	CHECK_STATUS(m_pCamera->IssueSoftwareTirggle());

	auto result = 0;
	while (true)
	{
		if (!m_isSnapshoting)
			break;

		if (m_callbackArrrived)
		{
			m_callbackArrrived = false;
			if (!m_isSnapshoting || m_callbackFrame >= info.averageFrames)
				break;

			result = m_pCamera->AcquisitionStop();
			if (result != 0)
			{
				std::string errorMessage = "Stop acquisition failed. Error is " + std::to_string(result) + ".";
				WriteLog(LogLevel::Error, errorMessage.c_str());
				return ERR_CAMSYS_SWTRIGGER_FAILED;
			}

			result = m_pCamera->IssueSoftwareTirggle();
			if (result != 0)
			{
				std::string errorMessage = "Software trigger the camera failed. Error is " + std::to_string(result) + ".";
				WriteLog(LogLevel::Error, errorMessage.c_str());
				return ERR_CAMSYS_SWTRIGGER_FAILED;
			}
		}
		if (!m_isSnapshoting || m_callbackFrame >= info.averageFrames)
			break;
	}

	//m_callback_image_queue.push({ -1, 0 });
	if (result != 0)
	{
		std::string errorMessage = "Start snapshot failed. Error is " + std::to_string(result) + ".";
		WriteLog(LogLevel::Error, errorMessage.c_str());
	}

	CHECK_STATUS(PostExecutionCore());
	std::unique_lock<std::mutex> lk(cv_mut);
	lk.unlock();
	cv.notify_one();

	return result;
}

int CameraSystem::StopSnapshot()
{
	if (!m_isSnapshoting) return 0;
	m_isSnapshoting = false;

	std::unique_lock<std::mutex> lock(cv_mut);
	cv.wait(lock);

	return SUCCESS_;
}

CameraSystem::CameraSystem()
	:m_imageCallback(nullptr), m_pCaptureInfo(nullptr),
	m_isLiving(false), m_isCapturing(false), m_CaptureErrorCode(0),
	m_callbackFrame(0), m_processedFrameCount(0), m_savedFrameCount(0),
	m_callbackArrrived(false)
{
	m_cameraType = CameraType::CamMono;
	m_isFrameRateControlSupport = false;
	m_isHardwareTriggerSupport = false;
	m_pCamera = nullptr;
	m_backup_p2d_img_handle = -1;
	m_backup_p2d_img_info = nullptr;
	m_hdl_AutoExposuring = -1;
	m_isAutoExposuring = false;
	m_isAutoImageReady = false;
	thread_deal = nullptr;
	thread_proc = nullptr;
	thread_auto_exposure = nullptr;
	//m_video_frame_duration(0);
}

CameraSystem::~CameraSystem()
{
	//don't do close
	//CameraSystem will be dispose when app shutdown,camera is closed before shutdown.
	//Close();
}

void CameraSystem::CameraDisconnectCallback(const char* sn)
{
	if (sn == nullptr) return;
	std::string msg = "Camera : " + std::string(sn) + " is disconnected.";
	WriteLog(LogLevel::Error, msg.c_str());
	if (s_currentCamera->m_pCamera != nullptr && s_currentCamera->_sn == std::string(sn))
	{
		//s_currentCamera->m_isDisconnected = true;
		s_currentCamera->m_pCamera->IsRemoved = true;
		s_currentCamera->m_disconnectCallback();
	}
}

void CameraSystem::CameraCallback(void* buffer, unsigned int frameNum, int width, int height)
{
	/*auto caltime = clock();
	auto dura = (long)caltime - s_currentCamera->test_call_back_time;
	std::string errorMessage = "Callback time: " + std::to_string(dura) + ".";
	WriteLog(LogLevel::Error, errorMessage.c_str());
	s_currentCamera->test_call_back_time = (long)caltime;*/

	//if (s_currentCamera->m_isDisconnected)
	//	return;

	long long currentClock = std::chrono::duration_cast<std::chrono::microseconds>(std::chrono::high_resolution_clock::now().time_since_epoch()).count();

	if (width < 0 || height < 0)
	{
		//std::string msg = "Camera";
		//if (buffer != nullptr)
		//{
		//	msg += " : " + std::string((char*)buffer);
		//}
		//msg += " is disconnected.";
		//WriteLog(LogLevel::Error, msg.c_str());
		//s_currentCamera->m_disconnectCallback();
		//s_currentCamera->m_isDisconnected = true;
		return;
	}

	if (s_currentCamera->m_backup_p2d_img_handle < 0 || buffer == nullptr) return;

	bool canCallBackWork = false;
	if (s_currentCamera->m_isCapturing)
	{
		canCallBackWork = (s_currentCamera->m_pCaptureInfo->frameCount > s_currentCamera->m_callbackFrame) && (s_currentCamera->m_CaptureErrorCode == 0);
	}

	if (s_currentCamera->m_isSnapshoting)
	{
		canCallBackWork = (s_currentCamera->m_pSnapshotInfo->averageFrames > s_currentCamera->m_callbackFrame) && (s_currentCamera->m_CaptureErrorCode == 0);
	}

	if (s_currentCamera->m_isLiving)
	{
		/*canCallBackWork = true;
		CameraSystemFrameInfo camsysinfo;
		while (s_currentCamera->m_callback_image_queue.try_dequeue(camsysinfo))
		{
			p2d_destroy_image(camsysinfo.p2d_img_hdl);
		}*/

		//auto size = s_currentCamera->m_callback_image_queue.size();

		//if (size > 1)
		//	return;
		canCallBackWork = true;
	}

	++(s_currentCamera->m_callbackFrame);

	p2d_info* info = s_currentCamera->m_backup_p2d_img_info;
	auto bytes = (info->valid_bits + 7) / 8;
	size_t count = info->x_size * info->y_size * (int)info->channels;

	if (canCallBackWork)
	{
		//timer.Stop();
		//double time_ms = timer.Elapsed_ms();
		//timer.Start();
		//WriteLog(LogLevel::Info, std::to_string(time_ms).c_str());

		info->data_buf = buffer;
		p2d_init_image_ex(s_currentCamera->m_backup_p2d_img_handle, info);

		if (s_currentCamera->m_callback_transfer_p2d_handle_queue.empty())
		{
			int temp_hdl = p2d_create_image();
			int8_t res = p2d_init_image(temp_hdl, info->x_size, info->y_size, info->pix_type, info->channels, info->valid_bits);
			if (STATUS_OK != res)
			{
				s_currentCamera->m_CaptureErrorCode = ERR_CAMSYS_INIT_P2D_FAILED;
				p2d_destroy_image(temp_hdl);
				return;
			}
			res = p2d_copy(s_currentCamera->m_backup_p2d_img_handle, temp_hdl);
			if (STATUS_OK != res)
			{
				s_currentCamera->m_CaptureErrorCode = ERR_CAMSYS_P2D_OPERATE_FAILED;
				p2d_destroy_image(temp_hdl);
				return;
			}
			s_currentCamera->m_callback_image_queue.push({ temp_hdl, s_currentCamera->m_callbackFrame, currentClock });
		}
		else
		{
			int temp_hdl = s_currentCamera->m_callback_transfer_p2d_handle_queue.front();
			int8_t res = p2d_copy(s_currentCamera->m_backup_p2d_img_handle, temp_hdl);
			if (STATUS_OK != res)
			{
				s_currentCamera->m_CaptureErrorCode = ERR_CAMSYS_P2D_OPERATE_FAILED;
				return;
			}
			s_currentCamera->m_callback_transfer_p2d_handle_queue.pop();
			s_currentCamera->m_callback_image_queue.push({ temp_hdl, s_currentCamera->m_callbackFrame, currentClock });
		}

		s_currentCamera->m_callbackArrrived = true;
		//if (s_currentCamera->m_isCapturing)
		//{
		//	std::string callbackMessage = "Frame arrived with current frame count : " + std::to_string(s_currentCamera->m_callbackFrame) + ".";
		//	WriteLog(LogLevel::Error, callbackMessage.c_str());
		//}
	}
	//else
	//{
	//	auto v= (s_currentCamera->m_callbackFrame);
	//}
	//if (s_currentCamera->m_isCapturing && !s_currentCamera->m_CaptureError)
	//{
	//	s_currentCamera->m_callbackArrrived = true;

	//	++(s_currentCamera->m_callbackFrame);

	//	//Notify the last frame arrived
	//	if (s_currentCamera->m_pCurrentSeriesInfo->frameCount <= frameNum)
	//	{
	//		s_currentCamera->m_lastFrameSemaphore.notify();
	//	}
	//}
}

//Only snapshot need deal processed images
void CameraSystem::DealProcessedImages()
{
	unsigned int frameCount = 1;
	//Frame count == 0 means no time series
	if (m_pCaptureInfo->frameCount != 0)
		frameCount = m_pCaptureInfo->frameCount;

	while (true)
	{
		if (!m_processed_image_queue.empty())
		{
			CameraSystemFrameInfo camera_frame_info = m_processed_image_queue.front();
			m_processed_image_queue.pop();
			if (camera_frame_info.p2d_img_hdl < 0 || !m_isCapturing)
				break;

#ifdef TEST_SPEED
			//timer_save.Start();
#endif
			wchar_t file_path[_MAX_PATH] = L"";

			p2d_info current_frame_info;
			int status = p2d_get_info(camera_frame_info.p2d_img_hdl, &current_frame_info);

			ImageInfo i_info{};
			i_info.width = current_frame_info.x_size;
			i_info.height = current_frame_info.y_size;
			i_info.line_bytes = ((current_frame_info.valid_bits + 7) / 8) * current_frame_info.x_size * (int)current_frame_info.channels;
			i_info.pixel_type = (int)current_frame_info.pix_type;
			i_info.valid_bits = (unsigned short)current_frame_info.valid_bits;
			i_info.image_type = (int)(current_frame_info.channels == p2d_channels::P2D_CHANNELS_3 ? ImageType::IMAGE_RGB : ImageType::IMAGE_GRAY);
			i_info.compression_mode = (int)CompressionMode::COMPRESSIONMODE_NONE;
			i_info.slot_index = camera_frame_info.frame_num;

			if (status == 0)
			{
				switch (m_pCaptureInfo->baseInfo->saveType)
				{
				case CaptureSaveType::CaptrueMultiTif:
				{
					if (m_multi_tif_hdl < 0)
					{
						swprintf_s(file_path, L"%s\\%s_001.tif", m_pCaptureInfo->baseInfo->folderName, m_pCaptureInfo->baseInfo->prefixName);

						m_multi_tif_hdl = open_tiff(file_path, OpenMode::CREATE_MODE);
						if (m_multi_tif_hdl < 0)
						{
							status = ERR_TIFF_HANDLE_NOT_VALID;
							break;
						}
					}
					status = SaveImage(m_multi_tif_hdl, camera_frame_info.p2d_img_hdl, &i_info);
					break;
				}
				case CaptureSaveType::CaptureSingleTif:
				{
					swprintf_s(file_path, L"%s\\%s_%04d.tif", m_pCaptureInfo->baseInfo->folderName, m_pCaptureInfo->baseInfo->prefixName, camera_frame_info.frame_num);

					int tiff_handle = open_tiff(file_path, OpenMode::CREATE_MODE);
					if (tiff_handle < 0)
					{
						status = tiff_handle;
						break;
					}
					status = SaveImage(tiff_handle, camera_frame_info.p2d_img_hdl, &i_info);
					close_tiff(tiff_handle);
					break;
				}
				/*
				case CaptureSaveType::CaptureVideo:
				{
					break;
				}
				case CaptureSaveType::CaptureJpeg:
					//FILE* jpeg_file = _wfsopen(m_pCaptureInfo->baseInfo->fileFullPath, L"wb+", _SH_DENYWR);
					//if (jpeg_file == nullptr)
					//{
					//	status = ERR_CREATE_JPEG_FILE_FALIED;
					//	break;
					//}
					//tjhandle jpeg_handle = tjInitCompress();
					//if (jpeg_handle == nullptr)
					//{
					//	status = ERR_INIT_TURBOJPEG_COMPRESS_FAILED;
					//	break;
					//}

					//int pixelFormat = 6;	//TJPF_GRAY
					//int subsamples = 3;		//TJSAMP_GRAY;
					//if (m_cameraType != CameraType::CamMono && m_cameraType != CameraType::CamPolar)
					//{
					//	pixelFormat = 0;		//TJPF_RGB
					//	subsamples = 0;			//TJSAMP_444
					//}
					//unsigned long dst_size = 0;
					//unsigned char* dst_buf = nullptr;

					//status = tjCompress2(jpeg_handle, (const unsigned char*)current_frame_info.data_buf,
					//	current_frame_info.x_size, current_frame_info.line_bytes, current_frame_info.y_size,
					//	pixelFormat, &dst_buf, &dst_size, subsamples, 99, TJFLAG_ACCURATEDCT);
					//tjDestroy(jpeg_handle);
					//fwrite(dst_buf, 1, dst_size, jpeg_file);
					//tjFree(dst_buf);
					//fclose(jpeg_file);
					break;
				}
				*/
				default:
					break;
				}
			}
			p2d_destroy_image(camera_frame_info.p2d_img_hdl);
			if (status != STATUS_OK)
			{
				m_CaptureErrorCode = status;
				break;
			}

			//if (m_ImageCallbackAction != nullptr)
			//{
			//	m_ImageCallbackAction(camera_frame_info.p2d_img_hdl, camera_frame_info.frame_num);
			//}
#ifdef TEST_SPEED
				/*timer_save.Stop();
				double elapsed = timer_save.Elapsed_ms();
				std::string str_save = "Save frame " + std::to_string(camera_frame_info.frame_num) + " elapsed time : " + std::to_string(elapsed);
				WriteLog(LogLevel::Info, str_save.c_str());*/

				//std::string str_save = "Save frame " + std::to_string(camera_frame_info.frame_num);
				//WriteLog(LogLevel::Info, str_save.c_str());
#endif
			++m_savedFrameCount;
			if (m_savedFrameCount >= m_pCaptureInfo->baseInfo->captureSlotSettingsCount)
				break;
		}
		else
		{
			std::this_thread::sleep_for(std::chrono::milliseconds(1));
			if (!m_isCapturing)
				break;
		}
	}

	switch (m_pCaptureInfo->baseInfo->saveType)
	{
	case CaptureSaveType::CaptrueMultiTif:
		if (m_multi_tif_hdl >= 0)
		{
			close_tiff(m_multi_tif_hdl);
			m_multi_tif_hdl = -1;
		}
		break;
	//case CaptureSaveType::CaptureVideo:
	//	if (m_video_started)
	//	{
	//		VideoSaving_Stop();
	//		m_video_started = false;
	//	}
	//	break;
	default:
		break;
	}
}

void CameraSystem::ProcessCaptureImages()
{
	auto info = s_currentCamera->m_backup_p2d_img_info;

	unsigned int frameNumber = 0;
	unsigned int frameCount = 1;
	//Frame count == 0 means no time series
	if (m_pCaptureInfo->frameCount != 0)
		frameCount = m_pCaptureInfo->frameCount;

	double* pmin = m_pCaptureInfo->baseInfo->min;
	double* pmax = m_pCaptureInfo->baseInfo->max;
	double m_value = 1.0;
	double a_value = 0;

	auto max_value = (1 << info->valid_bits) - 1;
	double shift_m_value = std::pow(2, 8 - (int)info->valid_bits);

	switch (m_cameraType)
	{
	case CameraType::CamMono:
	case CameraType::CamPolar:
	{
		//auto expand_max = ((double)max_value / 255.0) * pmax[0];
		//auto expand_min = ((double)max_value / 255.0) * pmax[0];
		m_value = max_value / (pmax[0] - pmin[0]); //pmax[0] - pmin[0] < 3 ? 1.0 : 255.0 / (pmax[0] - pmin[0]);
		a_value = 0 - (pmin[0] * m_value); //pmax[0] - pmin[0] < 3 ? 0 : (0 - (pmin[0] * m_value));
		if (m_pCaptureInfo->baseInfo->saveType == CaptureSaveType::CaptureVideo || m_pCaptureInfo->baseInfo->saveType == CaptureSaveType::CaptureJpeg)
		{
			m_value *= shift_m_value;
			a_value *= shift_m_value;
		}
		break;
	}
	}

	while (true)
	{
		if (!m_callback_image_queue.empty())
		{
			CameraSystemFrameInfo buffer_info = m_callback_image_queue.front();
			m_callback_image_queue.pop();

			int buffer_img_hdl = buffer_info.p2d_img_hdl;
			if (buffer_img_hdl < 0)
			{
				m_processed_image_queue.push({ -1, 0 ,0 });
				break;
			}
			if (!m_isCapturing)
			{
				m_callback_transfer_p2d_handle_queue.push(buffer_img_hdl);
				break;
			}

#ifdef TEST_SPEED
			//timer_process.Start();
#endif
			//process image here.
			int averageJudgeNumber = buffer_info.frame_num % m_pCaptureInfo->baseInfo->averageFrames;

			unsigned int current_slot_index = (buffer_info.frame_num - 1) / m_pCaptureInfo->baseInfo->averageFrames;
			current_slot_index += m_pCaptureInfo->baseInfo->currentSlotIndex;
			if (current_slot_index >= m_pCaptureInfo->baseInfo->captureSlotSettingsCount)
				current_slot_index -= m_pCaptureInfo->baseInfo->captureSlotSettingsCount;

			int callback_img_hdl = -111;
			switch (m_cameraType)
			{
			case CameraType::CamMono:
			{
				//original pure not min max image data for preview
				if (m_pCaptureInfo->baseInfo->isEnableCaptureImageUpdate)
				{
					callback_img_hdl = p2d_create_image();
					p2d_init_image(callback_img_hdl, info->x_size, info->y_size, info->pix_type, info->channels, info->valid_bits);
					p2d_copy(buffer_img_hdl, callback_img_hdl);
				}

				//won't be video or jpeg type
				if (m_pCaptureInfo->baseInfo->saveType == CaptureSaveType::CaptureVideo || m_pCaptureInfo->baseInfo->saveType == CaptureSaveType::CaptureJpeg)
				{
					break;
					//image with min max for save
					//int u8c1_img_hdl = p2d_create_image();
					//p2d_init_image(u8c1_img_hdl, info->x_size, info->y_size, p2d_data_format::P2D_8U, p2d_channels::P2D_CHANNELS_1, 8);
					//p2d_scale(processed_img_hdl, u8c1_img_hdl, m_value, a_value);
					//p2d_destroy_image(processed_img_hdl);
					//processed_img_hdl = u8c1_img_hdl;
				}
				else
				{
					if (m_pCaptureInfo->baseInfo->averageFrames > 1)
					{
						//WriteLog(LogLevel::Info, "Add frame.");
						p2d_addI(buffer_img_hdl, m_average_temp_handle);
						//auto ptr = (unsigned short*)(process_info.data_buf);
						//auto value = *(ptr + 1000 * 1000);
						//std::string msg = "Value is : " + std::to_string(value) + ".";
						//WriteLog(LogLevel::Info, msg.c_str());
						if (averageJudgeNumber == 0)
						{
							//WriteLog(LogLevel::Info, "Average.");
							double m_value = 1.0 / m_pCaptureInfo->baseInfo->averageFrames;
							int processed_img_hdl = p2d_create_image();
							do
							{
								if (processed_img_hdl < 0)
								{
									m_CaptureErrorCode = ERR_CAMSYS_CREATE_P2D_FAILED;
									break;
								}
								auto p2d_status = p2d_init_image(processed_img_hdl, info->x_size, info->y_size, info->pix_type, info->channels, info->valid_bits);
								if (p2d_status != 0)
								{
									m_CaptureErrorCode = ERR_CAMSYS_INIT_P2D_FAILED;
									p2d_destroy_image(processed_img_hdl);
									break;
								}
								p2d_status = p2d_scale(m_average_temp_handle, processed_img_hdl, m_value, 0);
								if (p2d_status != 0)
								{
									m_CaptureErrorCode = ERR_CAMSYS_P2D_OPERATE_FAILED;
									p2d_destroy_image(processed_img_hdl);
									break;
								}
								p2d_clear(m_average_temp_handle);
							} while (0);
							//value = *(ptr + 1000 * 1000);
							//msg = "Scale value is : " + std::to_string(value) + ".";
							//WriteLog(LogLevel::Info, msg.c_str());

							if (0 != m_CaptureErrorCode)
								m_processed_image_queue.push({ -1, 0 ,0 });
							else
								m_processed_image_queue.push({ processed_img_hdl, current_slot_index ,buffer_info.frame_clock });
						}
					}
					else
					{
						int processed_img_hdl = p2d_create_image();
						do
						{
							if (processed_img_hdl < 0)
							{
								m_CaptureErrorCode = ERR_CAMSYS_CREATE_P2D_FAILED;
								break;
							}
							auto p2d_status = p2d_init_image(processed_img_hdl, info->x_size, info->y_size, info->pix_type, info->channels, info->valid_bits);
							if (p2d_status != 0)
							{
								m_CaptureErrorCode = ERR_CAMSYS_INIT_P2D_FAILED;
								p2d_destroy_image(processed_img_hdl);
								break;
							}
							p2d_status = p2d_scale(buffer_img_hdl, processed_img_hdl, m_value, a_value);
							if (p2d_status != 0)
							{
								m_CaptureErrorCode = ERR_CAMSYS_P2D_OPERATE_FAILED;
								p2d_destroy_image(processed_img_hdl);
								break;
							}
						} while (0);

						if (0 != m_CaptureErrorCode)
							m_processed_image_queue.push({ -1, 0 ,0 });
						else
							m_processed_image_queue.push({ processed_img_hdl, current_slot_index ,buffer_info.frame_clock });
					}
					m_callback_transfer_p2d_handle_queue.push(buffer_img_hdl);					
				}
				break;
			}
			//case CameraType::CamColorBayer:
			//{
			//	p2d_info tempInfo;
			//	p2d_get_info(processed_img_hdl, &tempInfo);

			//	p2d_copy(processed_img_hdl, m_bayer_temp_16u1c_handle);

			//	p2d_info color_temp_16u1c_info;
			//	p2d_get_info(m_bayer_temp_16u1c_handle, &color_temp_16u1c_info);

			//	s_currentCamera->m_pCamera->BayerTransform((unsigned short*)color_temp_16u1c_info.data_buf, m_bayer_temp_16u3c_buffer);

			//	p2d_info color_temp_8u3c_info;
			//	p2d_get_info(m_bayer_temp_8u3c_handle, &color_temp_8u3c_info);

			//	s_currentCamera->m_pCamera->ColorTransform48to24(m_bayer_temp_16u3c_buffer, (unsigned char*)color_temp_8u3c_info.data_buf);

			//	int u8c3_img_hdl = p2d_create_image();
			//	p2d_init_image(u8c3_img_hdl, info->x_size, info->y_size, p2d_data_format::P2D_8U, p2d_channels::P2D_CHANNELS_3, 8);

			//	//original pure not min max image data
			//	if (m_pCaptureInfo->baseInfo->isEnableCaptureImageUpdate)
			//	{
			//		callback_img_hdl = p2d_create_image();
			//		p2d_init_image(callback_img_hdl, info->x_size, info->y_size, p2d_data_format::P2D_8U, p2d_channels::P2D_CHANNELS_3, 8);
			//		p2d_copy(m_bayer_temp_8u3c_handle, callback_img_hdl);
			//	}


			//	//image with min max for save
			//	p2d_adjust(m_bayer_temp_8u3c_handle, u8c3_img_hdl, pmin, pmax);

			//	p2d_destroy_image(processed_img_hdl);
			//	processed_img_hdl = u8c3_img_hdl;
			//	break;
			//}
			//case CameraType::CamPolar:
			//{
			//	p2d_copy(processed_img_hdl, m_polar_temp_16u1c_handle);

			//	p2d_info polar_temp_16u1c_info;
			//	p2d_get_info(m_polar_temp_16u1c_handle, &polar_temp_16u1c_info);

			//	p2d_info processed_polar_data_info;
			//	p2d_get_info(m_polar_temp_processed_16u1c_handle, &processed_polar_data_info);

			//	//Polar transform
			//	s_currentCamera->m_pCamera->PolarTransform((unsigned short*)polar_temp_16u1c_info.data_buf,
			//		current_polar_image_type_selection, (unsigned short*)processed_polar_data_info.data_buf);

			//	if (m_pCaptureInfo->baseInfo->isEnableCaptureImageUpdate)
			//	{
			//		callback_img_hdl = p2d_create_image();
			//		p2d_init_image(callback_img_hdl, info->x_size, info->y_size, p2d_data_format::P2D_16U, p2d_channels::P2D_CHANNELS_1, (int)info->valid_bits);
			//		p2d_copy(m_polar_temp_processed_16u1c_handle, callback_img_hdl);
			//	}

			//	if (m_pCaptureInfo->baseInfo->saveType == CaptureSaveType::CaptureVideo || m_pCaptureInfo->baseInfo->saveType == CaptureSaveType::CaptureJpeg)
			//	{
			//		//image with min max for save
			//		int u8c1_img_hdl = p2d_create_image();
			//		p2d_init_image(u8c1_img_hdl, info->x_size, info->y_size, p2d_data_format::P2D_8U, p2d_channels::P2D_CHANNELS_1, 8);
			//		p2d_scale(m_polar_temp_processed_16u1c_handle, u8c1_img_hdl, m_value, a_value);

			//		p2d_destroy_image(processed_img_hdl);
			//		processed_img_hdl = u8c1_img_hdl;
			//	}
			//	else
			//	{
			//		int u16c1_img_hdl = p2d_create_image();
			//		p2d_init_image(u16c1_img_hdl, info->x_size, info->y_size, p2d_data_format::P2D_16U, p2d_channels::P2D_CHANNELS_1, (int)info->valid_bits);
			//		p2d_copy(m_polar_temp_processed_16u1c_handle, u16c1_img_hdl);

			//		p2d_destroy_image(processed_img_hdl);
			//		processed_img_hdl = u16c1_img_hdl;
			//		p2d_scaleI(processed_img_hdl, m_value, a_value);
			//	}

			//	break;
			//}
			default:
				break;
			}

#ifdef TEST_SPEED
			/*timer_process.Stop();
			double elapsed = timer_process.Elapsed_ms();
			std::string str_process = "Processing frame " + std::to_string(buffer_info.frame_num) + " elapsed time : " + std::to_string(elapsed);
			WriteLog(LogLevel::Info, str_process.c_str());*/
			/*		std::string str_process = "Processing frame " + std::to_string(buffer_info.frame_num) ;
					WriteLog(LogLevel::Info, str_process.c_str());*/
#endif

			if (m_isCapturing && m_imageCallback != nullptr)
			{
				//std::string msg = "Slot index in process : " + std::to_string(slotIndex) + "; Frame count : " + std::to_string(m_callbackFrame) + ".";
				//WriteLog(LogLevel::Info, msg.c_str());
				m_imageCallback(callback_img_hdl, buffer_info.frame_num, buffer_info.frame_clock, current_slot_index, averageJudgeNumber == 0);
				p2d_destroy_image(callback_img_hdl);
			}

			if (0 != m_CaptureErrorCode)
				break;

			++m_processedFrameCount;
			if (m_processedFrameCount >= frameCount)
				break;
		}
		else
		{
			std::this_thread::sleep_for(std::chrono::milliseconds(1));
			if (!m_isCapturing)
				break;
		}
	}
}

void CameraSystem::ProcessLiveImages()
{
	auto info = s_currentCamera->m_backup_p2d_img_info;
	while (true)
	{
		if (!m_callback_image_queue.empty())
		{
			auto size = m_callback_image_queue.size();
			while (size > 1)
			{
				size--;
				CameraSystemFrameInfo pop_info = m_callback_image_queue.front();
				m_callback_image_queue.pop();
				m_callback_transfer_p2d_handle_queue.push(pop_info.p2d_img_hdl);
			}
			CameraSystemFrameInfo buffer_info = m_callback_image_queue.front();
			m_callback_image_queue.pop();

			int buffer_img_hdl = buffer_info.p2d_img_hdl;
			if (buffer_img_hdl < 0)
			{
				continue;
			}
			if (!m_isLiving)
			{
				m_callback_transfer_p2d_handle_queue.push(buffer_img_hdl);
				break;
			}

#ifdef TEST_SPEED
			//timer_process.Start();
#endif
			//process image here.
			switch (m_cameraType)
			{
			case CameraType::CamMono:
			{
				if (m_isAutoExposuring && m_hdl_AutoExposuring >= 0 && !m_isAutoImageReady)
				{
					p2d_copy(buffer_img_hdl, m_hdl_AutoExposuring);
					m_isAutoImageReady = true;
				}
				if (m_isLiving && m_imageCallback != nullptr)
				{
					m_imageCallback(buffer_img_hdl, buffer_info.frame_num, buffer_info.frame_clock, -1, false);
					m_callback_transfer_p2d_handle_queue.push(buffer_img_hdl);
				}
				break;
			}
			/*
			case CameraType::CamColorBayer:
			{
				p2d_info tempInfo;
				p2d_get_info(processed_img_hdl, &tempInfo);

				p2d_copy(processed_img_hdl, m_bayer_temp_16u1c_handle);

				p2d_info color_temp_16u1c_info;
				p2d_get_info(m_bayer_temp_16u1c_handle, &color_temp_16u1c_info);

				s_currentCamera->m_pCamera->BayerTransform((unsigned short*)color_temp_16u1c_info.data_buf, m_bayer_temp_16u3c_buffer);

				p2d_info color_temp_8u3c_info;
				p2d_get_info(m_bayer_temp_8u3c_handle, &color_temp_8u3c_info);

				s_currentCamera->m_pCamera->ColorTransform48to24(m_bayer_temp_16u3c_buffer, (unsigned char*)color_temp_8u3c_info.data_buf);

				int u8c3_img_hdl = p2d_create_image();
				p2d_init_image(u8c3_img_hdl, info->x_size, info->y_size, p2d_data_format::P2D_8U, p2d_channels::P2D_CHANNELS_3, 8);

				if (m_isAutoExposuring && m_hdl_AutoExposuring >= 0 && !m_isAutoImageReady)
				{
					p2d_color_to_gray(m_bayer_temp_8u3c_handle, m_hdl_AutoExposuring);
					m_isAutoImageReady = true;
				}
				p2d_copy(m_bayer_temp_8u3c_handle, u8c3_img_hdl);

				p2d_destroy_image(processed_img_hdl);
				processed_img_hdl = u8c3_img_hdl;
				break;
			}
			case CameraType::CamPolar:
				p2d_copy(processed_img_hdl, m_polar_temp_16u1c_handle);

				p2d_info polar_temp_16u1c_info;
				p2d_get_info(m_polar_temp_16u1c_handle, &polar_temp_16u1c_info);

				p2d_info processed_polar_data_info;
				p2d_get_info(m_polar_temp_processed_16u1c_handle, &processed_polar_data_info);

				s_currentCamera->m_pCamera->PolarTransform((unsigned short*)polar_temp_16u1c_info.data_buf,
					current_polar_image_type_selection, (unsigned short*)processed_polar_data_info.data_buf);

				int u16c1_img_hdl = p2d_create_image();
				p2d_init_image(u16c1_img_hdl, info->x_size, info->y_size, p2d_data_format::P2D_16U, p2d_channels::P2D_CHANNELS_1, (int)info->valid_bits);

				if (m_isAutoExposuring && m_hdl_AutoExposuring >= 0 && !m_isAutoImageReady)
				{
					p2d_copy(m_polar_temp_processed_16u1c_handle, m_hdl_AutoExposuring);
					m_isAutoImageReady = true;
				}
				p2d_copy(m_polar_temp_processed_16u1c_handle, u16c1_img_hdl);

				p2d_destroy_image(processed_img_hdl);
				processed_img_hdl = u16c1_img_hdl;
				break;
				*/
			default:
				break;
			}

#ifdef TEST_SPEED
			/*timer_process.Stop();
			double elapsed = timer_process.Elapsed_ms();
			std::string str_process = "Processing frame " + std::to_string(buffer_info.frame_num) + " elapsed time : " + std::to_string(elapsed);
			WriteLog(LogLevel::Info, str_process.c_str());*/
			/*		std::string str_process = "Processing frame " + std::to_string(buffer_info.frame_num) ;
					WriteLog(LogLevel::Info, str_process.c_str());*/
#endif

		}
		else
		{
			std::this_thread::sleep_for(std::chrono::milliseconds(1));
			if (!m_isLiving)
				break;
		}
	}
}

//int CameraSystem::ProcessOriginalDataCamColorBayer(int* hdl, p2d_info* info)
//{
//	int processed_img_hdl = *hdl;
//	CHECK_STATUS(p2d_copy(processed_img_hdl, m_bayer_temp_16u1c_handle));
//	CHECK_STATUS(p2d_destroy_image(processed_img_hdl));
//	p2d_info color_temp_16u1c_info;
//	CHECK_STATUS(p2d_get_info(m_bayer_temp_16u1c_handle, &color_temp_16u1c_info));
//
//	//bayer transform
//	CHECK_STATUS(s_currentCamera->m_pCamera->BayerTransform((unsigned short*)color_temp_16u1c_info.data_buf, m_bayer_temp_16u3c_buffer));
//	void* color_temp_8u3c_buf;
//	color_temp_8u3c_buf = p2d_get_buf(m_bayer_temp_8u3c_handle);
//	CHECK_STATUS(s_currentCamera->m_pCamera->ColorTransform48to24(m_bayer_temp_16u3c_buffer, (unsigned char*)color_temp_8u3c_buf));
//
//	int u8c3_img_hdl = p2d_create_image();
//	CHECK_STATUS(p2d_init_image(u8c3_img_hdl, info->x_size, info->y_size, p2d_data_format::P2D_8U, p2d_channels::P2D_CHANNELS_3, 8));
//	CHECK_STATUS(p2d_copy(m_bayer_temp_8u3c_handle, u8c3_img_hdl));
//
//	*hdl = u8c3_img_hdl;
//	return STATUS_OK;
//}
//
//int CameraSystem::ProcessOriginalDataCamPolar(int* hdl, p2d_info* info)
//{
//	int processed_img_hdl = *hdl;
//	CHECK_STATUS(p2d_copy(processed_img_hdl, m_polar_temp_16u1c_handle));
//	CHECK_STATUS(p2d_destroy_image(processed_img_hdl));
//	p2d_info polar_temp_16u1c_info;
//	CHECK_STATUS(p2d_get_info(m_polar_temp_16u1c_handle, &polar_temp_16u1c_info));
//
//	p2d_info processed_polar_data_info;
//	CHECK_STATUS(p2d_get_info(m_polar_temp_processed_16u1c_handle, &processed_polar_data_info));
//
//	//polar tranform
//	s_currentCamera->m_pCamera->PolarTransform((unsigned short*)polar_temp_16u1c_info.data_buf,
//		current_polar_image_type_selection, (unsigned short*)processed_polar_data_info.data_buf);
//
//	int u16c1_img_hdl = p2d_create_image();
//	CHECK_STATUS(p2d_init_image(u16c1_img_hdl, info->x_size, info->y_size, p2d_data_format::P2D_16U, p2d_channels::P2D_CHANNELS_1, (int)info->valid_bits));
//
//	CHECK_STATUS(p2d_copy(m_polar_temp_processed_16u1c_handle, u16c1_img_hdl));
//
//	*hdl = u16c1_img_hdl;
//	return STATUS_OK;
//}


void CameraSystem::ProcessSnapshotImage()
{
	auto info = s_currentCamera->m_backup_p2d_img_info;

	unsigned int frameCount = m_pSnapshotInfo->averageFrames;

	double* pmin = m_pSnapshotInfo->min;
	double* pmax = m_pSnapshotInfo->max;
	double m_value = 1.0;
	double a_value = 0;

	auto max_value = (1 << info->valid_bits) - 1;
	double shift_m_value = std::pow(2, 8 - (int)info->valid_bits);

	switch (m_cameraType)
	{
	case CameraType::CamMono:
	{
		//auto expand_max = ((double)max_value / 255.0) * pmax[0];
		//auto expand_min = ((double)max_value / 255.0) * pmax[0];
		m_value = max_value / (pmax[0] - pmin[0]); //pmax[0] - pmin[0] < 3 ? 1.0 : 255.0 / (pmax[0] - pmin[0]);
		a_value = 0 - (pmin[0] * m_value); //pmax[0] - pmin[0] < 3 ? 0 : (0 - (pmin[0] * m_value));
		break;
	}
	default:
		break;
	}

	while (true)
	{
		if (!m_callback_image_queue.empty())
		{
			CameraSystemFrameInfo buffer_info = m_callback_image_queue.front();
			m_callback_image_queue.pop();
			if (buffer_info.p2d_img_hdl < 0) 
				break;

			int processed_img_hdl = buffer_info.p2d_img_hdl;
			int averageJudgeNumber = m_callbackFrame % m_pSnapshotInfo->averageFrames;
			//WriteLog(LogLevel::Info, std::to_string(m_callbackFrame).c_str());

			long status = STATUS_OK;

			switch (m_cameraType)
			{
			case CameraType::CamMono:
			{
				p2d_scaleI(processed_img_hdl, m_value, a_value);
				bool canDoSave = false;
				if (m_pSnapshotInfo->averageFrames > 1)
				{
					p2d_addI(processed_img_hdl, m_average_temp_handle);
					if (averageJudgeNumber == 0)
					{
						double m_value = 1.0 / m_pSnapshotInfo->averageFrames;
						p2d_scale(m_average_temp_handle, processed_img_hdl, m_value, 0);
						p2d_clear(m_average_temp_handle);
						canDoSave = true;
					}
				}
				else
				{
					canDoSave = true;
				}
				if (!m_isSnapshoting || !canDoSave)
					break;

				int tiff_handle = open_tiff(m_pSnapshotInfo->fileName, OpenMode::CREATE_MODE);
				if (tiff_handle < 0)
				{
					status = tiff_handle;
					break;
				}

				p2d_info current_frame_info;
				int status = p2d_get_info(processed_img_hdl, &current_frame_info);

				ImageInfo i_info{};
				i_info.width = current_frame_info.x_size;
				i_info.height = current_frame_info.y_size;
				i_info.line_bytes = ((current_frame_info.valid_bits + 7) / 8) * current_frame_info.x_size * (int)current_frame_info.channels;
				i_info.pixel_type = (int)current_frame_info.pix_type;
				i_info.valid_bits = (unsigned short)current_frame_info.valid_bits;
				i_info.image_type = (int)(current_frame_info.channels == p2d_channels::P2D_CHANNELS_3 ? ImageType::IMAGE_RGB : ImageType::IMAGE_GRAY);
				i_info.compression_mode = (int)CompressionMode::COMPRESSIONMODE_NONE;
				i_info.slot_index = m_pSnapshotInfo->slotIndex;

				status = SaveImage(tiff_handle, processed_img_hdl, &i_info);
				close_tiff(tiff_handle);
			}
				break;
			default:
				break;
			//case CameraType::CamColorBayer:
			//{
			//	if (m_isSnapshoting)
			//		auto ret = ProcessOriginalDataCamColorBayer(&processed_img_hdl, info);
			//	break;
			//}
			//case CameraType::CamPolar:
			//{
			//	if (m_isSnapshoting)
			//		auto ret = ProcessOriginalDataCamPolar(&processed_img_hdl, info);
			//	break;
			//}
			}
			if (m_imageCallback != nullptr && m_isSnapshoting)
			{
				m_imageCallback(processed_img_hdl, buffer_info.frame_num, buffer_info.frame_clock, m_pSnapshotInfo->slotIndex, true);
			}
			m_callback_transfer_p2d_handle_queue.push(processed_img_hdl);

			if (!m_isSnapshoting || m_callbackFrame >= frameCount)
				break;
			//m_isSnapshoting = false;
			//break;
		}
		else
		{
			std::this_thread::sleep_for(std::chrono::milliseconds(1));
			if (!m_isSnapshoting) 
				break;
		}
	}
}

#pragma region Image Saving and Loading
int CameraSystem::GetImageCount(const wchar_t* file_name, int* tiff_handle, unsigned int* image_count)
{
	long status = LoadImageCount(file_name, tiff_handle, image_count);
	return status;
}

int CameraSystem::GetImageBuffer(int tiff_handle, unsigned int frame_number, TiffImageSimpleInfo* simple_info)
{
	ImageInfo info;
	long status = LoadImageInfo(tiff_handle, frame_number, &info);
	if (status != 0)
		return status;

	status = get_image_tag(tiff_handle, frame_number, VALIDBITS_TAG, sizeof(uint32_t), &simple_info->valid_bits);
	if (status != STATUS_OK || simple_info->valid_bits < 8)
	{
		simple_info->valid_bits = info.valid_bits;
	}

	status = get_image_tag(tiff_handle, frame_number, SLOTINDEX_TAG, sizeof(uint32_t), &simple_info->slot_index);
	if (status != STATUS_OK)
	{
		simple_info->slot_index = 0;
	}

	p2d_channels channels = info.image_type == 1 ? p2d_channels::P2D_CHANNELS_3 : p2d_channels::P2D_CHANNELS_1;

	int p2d_hdl = p2d_create_image();
	if (p2d_hdl < 0)
		return p2d_hdl;
	status = p2d_init_image(p2d_hdl, info.width, info.height, (p2d_data_format)info.pixel_type, channels, simple_info->valid_bits);
	if (status != STATUS_OK)
	{
		p2d_destroy_image(p2d_hdl);
		return status;
	}
	p2d_info p2d_img_info;
	status = p2d_get_info(p2d_hdl, &p2d_img_info);
	if (status != STATUS_OK)
	{
		p2d_destroy_image(p2d_hdl);
		return status;
	}

	status = load_image_data(tiff_handle, frame_number, p2d_img_info.data_buf, p2d_img_info.line_bytes);
	if (status != STATUS_OK)
	{
		p2d_destroy_image(p2d_hdl);
		return status;
	}
	simple_info->p2d_img_hdl = p2d_hdl;
	return STATUS_OK;
}

int CameraSystem::CloseImage(int tiff_handle)
{
	if (tiff_handle >= 0)
		close_tiff(tiff_handle);
	return STATUS_OK;
}

int CameraSystem::GetJpegBuffer(const wchar_t* file_name, int* p2d_img_hdl)
{
	FILE* jpeg_file = _wfsopen(file_name, L"rb", _SH_DENYWR);
	if (jpeg_file == nullptr)
		return ERR_CREATE_JPEG_FILE_FALIED;

	_fseeki64(jpeg_file, 0, SEEK_END);
	long total_size = ftell(jpeg_file);
	_fseeki64(jpeg_file, 0, SEEK_SET);
	void* buffer = calloc(total_size, 1);
	if (buffer == nullptr)
		return ERR_CAMSYS_MALLOCBUFFER;

	fread(buffer, 1, total_size, jpeg_file);
	fclose(jpeg_file);

	int ret = -1;
	tjhandle handle = tjInitDecompress();
	if (handle == nullptr)
	{
		free(buffer);
		return ERR_INIT_TURBOJPEG_DECOMPRESS_FAILED;
	}

	int width, height, subsample, colorspace;
	if (tjDecompressHeader3(handle, (unsigned char*)buffer, total_size, &width, &height, &subsample, &colorspace) == 0)
	{
		int pixelFormat = TJPF_RGB;	//only support RBG, other format not support
		p2d_channels channels = p2d_channels::P2D_CHANNELS_3;
		if (subsample == TJSAMP_GRAY && colorspace == TJCS_GRAY)
		{
			pixelFormat = TJPF_GRAY;
			channels = p2d_channels::P2D_CHANNELS_1;
		}
		*p2d_img_hdl = p2d_create_image();
		p2d_init_image(*p2d_img_hdl, width, height, p2d_data_format::P2D_8U, channels, 8);
		p2d_info p2d_img_info;
		p2d_get_info(*p2d_img_hdl, &p2d_img_info);

		ret = tjDecompress2(handle, (unsigned char*)buffer, total_size, (unsigned char*)p2d_img_info.data_buf, width, p2d_img_info.line_bytes, height, pixelFormat, TJFLAG_ACCURATEDCT);
	}
	tjDestroy(handle);
	free(buffer);

	return ret;
}

int CameraSystem::SetCurrentPolarizationImageType(int polar_image_type)
{
	if (polar_image_type > (int)PolarImageType::QUAD_VIEW)
	{
		return 1;
	}
	current_polar_image_type_selection = (PolarImageType)polar_image_type;
	return STATUS_OK;
}

int CameraSystem::GetCurrentPolarizationImageType(int* polar_image_type)
{
	*polar_image_type = (int)current_polar_image_type_selection;
	return STATUS_OK;
}

long CameraSystem::LoadImageInfo(int tiff_handle, unsigned int frame_number, ImageInfo* image_info)
{
	if (tiff_handle < 0)
		return -10002;
	//char* manu = (char*)malloc(255);
	//if (manu == nullptr)
	//	return ERR_CAMSYS_MALLOCBUFFER;
	long status;
	//status = get_image_tag(tiff_handle, frame_number, MANUFACTURE_TAG, 255, (void*)manu);
	//if (status != STATUS_OK)
	//{
	//	return status;
	//}
	//for (int i = 0; i < strlen(MICROSNAP_MANU); i++)
	//{
	//	if (toupper(manu[i]) != MICROSNAP_MANU[i])
	//		return ERR_CAMSYS_MANUFACTURE;
	//}

	SingleImageInfo info;
	status = get_image_info(tiff_handle, frame_number, &info);
	if (status != STATUS_OK)
	{
		return status;
	}

	image_info->width = info.width;
	image_info->height = info.height;
	image_info->valid_bits = info.valid_bits;
	image_info->pixel_type = (int)info.pixel_type;
	image_info->image_type = (int)info.image_type;
	image_info->compression_mode = (int)info.compress_mode;

	const int bytes[] = { 1, 1, 2, 2, 4 };
	image_info->line_bytes = image_info->width * bytes[image_info->pixel_type] * (image_info->image_type == 1 ? 3 : 1);

	return STATUS_OK;
}

int CameraSystem::SaveImage(const wchar_t* file_name, int p2d_img_hdl, int slotIndex)
{
	int tiff_handle = open_tiff(file_name, OpenMode::CREATE_MODE);
	if (tiff_handle < 0)
		return ERR_TIFF_HANDLE_NOT_VALID;
	p2d_info p2d_img_info;
	int status = p2d_get_info(p2d_img_hdl, &p2d_img_info);
	if (status != 0)
		return ERR_CAMSYS_P2D_GETINFO_FAILED;

	ImageInfo i_info{};
	i_info.width = p2d_img_info.x_size;
	i_info.height = p2d_img_info.y_size;
	i_info.line_bytes = ((p2d_img_info.valid_bits + 7) / 8) * p2d_img_info.x_size * (int)p2d_img_info.channels;
	i_info.pixel_type = (int)p2d_img_info.pix_type;
	i_info.valid_bits = (unsigned short)p2d_img_info.valid_bits;
	i_info.image_type = (int)(p2d_img_info.channels == p2d_channels::P2D_CHANNELS_3 ? ImageType::IMAGE_RGB : ImageType::IMAGE_GRAY);
	i_info.compression_mode = (int)CompressionMode::COMPRESSIONMODE_NONE;
	i_info.slot_index = slotIndex;

	status = SaveImage(tiff_handle, p2d_img_hdl, &i_info);
	CloseImage(tiff_handle);
	return status;
}

long CameraSystem::SaveImage(int tiff_handle, int p2d_img_hdl, ImageInfo* image_info)
{
	if (tiff_handle < 0)
		return ERR_TIFF_HANDLE_NOT_VALID;

	p2d_info tiff_image_info;
	long status = p2d_get_info(p2d_img_hdl, &tiff_image_info);
	if (status != STATUS_OK)
		return ERR_CAMSYS_P2D_GETINFO_FAILED;

	unsigned short bytes = (image_info->valid_bits + 7) / 8;
	SingleImageInfo info =
	{
		image_info->width,
		image_info->height,
		(unsigned short)(bytes * 8),
		(PixelType)image_info->pixel_type,
		(ImageType)image_info->image_type,
		(CompressionMode)image_info->compression_mode,
	};

	long frame = create_image(tiff_handle, info);
	if (frame < 0)
		return frame;

	status = save_image_data(tiff_handle, frame, tiff_image_info.data_buf, tiff_image_info.line_bytes);
	if (status != STATUS_OK)
		return status;

	const char* data = THORIMAGECAM_MANU;
	status = set_image_tag(tiff_handle, frame, MANUFACTURE_TAG, TiffTagDataType::TIFF_BYTE, (unsigned int)strlen(data), (void*)data);
	if (status != STATUS_OK)
		return ERR_CAMSYS_P2D_SETTAG_FAILED;

	status = set_image_tag(tiff_handle, frame, VALIDBITS_TAG, TiffTagDataType::TIFF_LONG, 1, &image_info->valid_bits);
	if (status != STATUS_OK)
		return ERR_CAMSYS_P2D_SETTAG_FAILED;

	if (image_info->slot_index >= 0)
	{
		status = set_image_tag(tiff_handle, frame, SLOTINDEX_TAG, TiffTagDataType::TIFF_LONG, 1, &image_info->slot_index);
		if (status != STATUS_OK)
			return ERR_CAMSYS_P2D_SETTAG_FAILED;
	}

	return status;
}

long CameraSystem::LoadImageCount(const wchar_t* file_name, int* tiff_handle, unsigned int* image_count)
{
	int handle = open_tiff(file_name, OpenMode::READ_ONLY_MODE);
	if (handle < 0)
	{
		return handle;
	}

	unsigned int count;
	long status = get_image_count(handle, &count);
	if (status != STATUS_OK)
	{
		return status;
	}
	if (count < 1)
		return -10001;

	*tiff_handle = handle;
	*image_count = count;

	return STATUS_OK;
}

#pragma endregion

//#pragma region Video Saving and Loading
//
//int CameraSystem::VideoSaving_Start(uint32_t width, uint32_t height, p2d_channels colorType, uint32_t fps, const wchar_t* path)
//{
//	vpl_info info{};
//	info.bit_rate = 4 * width * height;
//	switch (colorType)
//	{
//	case p2d_channels::P2D_CHANNELS_1:
//		info.color = vpl_color_format::VPL_L8;
//		break;
//	case p2d_channels::P2D_CHANNELS_3:
//		info.color = vpl_color_format::VPL_RGB24;
//		break;
//	}
//	info.fps = fps;
//	info.width = width;
//	info.height = height;
//
//	m_video_frame_duration = 10 * 1000 * 1000 / info.fps;
//	vpl_enc_init(info, path);
//	return STATUS_OK;
//}
//
//int CameraSystem::VideoSaving_SingleFrame(int hdl_img, uint64_t video_frame_duration)
//{
//	p2d_info hdl_info;
//	auto result = p2d_get_info(hdl_img, &hdl_info);
//	if (result == 0)
//	{
//		frameData.pdata1 = (uint8_t*)hdl_info.data_buf;
//		frameData.d1stride = hdl_info.line_bytes;
//		frameData.rtStart += video_frame_duration;
//
//		result = vpl_enc_frame(frameData);
//	}
//	return result;
//}
//
//int CameraSystem::VideoSaving_Stop()
//{
//	frameData.rtStart = 0;
//	auto result = vpl_enc_dispose();
//	return result;
//}
//
//#pragma endregion
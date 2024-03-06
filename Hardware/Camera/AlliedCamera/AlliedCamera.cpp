#include "AlliedCamera.h"
#include <vector>
#include <algorithm>
#include <cmath>

#define NUM_FRAMES 10
#define BUFFER_ALIGNMENT 2

bool AlliedCamera::_isInitialized = false;
char* AlliedCamera::_sn = nullptr;
VmbSystem& AlliedCamera::_system = VmbSystem::GetInstance();

#define CHECKVALID(handle) { if (!handle) return ERROR_; }
#define CHECKERROR(error) {if(error != VmbErrorSuccess) return error;}

void SplitString(const std::string& s, std::vector<std::string>& v, const std::string& c)
{
	std::string::size_type pos1, pos2;
	pos2 = s.find(c);
	pos1 = 0;
	while (std::string::npos != pos2)
	{
		v.push_back(s.substr(pos1, pos2 - pos1));

		pos1 = pos2 + c.size();
		pos2 = s.find(c, pos1);
	}
	if (pos1 != s.length())
		v.push_back(s.substr(pos1));
}

AlliedCamera::~AlliedCamera()
{
	Close();
	//we don't need release SDK
	//disconnect camera will also delete camera, but no need to release SDK
	//ReleaseSDK();
}

//AlliedCamera listed as "sn1,model1;sn2,model2;sn3,;"
//if one camera is opened, the model is empty.
int AlliedCamera::ListCamera(char* camera_ids, int size)
{
	//Must guarantee SDK is initialized
	if (!_isInitialized) return ERROR_;

	VmbErrorType error;
	CameraPtrVector cameras;
	error = _system.GetCameras(cameras);
	CHECKERROR(error);

	std::string final_str;
	for (auto& camera : cameras)
	{
		std::string id;
		std::string model_name;

		error = camera->GetID(id);
		if (error != VmbErrorSuccess) continue;

		if (_sn != nullptr && id.compare(_sn) == 0)
		{
			final_str.append(id);
			final_str.append(",");
		}
		else
		{
			error = camera->GetModel(model_name);
			if (error != VmbErrorSuccess) continue;

			final_str.append(id);
			final_str.append(",");
			final_str.append(model_name);
		}
		final_str.append(";");
	}

	auto length = final_str.length();
	if ((length + 1) > size) return -4;

	memset(camera_ids, 0, size);
	memcpy(camera_ids, final_str.c_str(), length);
	return SUCCESS_;
}

int AlliedCamera::Open(const char* sn)
{
	if (!_isInitialized) return ERROR_;

	VmbErrorType error = _system.GetCameraByID(sn, _camera);
	CHECKERROR(error);
	error = _camera->Open(VmbAccessModeFull);
	CHECKERROR(error);

	size_t len = strlen(sn);
	_sn = (char*)calloc(len + 1, 1);
	if (_sn == nullptr) return -3;
	memcpy(_sn, sn, len);

	FeaturePtr pixel_type;
	error = _camera->GetFeatureByName("PixelFormat", pixel_type);
	CHECKERROR(error);

	std::vector<std::string> pixelFormatValues;
	VmbErrorType err = pixel_type->GetValues(pixelFormatValues);

	auto length = pixelFormatValues.size();
	if (length > 0)
	{
		std::string fp = "";
		for (int i = 0; i < length; i++)
		{
			auto index = pixelFormatValues[i].find("Mono");
			if (index >= 0)
			{
				if (pixelFormatValues[i].find("Mono8") == 0)
				{
					fp = "Mono8";
				}
				if (pixelFormatValues[i].find("Mono10") == 0)
				{
					fp = "Mono10";
				}
				if (pixelFormatValues[i].find("Mono12") == 0)
				{
					fp = "Mono12";
				}
			}
		}
		//no mono8/mono10/mono12
		if (fp.empty())
			return ERROR_;

		error = pixel_type->SetValue(fp.c_str());
	}
	else
	{
		return ERROR_;
	}

	_isColorCamera = false;

	//Not sure exist feature polarized.
	_is_polar_camera = false;

	if (GetBitDepth(&_pixel_bit_depth) != 0)
		return ERROR_;

	// Get serial hub status
	bool _is_serial_enabled;
	if (GetSerialHubEnabled(&_is_serial_enabled) != SUCCESS_)
		return ERROR_;

	if (_is_serial_enabled == 0)
	{
		// enable Serial Hub
		if (SetSerialHubEnabled(1) != SUCCESS_)
			return ERROR_;
	}

	// set default baud rate
	if (SetSerialBaudRate(SerialBaudRate::Baud_115200) != SUCCESS_)
		return ERROR_;

	return SUCCESS_;
}

int AlliedCamera::Close()
{
	int err = SUCCESS_;

	if (_isRunning)
	{
		CameraDisarm();
		_isRunning = false;
	}

	for (FramePtrVector::iterator iter = _frames.begin(); _frames.end() != iter; ++iter)
	{
		// Unregister the frame observer/callback
		auto ret = (*iter)->UnregisterObserver();
	}
	err = _camera->FlushQueue();
	err = _camera->RevokeAllFrames();
	_frames.clear();

	if (_camera)
	{
		if (_camera->Close() != VmbErrorSuccess) err = ERROR_;

		_camera.reset();
	}

	if (_sn != nullptr)
	{
		free(_sn);
		_sn = nullptr;
	}

	return err;
}

int AlliedCamera::GetModelName(char* model_name, int str_length)
{
	CHECKVALID(_camera);
	std::string model_name_s;
	if (_camera->GetModel(model_name_s) != VmbErrorSuccess) return ERROR_;
	auto length = model_name_s.length();
	if ((int)length >= str_length) return ERROR_;
	strcpy_s(model_name, length + 1, model_name_s.c_str());
	return SUCCESS_;
}

int AlliedCamera::GetFirmware(char* firmware_version, int str_length)
{
	std::string _firmware_version;
	if (GetFeatureValue("DeviceFirmwareID", _firmware_version) != 0) return ERROR_;
	auto length = _firmware_version.length();
	if ((int)length >= str_length) return ERROR_;
	strcpy_s(firmware_version, length + 1, _firmware_version.c_str());
	return SUCCESS_;
}


int AlliedCamera::SetExposureTime(double exposure_us)
{
	CHECKVALID(_camera);
	FeaturePtr exposure_feature;
	VmbErrorType error = _camera->GetFeatureByName("ExposureTime", exposure_feature);
	CHECKERROR(error);

	double expT = exposure_us;

	double _exposure_time_us_min;
	double _exposure_time_us_max;
	error = exposure_feature->GetRange(_exposure_time_us_min, _exposure_time_us_max);
	CHECKERROR(error);


	if (expT < _exposure_time_us_min)
	{
		expT = _exposure_time_us_min;
	}
	else if (expT > _exposure_time_us_max)
	{
		expT = _exposure_time_us_max;
	}
	else
	{
		double incre;
		error = exposure_feature->GetIncrement(incre);
		CHECKERROR(error);

		double multiplier = std::floor((expT - _exposure_time_us_min) / incre + 0.05);
		expT = multiplier * incre + _exposure_time_us_min;
	}

	error = exposure_feature->SetValue(expT);
	CHECKERROR(error);

	return SUCCESS_;
}

int AlliedCamera::GetExposureTime(double* exposure)
{
	CHECKVALID(_camera);
	FeaturePtr exposure_feature;
	VmbErrorType error = _camera->GetFeatureByName("ExposureTime", exposure_feature);
	CHECKERROR(error);

	double _exposure;
	error = exposure_feature->GetValue(_exposure);
	CHECKERROR(error);

	*exposure = _exposure;
	return SUCCESS_;
}

int AlliedCamera::GetExposureTimeRange(double* exposure_us_min, double* exposure_us_max, double* exposure_us_inc)
{
	CHECKVALID(_camera);
	FeaturePtr exposure_feature;
	VmbErrorType error = _camera->GetFeatureByName("ExposureTime", exposure_feature);
	CHECKERROR(error);

	double _exposure_time_us_min;
	double _exposure_time_us_max;
	error = exposure_feature->GetRange(_exposure_time_us_min, _exposure_time_us_max);
	CHECKERROR(error);
	*exposure_us_min = _exposure_time_us_min;
	*exposure_us_max = _exposure_time_us_max;

	double incre;
	error = exposure_feature->GetIncrement(incre);
	CHECKERROR(error);
	*exposure_us_inc = incre;

	return SUCCESS_;
}

int AlliedCamera::SetGain(double gain)
{
	CHECKVALID(_camera);
	FeaturePtr gain_feature;
	VmbErrorType error = _camera->GetFeatureByName("Gain", gain_feature);
	CHECKERROR(error);

	error = gain_feature->SetValue(gain);
	CHECKERROR(error);

	return SUCCESS_;
}

int AlliedCamera::GetGain(double* gain)
{
	CHECKVALID(_camera);
	FeaturePtr gain_feature;
	VmbErrorType error = _camera->GetFeatureByName("Gain", gain_feature);
	CHECKERROR(error);

	double _gain;
	error = gain_feature->GetValue(_gain);
	CHECKERROR(error);
	*gain = _gain;
	return SUCCESS_;
}

int AlliedCamera::GetGainRange(double* gain_min, double* gain_max)
{
	CHECKVALID(_camera);
	FeaturePtr gain_feature;
	VmbErrorType error = _camera->GetFeatureByName("Gain", gain_feature);
	CHECKERROR(error);

	double _gain_min;
	double _gain_max;
	error = gain_feature->GetRange(_gain_min, _gain_max);
	CHECKERROR(error);
	*gain_min = _gain_min;
	*gain_max = _gain_max;
	return SUCCESS_;
}

int AlliedCamera::SetBlackLevel(int black_level)
{
	CHECKVALID(_camera);
	FeaturePtr black_level_feature;
	VmbErrorType error = _camera->GetFeatureByName("BlackLevel", black_level_feature);
	CHECKERROR(error);

	error = black_level_feature->SetValue(black_level);
	CHECKERROR(error);

	return SUCCESS_;
}

int AlliedCamera::GetBlackLevel(int* black_level)
{
	CHECKVALID(_camera);
	FeaturePtr black_level_feature;
	VmbErrorType error = _camera->GetFeatureByName("BlackLevel", black_level_feature);
	CHECKERROR(error);

	double _black_level;
	error = black_level_feature->GetValue(_black_level);
	CHECKERROR(error);
	*black_level = (int)_black_level;
	return SUCCESS_;
}

int AlliedCamera::GetBlackLevelRange(int* min, int* max)
{
	CHECKVALID(_camera);
	FeaturePtr black_level_feature;
	VmbErrorType error = _camera->GetFeatureByName("BlackLevel", black_level_feature);
	CHECKERROR(error);

	double _min;
	double _max;
	error = black_level_feature->GetRange(_min, _max);
	CHECKERROR(error);
	*min = (int)_min;
	*max = (int)_max;
	return SUCCESS_;
}

int AlliedCamera::SetBinX(int xbin)
{
	CHECKVALID(_camera);
	FeaturePtr bin_feature;
	VmbErrorType error = _camera->GetFeatureByName("BinningHorizontal", bin_feature);
	CHECKERROR(error);

	error = bin_feature->SetValue(xbin);
	CHECKERROR(error);

	return SUCCESS_;
}

int AlliedCamera::GetBinX(int* binx)
{
	CHECKVALID(_camera);
	FeaturePtr bin_feature;
	VmbErrorType error = _camera->GetFeatureByName("BinningHorizontal", bin_feature);
	CHECKERROR(error);

	VmbInt64_t _bin_x;
	error = bin_feature->GetValue(_bin_x);
	CHECKERROR(error);
	*binx = (int)_bin_x;
	return SUCCESS_;
}

int AlliedCamera::GetBinXRange(int* hbin_min, int* hbin_max)
{
	CHECKVALID(_camera);
	FeaturePtr bin_feature;
	VmbErrorType error = _camera->GetFeatureByName("BinningHorizontal", bin_feature);
	CHECKERROR(error);

	VmbInt64_t bin_min;
	VmbInt64_t bin_max;
	error = bin_feature->GetRange(bin_min, bin_max);
	CHECKERROR(error);
	*hbin_min = (int)bin_min;
	*hbin_max = (int)bin_max;
	return SUCCESS_;
}

int AlliedCamera::SetBinY(int ybin)
{
	CHECKVALID(_camera);
	FeaturePtr bin_feature;
	VmbErrorType error = _camera->GetFeatureByName("BinningVertical", bin_feature);
	CHECKERROR(error);

	error = bin_feature->SetValue(ybin);
	CHECKERROR(error);

	return SUCCESS_;
}

int AlliedCamera::GetBinY(int* biny)
{
	CHECKVALID(_camera);
	FeaturePtr bin_feature;
	VmbErrorType error = _camera->GetFeatureByName("BinningVertical", bin_feature);
	CHECKERROR(error);

	VmbInt64_t _bin_y;
	error = bin_feature->GetValue(_bin_y);
	CHECKERROR(error);
	*biny = (int)_bin_y;
	return SUCCESS_;
}

int AlliedCamera::GetBinYRange(int* vbin_min, int* vbin_max)
{
	CHECKVALID(_camera);
	FeaturePtr bin_feature;
	VmbErrorType error = _camera->GetFeatureByName("BinningVertical", bin_feature);
	CHECKERROR(error);

	VmbInt64_t bin_min;
	VmbInt64_t bin_max;
	error = bin_feature->GetRange(bin_min, bin_max);
	CHECKERROR(error);
	*vbin_min = (int)bin_min;
	*vbin_max = (int)bin_max;
	return SUCCESS_;
}

int AlliedCamera::SetROI(int x, int y, int width, int height)
{
	CHECKVALID(_camera);

	FeaturePtr feature;
	VmbErrorType error;

	error = _camera->GetFeatureByName("Width", feature);
	CHECKERROR(error);
	VmbInt64_t incre;
	error = feature->GetIncrement(incre);
	CHECKERROR(error);
	int64_t containX = width / incre;
	int64_t adjustWidth = containX * incre;
	VmbInt64_t width_min, width_max;
	error = feature->GetRange(width_min, width_max);
	CHECKERROR(error);
	if (adjustWidth < width_min)
		adjustWidth = width_min;
	if (adjustWidth > width_max)
		adjustWidth = width_max;
	error = feature->SetValue(adjustWidth);
	CHECKERROR(error);

	error = _camera->GetFeatureByName("Height", feature);
	CHECKERROR(error);
	error = feature->GetIncrement(incre);
	CHECKERROR(error);
	int64_t containY = height / incre;
	int64_t adjustHeight = containY * incre;
	VmbInt64_t height_min, height_max;
	error = feature->GetRange(height_min, height_max);
	CHECKERROR(error);
	if (adjustHeight < height_min)
		adjustHeight = height_min;
	if (adjustHeight > height_max)
		adjustHeight = height_max;
	error = feature->SetValue(adjustHeight);
	CHECKERROR(error);

	error = _camera->GetFeatureByName("OffsetX", feature);
	CHECKERROR(error);
	VmbInt64_t offset_x_min, offset_x_max;
	error = feature->GetRange(offset_x_min, offset_x_max);
	CHECKERROR(error);
	int64_t x_64 = x;
	if (x_64 < offset_x_min)
		x_64 = offset_x_min;
	if (x_64 > offset_x_max)
		x_64 = offset_x_max;
	error = feature->SetValue(x_64);
	CHECKERROR(error);

	error = _camera->GetFeatureByName("OffsetY", feature);
	CHECKERROR(error);
	VmbInt64_t offset_y_min, offset_y_max;
	error = feature->GetRange(offset_y_min, offset_y_max);
	CHECKERROR(error);
	int64_t y_64 = y;
	if (y_64 < offset_y_min)
		y_64 = offset_y_min;
	if (y_64 > offset_y_max)
		y_64 = offset_y_max;
	error = feature->SetValue(y_64);
	CHECKERROR(error);

	return SUCCESS_;
}

int AlliedCamera::GetROI(int* x, int* y, int* width, int* height)
{
	CHECKVALID(_camera);

	FeaturePtr feature;
	VmbErrorType error = _camera->GetFeatureByName("OffsetX", feature);
	CHECKERROR(error);
	VmbInt64_t left_x;
	error = feature->GetValue(left_x);
	CHECKERROR(error);

	error = _camera->GetFeatureByName("OffsetY", feature);
	CHECKERROR(error);
	VmbInt64_t left_y;
	error = feature->GetValue(left_y);
	CHECKERROR(error);

	error = _camera->GetFeatureByName("Width", feature);
	CHECKERROR(error);
	VmbInt64_t Width;
	error = feature->GetValue(Width);
	CHECKERROR(error);

	error = _camera->GetFeatureByName("Height", feature);
	CHECKERROR(error);
	VmbInt64_t Height;
	error = feature->GetValue(Height);
	CHECKERROR(error);

	*x = (int)left_x;
	*y = (int)left_y;
	*width = (int)(Width);
	*height = (int)(Height);

	//For polar and color processors
	//_roi_start_x = (int)left_x;
	//_roi_start_y = (int)left_y;
	//_roi_end_x = (int)(Width);
	//_roi_end_y = (int)(Height);

	return SUCCESS_;
}

int AlliedCamera::GetROIRange(int* upper_left_x_pixels_min, int* upper_left_y_pixels_min, int* lower_right_x_pixels_min, int* lower_right_y_pixels_min, int* upper_left_x_pixels_max, int* upper_left_y_pixels_max, int* lower_right_x_pixels_max, int* lower_right_y_pixels_max)
{
	CHECKVALID(_camera);

	FeaturePtr feature;
	VmbErrorType error = _camera->GetFeatureByName("Width", feature);
	CHECKERROR(error);
	VmbInt64_t width_min, width_max;
	error = feature->GetRange(width_min, width_max);
	CHECKERROR(error);

	error = _camera->GetFeatureByName("Height", feature);
	CHECKERROR(error);
	VmbInt64_t height_min, height_max;
	error = feature->GetRange(height_min, height_max);
	CHECKERROR(error);

	error = _camera->GetFeatureByName("OffsetX", feature);
	CHECKERROR(error);
	VmbInt64_t offset_x_min, offset_x_max;
	error = feature->GetRange(offset_x_min, offset_x_max);
	CHECKERROR(error);

	error = _camera->GetFeatureByName("OffsetY", feature);
	CHECKERROR(error);
	VmbInt64_t offset_y_min, offset_y_max;
	error = feature->GetRange(offset_y_min, offset_y_max);
	CHECKERROR(error);

	int total_width = (int)(width_max + offset_x_max);
	int total_height = (int)(height_max + offset_y_max);

	*upper_left_x_pixels_min = (int)offset_x_min;
	*upper_left_y_pixels_min = (int)offset_y_min;
	*lower_right_x_pixels_min = (int)width_min;
	*lower_right_y_pixels_min = (int)height_min;
	*upper_left_x_pixels_max = (int)(total_width - width_min);
	*upper_left_y_pixels_max = (int)(total_height - height_min);
	*lower_right_x_pixels_max = total_width;
	*lower_right_y_pixels_max = total_height;

	return SUCCESS_;
}

int AlliedCamera::GetBitDepth(int* pixel_bit_depth)
{
	CHECKVALID(_camera);
	FeaturePtr pixel_type;
	VmbErrorType error = _camera->GetFeatureByName("PixelFormat", pixel_type);
	CHECKERROR(error);

	std::string pixel_type_str;
	error = pixel_type->GetValue(pixel_type_str);
	auto index = pixel_type_str.find("Mono");
	int bit_depth = 8;
	if (index >= 0)
	{
		if (pixel_type_str.find("Mono8") == 0)
			bit_depth = 8;
		if (pixel_type_str.find("Mono10") == 0)
			bit_depth = 10;
		if (pixel_type_str.find("Mono12") == 0)
			bit_depth = 12;
	}

	CHECKERROR(error);
	*pixel_bit_depth = bit_depth;
	return SUCCESS_;
}

int AlliedCamera::GetImageHeight(int* height_pixels)
{
	CHECKVALID(_camera);
	FeaturePtr height_feature;
	VmbErrorType error = _camera->GetFeatureByName("Height", height_feature);
	CHECKERROR(error);

	VmbInt64_t height;
	error = height_feature->GetValue(height);
	CHECKERROR(error);
	*height_pixels = (int)height;
	return SUCCESS_;
}

int AlliedCamera::GetImageWidth(int* width_pixels)
{
	CHECKVALID(_camera);
	FeaturePtr width_feature;
	VmbErrorType error = _camera->GetFeatureByName("Width", width_feature);
	CHECKERROR(error);

	VmbInt64_t width;
	error = width_feature->GetValue(width);
	CHECKERROR(error);
	*width_pixels = (int)width;
	return SUCCESS_;
}

int AlliedCamera::GetImageWidthAndHeight(int* width_pixels, int* height_pixels)
{
	CHECKVALID(_camera);

	FeaturePtr feature;
	VmbErrorType error = _camera->GetFeatureByName("Width", feature);
	CHECKERROR(error);
	VmbInt64_t width;
	error = feature->GetValue(width);
	CHECKERROR(error);

	error = _camera->GetFeatureByName("Height", feature);
	CHECKERROR(error);
	VmbInt64_t height;
	error = feature->GetValue(height);
	CHECKERROR(error);

	*width_pixels = (int)width;
	*height_pixels = (int)height;
	return SUCCESS_;
}

int AlliedCamera::GetSensorHeight(int* height_pixels)
{
	CHECKVALID(_camera);
	FeaturePtr height_feature;
	VmbErrorType error = _camera->GetFeatureByName("SensorHeight", height_feature);
	CHECKERROR(error);

	VmbInt64_t sensor_height;
	error = height_feature->GetValue(sensor_height);
	CHECKERROR(error);
	*height_pixels = (int)sensor_height;
	return SUCCESS_;
}

int AlliedCamera::GetSensorWidth(int* width_pixels)
{
	CHECKVALID(_camera);
	FeaturePtr width_feature;
	VmbErrorType error = _camera->GetFeatureByName("SensorWidth", width_feature);
	CHECKERROR(error);

	VmbInt64_t sensor_width;
	error = width_feature->GetValue(sensor_width);
	CHECKERROR(error);
	*width_pixels = (int)sensor_width;
	return SUCCESS_;
}

int AlliedCamera::GetFrameRate(float* frameRate)
{
	CHECKVALID(_camera);
	FeaturePtr frame_time_feature;
	VmbErrorType error = _camera->GetFeatureByName("FrameTime", frame_time_feature);
	CHECKERROR(error);

	VmbInt64_t frame_time_us;
	error = frame_time_feature->GetValue(frame_time_us);
	CHECKERROR(error);

	*frameRate = 1.0f / static_cast<float>(frame_time_us) * 1E6f;
	return SUCCESS_;
}

int AlliedCamera::SetRecieveImageCallback(RecieveImage callback)
{
	CHECKVALID(_camera);
	if (_isRunning)
	{
		return ERROR_;
	}
	_callback = callback;
	return SUCCESS_;
}

int AlliedCamera::SetTriggerMode(CameraTriggerMode mode, HardwareTriggerPolarity plority)
{
	std::string trigger_mode;
	std::string trigger_polarity;
	VmbErrorType err;
	switch (mode)
	{
	case CameraTriggerMode::SOFTWARE_TRIGGER:
	{
		FeaturePtr pTriggerSrc;
		err = _camera->GetFeatureByName("TriggerSource", pTriggerSrc);
		CHECKERROR(err);
		err = pTriggerSrc->SetValue("Software");
		CHECKERROR(err);
		break;
	}
	case CameraTriggerMode::HARDWARE_TRIGGER:
		trigger_mode = "Hardware";
		switch (plority)
		{
		case HardwareTriggerPolarity::RISING:
		{
			trigger_polarity = "Rising";
			break;
		}
		case HardwareTriggerPolarity::FALLING:
			trigger_polarity = "Falling";
			break;
		}
		break;
	case CameraTriggerMode::BULB:
		trigger_mode = "Bubble";
		switch (plority)
		{
		case HardwareTriggerPolarity::RISING:
		{
			trigger_polarity = "Rising";
			break;
		}
		case HardwareTriggerPolarity::FALLING:
			trigger_polarity = "Falling";
			break;
		}
		break;
	}
	_triggerMode = mode;
	return SUCCESS_;
}

int AlliedCamera::SetFramePerTrigger(const unsigned int& frame)
{
	if (frame == 0)
	{
		auto ret = SetFeatureValue("AcquisitionMode", "Continuous");
		if (ret != 0)
			return ERROR_;

		ret = SetFeatureValue("TriggerMode", "Off");
		if (ret != 0)
			return ERROR_;

		_frames_per_trigger = 0;
	}
	else if (frame == 1)
	{
		auto ret = SetFeatureValue("AcquisitionMode", "SingleFrame");
		if (ret != 0)
			return ERROR_;

		ret = SetFeatureValue("TriggerSelector", "FrameStart");
		if (ret != 0)
			return ERROR_;

		ret = SetFeatureValue("TriggerMode", "Off");
		if (ret != 0)
			return ERROR_;

		_frames_per_trigger = frame;
	}
	else
	{
		auto ret = SetFeatureValue("AcquisitionMode", "MultiFrame");
		if (ret != 0)
			return ERROR_; 
		
		ret = SetFeatureValue("TriggerMode", "Off");
		if (ret != 0)
			return ERROR_;		

		_frames_per_trigger = frame;
	}
	return SUCCESS_;
}

int AlliedCamera::GetFramePerTrigger(unsigned int& frame)
{
	if (_frames_per_trigger == 0)
	{
		std::string mode;
		if (GetFeatureValue("AcquisitionMode", mode) != 0) return ERROR_;
		if (mode != "Continuous") return ERROR_;
	}
	else if (_frames_per_trigger == 1)
	{
		std::string mode;
		if (GetFeatureValue("AcquisitionMode", mode) != 0) return ERROR_;
		if (mode != "SingleFrame") return ERROR_;
	}
	else
	{
		std::string mode;
		if (GetFeatureValue("AcquisitionMode", mode) != 0) return ERROR_;
		if (mode != "MultiFrame") return ERROR_;
	}
	frame = _frames_per_trigger;
	return SUCCESS_;
}

int AlliedCamera::IssueSoftwareTirggle()
{
	//if (_frames_per_trigger == 1)
	//{
	//	FeaturePtr pFeature;
	//	VmbErrorType err = _camera->GetFeatureByName("TriggerSoftware", pFeature);
	//	CHECKERROR(err);
	//	err = pFeature->RunCommand();
	//	CHECKERROR(err);
	//}
	FeaturePtr pFeature;
	VmbErrorType err = _camera->GetFeatureByName("AcquisitionStart", pFeature);
	CHECKERROR(err);
	err = pFeature->RunCommand();
	CHECKERROR(err);

	return SUCCESS_;
}

int AlliedCamera::AcquisitionStop()
{
	FeaturePtr pFeature;
	VmbErrorType err = _camera->GetFeatureByName("AcquisitionStop", pFeature);
	CHECKERROR(err);
	err = pFeature->RunCommand();
	CHECKERROR(err);

	return SUCCESS_;

}

int AlliedCamera::GetColorMode(int* color_type)
{
	CHECKVALID(_camera);
	if (_isColorCamera)
	{
		*color_type = 1;
	}
	else if (_is_polar_camera)
	{
		*color_type = 2;
	}
	else
	{
		*color_type = 0;
	}
	return SUCCESS_;
}

int AlliedCamera::SetAutoExposure(int enable)
{
	CHECKVALID(_camera);

	return NOTSUPPORT_;
}

int AlliedCamera::GetAutoExposure(int* enable)
{
	CHECKVALID(_camera);
	*enable = 0;
	return NOTSUPPORT_;
}

int AlliedCamera::GetVFlip(int* bVFlip)
{
	CHECKVALID(_camera);

	return NOTSUPPORT_;
}

int AlliedCamera::SetVFlip(int bVFlip)
{
	CHECKVALID(_camera);

	return NOTSUPPORT_;
}

int AlliedCamera::GetHFlip(int* bHFlip)
{
	CHECKVALID(_camera);

	return NOTSUPPORT_;
}

int AlliedCamera::SetHFlip(int bHFlip)
{
	CHECKVALID(_camera);

	return NOTSUPPORT_;
}

int AlliedCamera::BayerTransform(unsigned short* imageBuffer, unsigned short* outputBuffer)
{
	return SUCCESS_;
}

int AlliedCamera::ColorTransform48to24(unsigned short* inputBuffer, unsigned char* outputBuffer)
{
	return SUCCESS_;
}

int AlliedCamera::PolarTransform(unsigned short* inputBuffer, PolarImageType imageType, unsigned short* outputBuffer)
{
	return SUCCESS_;
}

int AlliedCamera::CameraArm()
{
	CHECKVALID(_camera);

	int width, height;
	int ret = GetImageWidthAndHeight(&width, &height);
	if (0 != ret)
		return ret;

	FeaturePtr VmbPayloadsizeGet;
	VmbErrorType err = _camera->GetFeatureByName("PayloadSize", VmbPayloadsizeGet);
	CHECKERROR(err);

	VmbInt64_t nPLS;
	err = VmbPayloadsizeGet->GetValue(nPLS);
	CHECKERROR(err);

	for (int i = 0; i < NUM_FRAMES; i++)
	{
		FramePtr frame;
		frame.reset(new Frame(nPLS, FrameAllocation_AnnounceFrame, BUFFER_ALIGNMENT));
		auto frameObserver = new MyFrameObserver(_camera, this, FrameAvailableCallback);
		err = frame->RegisterObserver(IFrameObserverPtr(frameObserver));
		CHECKERROR(err);
		err = _camera->AnnounceFrame(frame);
		CHECKERROR(err);
		_frames.push_back(frame);
	}

	err = _camera->StartCapture();
	CHECKERROR(err);

	for (FramePtrVector::iterator iter = _frames.begin(); iter != _frames.end(); ++iter)
	{
		err = _camera->QueueFrame(*iter);
	}

	//FeaturePtr pFeature;
	//err = _camera->GetFeatureByName("AcquisitionStart", pFeature);
	//CHECKERROR(err);
	//err = pFeature->RunCommand();
	//CHECKERROR(err);

	_isRunning = true;
	return SUCCESS_;
}

int AlliedCamera::CameraDisarm()
{
	CHECKVALID(_camera);
	if (_isRunning)
	{
		_isRunning = false;

		//auto ret=_camera->StopContinuousImageAcquisition();
		FeaturePtr pFeature;
		VmbErrorType err = _camera->GetFeatureByName("AcquisitionStop", pFeature);
		CHECKERROR(err);

		err = pFeature->RunCommand();
		CHECKERROR(err);

		err = _camera->EndCapture();
		CHECKERROR(err);

		for (FramePtrVector::iterator iter = _frames.begin(); _frames.end() != iter; ++iter)
		{
			// Unregister the frame observer/callback
			err = (*iter)->UnregisterObserver();
			CHECKERROR(err);
		}

		err = _camera->FlushQueue();
		CHECKERROR(err);

		err = _camera->RevokeAllFrames();
		CHECKERROR(err);

		_frames.clear();
		//ret = _camera->FlushQueue();
		//ret = _camera->RevokeAllFrames();
		/*for (auto& frame : _frames)
		{
			ret = frame->UnregisterObserver();
		}*/
	}
	return SUCCESS_;
}

int AlliedCamera::SetFrameRateControlValue(double frame_rate_fps)
{
	//CHECKVALID(_camera);
	//if (tl_camera_set_frame_rate_control_value(_camera, frame_rate_fps))
	//	return ERROR_;
	return SUCCESS_;
}

int AlliedCamera::GetFrameRateControlValue(double* frame_rate_fps)
{
	//CHECKVALID(_camera);
	//if (tl_camera_get_frame_rate_control_value(_camera, frame_rate_fps))
	//	return ERROR_;
	return SUCCESS_;
}

int AlliedCamera::GetFrameRateControlValueRange(double* min, double* max)
{
	*min = -1;
	*max = -1;

	/*if (tl_camera_get_frame_rate_control_value_range(_camera, min, max))
		return ERROR_;*/
	return SUCCESS_;
}

int AlliedCamera::SetFrameRateControlEnabled(int is_enabled)
{
	//if (tl_camera_set_is_frame_rate_control_enabled(_camera, is_enabled))
	//	return ERROR_;
	return SUCCESS_;
}

int AlliedCamera::GetFrameRateControlEnabled(int* is_enabled)
{
	//if (tl_camera_get_is_frame_rate_control_enabled(_camera, is_enabled))
	//	return ERROR_;
	return SUCCESS_;
}

int AlliedCamera::GetHotPixelCorrectionEnabled(int* is_enabled)
{
	//not support
	return SUCCESS_;
}

int AlliedCamera::SetHotPixelCorrectionEnabled(int is_enabled)
{
	//not support
	return SUCCESS_;
}

int AlliedCamera::GetHotPixelCorrectionThresholdRange(int* min, int* max)
{
	//not support
	return SUCCESS_;
}

int AlliedCamera::GetHotPixelCorrectionThreshold(int* threshold)
{
	//not support
	return SUCCESS_;
}

int AlliedCamera::SetHotPixelCorrectionThreshold(int threshold)
{
	//not support
	return SUCCESS_;
}


int AlliedCamera::GetIsOperationModeSupported(CameraTriggerMode mode, int* is_operation_mode_supported)
{
	//CHECKVALID(_camera);
	//switch (mode)
	//{
	//case CameraTriggerMode::SOFTWARE_TRIGGER:
	//	if (tl_camera_get_is_operation_mode_supported(_camera, TL_CAMERA_OPERATION_MODE::TL_CAMERA_OPERATION_MODE_SOFTWARE_TRIGGERED, is_operation_mode_supported))
	//		return ERROR_;
	//	break;
	//case CameraTriggerMode::HARDWARE_TRIGGER:
	//	if (tl_camera_get_is_operation_mode_supported(_camera, TL_CAMERA_OPERATION_MODE::TL_CAMERA_OPERATION_MODE_HARDWARE_TRIGGERED, is_operation_mode_supported))
	//		return ERROR_;
	//	break;
	//}
	return SUCCESS_;
}

int AlliedCamera::GetIsLEDSupported(int* is_led_supported)
{
	//not support
	return SUCCESS_;
}

int AlliedCamera::GetLEDStatus(int* is_led_on)
{
	//not support
	return SUCCESS_;
}

int AlliedCamera::SetLEDStatus(int is_led_on)
{
	//not support
	return SUCCESS_;
}

void AlliedCamera::FrameAvailableCallback(void* imageBuffer, int frameCount, int width, int height, void* context)
{
	if (_sn == nullptr)
		return;

	AlliedCamera* camera = reinterpret_cast<AlliedCamera*>(context);
	if (!camera->_isRunning || camera->IsRemoved || camera->_callback == nullptr) return;
	camera->_callback(imageBuffer, frameCount, width, height);
}

void AlliedCamera::CameraDisconnectCallback(const char* cameraSerialNumber, void* context)
{
	if (_sn == nullptr || strcmp(cameraSerialNumber, _sn) != 0)
		return;
	AlliedCamera* camera = reinterpret_cast<AlliedCamera*>(context);
	if (camera->_callback != nullptr)
	{
		camera->IsRemoved = true;
		camera->_callback((char*)cameraSerialNumber, 0, -1, -1);
	}
}

int AlliedCamera::InitializeSdk(void(__cdecl* disconnect_callback)(const char*))
{
	if (_isInitialized) return SUCCESS_;
	// Initializes camera dll
	if (_system.Startup() != VmbErrorSuccess ||
		_system.RegisterCameraListObserver(ICameraListObserverPtr(new ConnectObserver(disconnect_callback))) != VmbErrorSuccess)
	{
		return ERROR_;
	}
	_isInitialized = true;
	return SUCCESS_;
}

int AlliedCamera::ReleaseSDK()
{
	int ret = SUCCESS_;

	if (_isInitialized)
	{
		if (_system.Shutdown() != VmbErrorSuccess)
		{
			ret = ERROR_;
		}
		_isInitialized = false;
	}

	return ret;
}

int AlliedCamera::GetSensorPixelWidth(double* pixel_width)
{
	//double _pixel_width;
	//if (GetFeatureValue("SensorWidth", _pixel_width) != 0) return ERROR_;

	*pixel_width = 1.0;
	return SUCCESS_;
}

int AlliedCamera::GetSensorPixelHeight(double* pixel_height)
{
	/*double _pixel_height;
	if (GetFeatureValue("SensorPixelHeight", _pixel_height) != 0) return ERROR_;*/

	*pixel_height = 1.0;
	return SUCCESS_;
}


int AlliedCamera::GetCorrectionModeEnabled(bool* is_enabled)
{
	FeaturePtr feature;
	VmbErrorType error = _camera->GetFeatureByName("CorrectionMode", feature);
	CHECKERROR(error);
	std::string is_enable;
	error = feature->GetValue(is_enable);
	CHECKERROR(error);
	*is_enabled = is_enable == "On";
	return SUCCESS_;
}

int AlliedCamera::SetCorrectionModeEnabled(bool is_enabled)
{
	if (is_enabled)
	{
		auto ret = SetFeatureValue("CorrectionMode", "On");
		if (ret != 0)
			return ERROR_;
	}
	else
	{
		auto ret = SetFeatureValue("CorrectionMode", "Off");
		if (ret != 0)
			return ERROR_;
	}
	return SUCCESS_;
}

int AlliedCamera::GetCorrectionMode(int* Mode)
{
	FeaturePtr feature;
	VmbErrorType error = _camera->GetFeatureByName("CorrectionSelector", feature);
	CHECKERROR(error);
	std::string mode;
	error = feature->GetValue(mode);
	CHECKERROR(error);
	//0 : DPC, DefectPixelCorrection; 1 : FNPC, FixedPatternNoiseCorrection
	*Mode = mode == "DefectPixelCorrection" ? 0 : 1;
	return SUCCESS_;
}

int AlliedCamera::SetCorrectionMode(int Mode)
{
	if (Mode == 0)
	{
		auto ret = SetFeatureValue("CorrectionSelector", "DefectPixelCorrection");
		if (ret != 0)
			return ERROR_;
	}
	else
	{
		auto ret = SetFeatureValue("CorrectionSelector", "FixedPatternNoiseCorrection");
		if (ret != 0)
			return ERROR_;
	}
	return SUCCESS_;
}

int AlliedCamera::GetGainAutoEnabled(bool* is_enabled)
{
	FeaturePtr feature;
	VmbErrorType error = _camera->GetFeatureByName("GainAuto", feature);
	CHECKERROR(error);
	std::string is_enable;
	error = feature->GetValue(is_enable);
	CHECKERROR(error);
	*is_enabled = is_enable == "On";
	return SUCCESS_;
}

int AlliedCamera::SetGainAutoEnabled(bool is_enabled)
{
	if (is_enabled )
	{
		auto ret = SetFeatureValue("GainAuto", "On");
		if (ret != 0)
			return ERROR_;
	}
	else
	{
		auto ret = SetFeatureValue("GainAuto", "Off");
		if (ret != 0)
			return ERROR_;
	}
	return SUCCESS_;
}

int AlliedCamera::GetExposureAutoEnabled(int* mode)
{
	FeaturePtr feature;
	VmbErrorType error = _camera->GetFeatureByName("ExposureAuto", feature);
	CHECKERROR(error);
	std::string is_enable;
	error = feature->GetValue(is_enable);
	CHECKERROR(error);
	if (is_enable == "Off")
		*mode = 0;
	else if (is_enable == "Once")
		*mode = 1;
	else if (is_enable == "Continuous")
		*mode = 2;
	else
		return INVALID_;
	return SUCCESS_;
}

int AlliedCamera::SetExposureAutoEnabled(int mode)
{
	if (mode == 0)
	{
		auto ret = SetFeatureValue("ExposureAuto", "Off");
		if (ret != 0)
			return ERROR_;
	}
	else if (mode == 1)
	{
		auto ret = SetFeatureValue("ExposureAuto", "Once");
		if (ret != 0)
			return ERROR_;
	}
	else if (mode == 2)
	{
		auto ret = SetFeatureValue("ExposureAuto", "Continuous");
		if (ret != 0)
			return ERROR_;
	}
	else
		return INVALID_;
	return SUCCESS_;
}

int AlliedCamera::GetReverseXEnabled(bool* is_enabled)
{
	FeaturePtr feature;
	VmbErrorType error = _camera->GetFeatureByName("ReverseX", feature);
	CHECKERROR(error);
	bool is_enable;
	error = feature->GetValue(is_enable);
	CHECKERROR(error);
	*is_enabled = is_enable;
	return SUCCESS_;
}

int AlliedCamera::SetReverseXEnabled(bool is_enabled)
{
	auto ret = SetFeatureValue("ReverseX", is_enabled);
	if (ret != 0)
		return ERROR_;
	return SUCCESS_;
}

int AlliedCamera::GetReverseYEnabled(bool* is_enabled)
{
	FeaturePtr feature;
	VmbErrorType error = _camera->GetFeatureByName("ReverseY", feature);
	CHECKERROR(error);
	bool is_enable;
	error = feature->GetValue(is_enable);
	CHECKERROR(error);
	*is_enabled = is_enable;
	return SUCCESS_;
}

int AlliedCamera::SetReverseYEnabled(bool is_enabled)
{
	auto ret = SetFeatureValue("ReverseY", is_enabled);
	if (ret != 0)
		return ERROR_;
	return SUCCESS_;
}


int AlliedCamera::SetSerialHubEnabled(bool is_enabled)
{
	auto ret = SetFeatureValue("SerialHubEnable", is_enabled);
	if (ret != SUCCESS_)
		return ERROR_;
}

int AlliedCamera::GetSerialHubEnabled(bool* is_enabled)
{
	bool _is_serial_enabled;
	if (GetFeatureValue("SerialHubEnable", _is_serial_enabled) != SUCCESS_) return ERROR_;
	*is_enabled = _is_serial_enabled;
	return SUCCESS_;
}

int AlliedCamera::SetSerialBaudRate(SerialBaudRate baud_rate)
{
	switch (baud_rate)
	{
	case SerialBaudRate::Baud_9600:
	{
		auto ret = SetFeatureValue("SerialBaudRate", "Baud_9600");
		if (ret != SUCCESS_)
			return ERROR_;
		break;
	}
	case SerialBaudRate::Baud_115200:
	{
		auto ret = SetFeatureValue("SerialBaudRate", "Baud_115200");
		break;
	}
	case SerialBaudRate::Baud_230400:
	{
		auto ret = SetFeatureValue("SerialBaudRate", "Baud_230400");
		break;
	}
	}
	return SUCCESS_;
}

int AlliedCamera::GetSerialBaudRate(SerialBaudRate* baud_rate)
{
	std::string _baud;
	auto ret = GetFeatureValue("SerialBaudRate", _baud);
	if (ret != SUCCESS_)
		return ERROR_;

	if (_baud.compare("Baud_9600") == 0) {
		*baud_rate = SerialBaudRate::Baud_9600;
	}
	if (_baud.compare("Baud_115200") == 0)
	{
		*baud_rate = SerialBaudRate::Baud_115200;
	}
	if (_baud.compare("Baud_230400") == 0)
	{
		*baud_rate = SerialBaudRate::Baud_230400;
	}
	return SUCCESS_;
}

int AlliedCamera::SetSerialTxSize(int n_bytes)
{
	if (n_bytes < 0)
		return ERROR_;
	if (n_bytes > 128)
		return ERROR_;

	auto ret = SetFeatureValue("SerialTxSize", n_bytes);
	if (ret != SUCCESS_)
		return ERROR_;

	return SUCCESS_;
}

int AlliedCamera::SetSerialTxData(unsigned char* tx_data)
{
	std::vector<unsigned char>::size_type _size = strlen((const char*)tx_data);
	std::vector<unsigned char> _rx_vector(tx_data, tx_data + _size);

	auto ret = SetFeatureValue("SerialTxData", _rx_vector);

	return ret == SUCCESS_ ? SUCCESS_ : ERROR_;
}

int AlliedCamera::SetSerialTxLockEnabled(int is_enabled)
{
	if (is_enabled == 0)
	{
		auto ret = SetFeatureValue("SerialTxLock", true);
		if (ret != SUCCESS_)
			return ERROR_;
	}
	else
	{
		auto ret = SetFeatureValue("SerialTxLock", false);
		if (ret != SUCCESS_)
			return ERROR_;
	}
	return SUCCESS_;
}

int AlliedCamera::GetSerialTxLockEnabled(int* is_enabled)
{
	bool _is_enabled;
	if (GetFeatureValue("SerialTxLock", _is_enabled) != SUCCESS_) return ERROR_;
	*is_enabled = _is_enabled ? 0 : 1;
	return SUCCESS_;
}

int AlliedCamera::SetSerialRxSize(int n_bytes)
{
	if (n_bytes < 0)
		return ERROR_;
	if (n_bytes > 128)
		return ERROR_;

	auto ret = SetFeatureValue("SerialRxSize", n_bytes);
	if (ret != SUCCESS_)
		return ERROR_;
	return SUCCESS_;
}

int AlliedCamera::GetSerialRxWaiting(int* n_bytes)
{
	std::string _n_bytes;
	if (GetFeatureValue("SerialRxWaiting", _n_bytes) != SUCCESS_) return ERROR_;
	*n_bytes = std::stoi(_n_bytes);
	return SUCCESS_;
}

int AlliedCamera::GetSerialRxData(unsigned char* rx_data)
{
	//if (GetFeatureValue("SerialRxData", rx_data) != SUCCESS_) return ERROR_;
	return SUCCESS_;
}



template <typename T>
int AlliedCamera::GetFeatureValue(std::string feature_name, T& value)
{
	CHECKVALID(_camera);

	FeaturePtr feature_ptr;
	VmbErrorType error = _camera->GetFeatureByName(feature_name.c_str(), feature_ptr);
	CHECKERROR(error);

	T temp_value;
	error = feature_ptr->GetValue(temp_value);
	CHECKERROR(error);

	value = temp_value;
	return SUCCESS_;
}

template <typename T>
int AlliedCamera::SetFeatureValue(std::string feature_name, T value)
{
	CHECKVALID(_camera);

	FeaturePtr feature_ptr;
	VmbErrorType error = _camera->GetFeatureByName(feature_name.c_str(), feature_ptr);
	CHECKERROR(error);

	error = feature_ptr->SetValue(value);
	CHECKERROR(error);

	return SUCCESS_;
}
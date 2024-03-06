// The following ifdef block is the standard way of creating macros which make exporting
// from a DLL simpler. All files within this DLL are compiled with the VIDEOPROCESSLIB_EXPORTS
// symbol defined on the command line. This symbol should not be defined on any project
// that uses this DLL. This way any other project whose source files include this file see
// VIDEOPROCESSLIB_API functions as being imported from a DLL, whereas this DLL sees symbols
// defined with this macro as being exported.
#pragma once
#ifdef VIDEOPROCESSLIB_EXPORTS
#define VIDEOPROCESSLIB_API extern "C" __declspec(dllexport)
#else
#define VIDEOPROCESSLIB_API extern "C" __declspec(dllimport)
#endif
#include <stdint.h>

enum class vpl_color_format {
	VPL_RGB24 = 1,
	VPL_YV12 = 2,
	//VPL_NV12 = 3,
	VPL_L8 = 4,
	//VPL_L16 = 5,
};

typedef struct {
	uint32_t fps;
	uint32_t bit_rate;
	uint32_t width;
	uint32_t height;
	vpl_color_format color;
} vpl_info;

typedef struct {
	int64_t rtStart;
	uint8_t* pdata1;
	uint8_t* pdata2;
	uint8_t* pdata3;
	uint32_t d1stride;
	uint32_t d2stride;
	uint32_t d3stride;
} vpl_data;

VIDEOPROCESSLIB_API int8_t vpl_enc_init(vpl_info info, const wchar_t *path_name);
VIDEOPROCESSLIB_API int8_t vpl_enc_frame(vpl_data imgData);
VIDEOPROCESSLIB_API int8_t vpl_enc_dispose(void);

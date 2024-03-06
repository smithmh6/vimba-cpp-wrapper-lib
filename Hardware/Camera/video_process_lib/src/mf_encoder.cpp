
#include "mf_encoder.h"
#include <mfapi.h>
#include <mfidl.h>
#include <Mfreadwrite.h>
#include <mferror.h>
#include <algorithm>
#include <strmif.h>
#include <Codecapi.h>

#pragma comment(lib, "mfreadwrite")
#pragma comment(lib, "mfplat")
#pragma comment(lib, "mfuuid")

const GUID VIDEO_ENCODING_FORMAT = MFVideoFormat_H264;
static IMFSinkWriter* g_pSinkWriter = NULL;
static ICodecAPI* pCodecApi = NULL;
static DWORD g_stream;
static vpl_info g_info;
static uint8_t* g_uBuf = NULL;
static uint8_t* g_vBuf = NULL;

#define check_status(func) do { \
	HRESULT hr= func; \
	if(!SUCCEEDED(hr)) { \
		return -1; \
	} \
} while(0)

template <class T> void SafeRelease(T** ppT)
{
	if (*ppT)
	{
		(*ppT)->Release();
		*ppT = NULL;
	}
}

int8_t mf_enc_init(vpl_info info, const wchar_t* path_name)
{
	IMFSinkWriter* pSinkWriter = NULL;
	IMFMediaType* pMediaTypeOut = NULL;
	IMFMediaType* pMediaTypeIn = NULL;
	DWORD           stream;

	g_pSinkWriter = NULL;
	g_stream = 0;
	g_info = { 0 };
	g_uBuf = NULL;
	g_vBuf = NULL;
	//check_status(CoInitializeEx(NULL, COINIT_APARTMENTTHREADED));
	check_status(MFStartup(MF_VERSION));

	check_status(MFCreateSinkWriterFromURL(path_name, NULL, NULL, &pSinkWriter));




	// Set the output media type.
	check_status(MFCreateMediaType(&pMediaTypeOut));
	check_status(pMediaTypeOut->SetGUID(MF_MT_MAJOR_TYPE, MFMediaType_Video));
	check_status(pMediaTypeOut->SetGUID(MF_MT_SUBTYPE, VIDEO_ENCODING_FORMAT));
	check_status(pMediaTypeOut->SetUINT32(MF_MT_AVG_BITRATE, info.bit_rate));
	check_status(pMediaTypeOut->SetUINT32(MF_MT_INTERLACE_MODE, MFVideoInterlace_Progressive));

	check_status(MFSetAttributeSize(pMediaTypeOut, MF_MT_FRAME_SIZE, info.width, info.height));
	check_status(MFSetAttributeRatio(pMediaTypeOut, MF_MT_FRAME_RATE, info.fps, 1));
	check_status(MFSetAttributeRatio(pMediaTypeOut, MF_MT_PIXEL_ASPECT_RATIO, 1, 1));

	check_status(pSinkWriter->AddStream(pMediaTypeOut, &stream));


	/*ICodecAPI* pCodecApi;
	HRESULT hr = pSinkWriter->GetServiceForStream(stream, GUID_NULL, __uuidof (ICodecAPI), (LPVOID*)&pCodecApi);

	VARIANT var = { 0 };
	var.vt = VT_UI4;
	var.ulVal = 2;
	hr = pCodecApi->SetValue(&CODECAPI_AVEncNumWorkerThreads, &var);*/



	// Set the input media type.
	check_status(MFCreateMediaType(&pMediaTypeIn));
	check_status(pMediaTypeIn->SetGUID(MF_MT_MAJOR_TYPE, MFMediaType_Video));
	{
		GUID VIDEO_INPUT_FORMAT = MFVideoFormat_YV12;
		if (info.color == vpl_color_format::VPL_RGB24) {
			VIDEO_INPUT_FORMAT = MFVideoFormat_RGB24;
		}
		else if (info.color == vpl_color_format::VPL_L8) {
			g_uBuf = new uint8_t[info.width * info.height / 2];
			g_vBuf = g_uBuf + info.width * info.height / 4;
			std::fill(g_uBuf, g_uBuf + info.width * info.height / 2, 0x80);
		}
		check_status(pMediaTypeIn->SetGUID(MF_MT_SUBTYPE, VIDEO_INPUT_FORMAT));
	}
	check_status(pMediaTypeIn->SetUINT32(MF_MT_INTERLACE_MODE, MFVideoInterlace_Progressive));
	check_status(MFSetAttributeSize(pMediaTypeIn, MF_MT_FRAME_SIZE, info.width, info.height));
	check_status(MFSetAttributeRatio(pMediaTypeIn, MF_MT_FRAME_RATE, info.fps, 1));
	check_status(MFSetAttributeRatio(pMediaTypeIn, MF_MT_PIXEL_ASPECT_RATIO, 1, 1));
	check_status(pSinkWriter->SetInputMediaType(stream, pMediaTypeIn, NULL));



	/*ICodecAPI* pCodecApi;
	HRESULT hr = pSinkWriter->GetServiceForStream(stream, GUID_NULL, __uuidof (ICodecAPI), (LPVOID*)&pCodecApi);

	VARIANT var = { 0 };
	var.vt = VT_UI4;
	var.ulVal = 2;
	hr = pCodecApi->SetValue(&CODECAPI_AVEncNumWorkerThreads, &var);
*/


// start accepting data.
	check_status(pSinkWriter->BeginWriting());
	{
		g_pSinkWriter = pSinkWriter;
		g_pSinkWriter->AddRef();
		g_stream = stream;
		g_info = info;
	}





	HRESULT hr = g_pSinkWriter->GetServiceForStream(0, GUID_NULL, __uuidof (ICodecAPI), (LPVOID*)&pCodecApi);


	VARIANT varTemp1 = { 0 };
	hr = pCodecApi->GetValue(&CODECAPI_AVEncNumWorkerThreads, &varTemp1);
	VARIANT varTemp1_1 = { 0 };
	hr = pCodecApi->GetValue(&CODECAPI_AVEncAdaptiveMode, &varTemp1_1);

	VARIANT var = { 0 };
	var.vt = VT_UI4;
	var.ulVal = 2;
	hr = pCodecApi->SetValue(&CODECAPI_AVEncNumWorkerThreads, &var);

	VARIANT varTemp2 = { 0 };
	hr = pCodecApi->GetValue(&CODECAPI_AVEncNumWorkerThreads, &varTemp2);



	SafeRelease(&pSinkWriter);
	SafeRelease(&pMediaTypeOut);
	SafeRelease(&pMediaTypeIn);
	return 0;
}

int8_t mf_enc_frame(vpl_data imgData)
{
	IMFSample* pSample = NULL;
	IMFMediaBuffer* pBuffer = NULL;
	uint64_t video_frame_duration = 10 * 1000 * 1000 / g_info.fps;
	LONG cbWidth = 0;
	DWORD cbBuffer = 0;
	BYTE* pData = NULL;

	if (g_info.color == vpl_color_format::VPL_RGB24) {
		cbWidth = 3 * g_info.width;
		cbBuffer = cbWidth * g_info.height;
	}
	else if (g_info.color == vpl_color_format::VPL_YV12) {
		cbWidth = g_info.width;
		cbBuffer = cbWidth * g_info.height * 3 / 2;
	}
	else if (g_info.color == vpl_color_format::VPL_L8) {
		cbWidth = g_info.width;
		cbBuffer = cbWidth * g_info.height * 3 / 2;
	}

	// Create a new memory buffer.
	check_status(MFCreateMemoryBuffer(cbBuffer, &pBuffer));
	// Lock the buffer and copy the video frame to the buffer.
	check_status(pBuffer->Lock(&pData, NULL, NULL));

	if (g_info.color == vpl_color_format::VPL_RGB24)
	{
		MFCopyImage(
			pData,                      // Destination buffer.
			cbWidth,                    // Destination stride.
			imgData.pdata1,    // First row in source image.
			imgData.d1stride,                    // Source stride.
			cbWidth,                    // Image width in bytes.
			g_info.height                // Image height in pixels.
		);
	}
	else if (g_info.color == vpl_color_format::VPL_YV12 || g_info.color == vpl_color_format::VPL_L8)
	{
		DWORD pxSize = cbWidth * g_info.height;
		BYTE* puData = pData + pxSize;
		BYTE* pvData = puData + pxSize / 4;
		vpl_data tData = imgData;
		if (g_info.color == vpl_color_format::VPL_L8) {
			tData.pdata2 = g_uBuf;
			tData.pdata3 = g_vBuf;
			tData.d2stride = cbWidth / 2;
			tData.d3stride = cbWidth / 2;
		}
		MFCopyImage(
			pData,                      // Destination buffer.
			cbWidth,                    // Destination stride.
			tData.pdata1,    // First row in source image.
			tData.d1stride,                    // Source stride.
			cbWidth,                    // Image width in bytes.
			g_info.height                // Image height in pixels.
		);
		MFCopyImage(
			puData,                      // Destination buffer.
			cbWidth / 2,                    // Destination stride.
			tData.pdata2,		// First row in source image.
			tData.d2stride,	// Source stride.
			cbWidth / 2,                    // Image width in bytes.
			g_info.height / 2                // Image height in pixels.
		);
		MFCopyImage(
			pvData,                      // Destination buffer.
			cbWidth / 2,                    // Destination stride.
			tData.pdata3,    // First row in source image.
			tData.d3stride,                    // Source stride.
			cbWidth / 2,                    // Image width in bytes.
			g_info.height / 2                // Image height in pixels.
		);
	}

	if (pBuffer) {
		pBuffer->Unlock();
	}
	// Set the data length of the buffer.
	check_status(pBuffer->SetCurrentLength(cbBuffer));
	// Create a media sample and add the buffer to the sample.
	check_status(MFCreateSample(&pSample));
	check_status(pSample->AddBuffer(pBuffer));

	// Set the time stamp and the duration.
	check_status(pSample->SetSampleTime(imgData.rtStart));
	check_status(pSample->SetSampleDuration(video_frame_duration));
	// Send the sample to the Sink Writer.
	check_status(g_pSinkWriter->WriteSample(g_stream, pSample));

	SafeRelease(&pSample);
	SafeRelease(&pBuffer);
	return 0;
}

int8_t mf_enc_dispose(void)
{
	check_status(g_pSinkWriter->Finalize());

	pCodecApi->Release();

	SafeRelease(&g_pSinkWriter);
	check_status(MFShutdown());
	CoUninitialize();
	if (g_info.color == vpl_color_format::VPL_L8) {
		delete[] g_uBuf;
		g_uBuf = NULL;
		g_vBuf = NULL;
	}
	g_pSinkWriter = NULL;
	g_stream = 0;
	g_info = { 0 };
	return 0;
}

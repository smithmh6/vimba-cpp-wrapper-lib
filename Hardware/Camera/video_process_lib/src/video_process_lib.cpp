// video_process_lib.cpp : Defines the exported functions for the DLL.
//

#include "pch.h"
#include "video_process_lib.h"
#include "mf_encoder.h"

VIDEOPROCESSLIB_API int8_t vpl_enc_init(vpl_info info, const wchar_t *path_name)
{
	return mf_enc_init(info, path_name);
}

VIDEOPROCESSLIB_API int8_t vpl_enc_frame(vpl_data imgData)
{
	return mf_enc_frame(imgData);
}

VIDEOPROCESSLIB_API int8_t vpl_enc_dispose(void)
{
	return mf_enc_dispose();
}

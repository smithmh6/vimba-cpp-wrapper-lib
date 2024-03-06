#pragma once
#include <stdint.h>
#include "video_process_lib.h"

int8_t mf_enc_init(vpl_info info, const wchar_t *path_name);
int8_t mf_enc_frame(vpl_data imgData);
int8_t mf_enc_dispose(void);

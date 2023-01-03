#pragma once

#ifdef GAUSSIANBLUR_EXPORTS
#define GAUSSIANBLUR_API __declspec(dllexport)
#else
#define GAUSSIANBLUR_API __declspec(dllimport)
#endif

extern "C" GAUSSIANBLUR_API void blur_image(BYTE image[], BYTE result_image[], BYTE mask[], int mask_sum, int width, int height);
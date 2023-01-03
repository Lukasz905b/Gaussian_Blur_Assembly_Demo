#include "pch.h"
#include "GaussianBlur.h"

void blur_image(BYTE image[], BYTE result_image[], BYTE mask[], int mask_sum, int width, int height)
{
	
	// Padded width of the image in bytes
	int padded_width_bytes = (width + 2) * 3;

	// Padded image size in bytes
	int image_size_bytes = padded_width_bytes * (height + 2);

	// Iterate over all pixels of the image excluding top and bottom padding
	for (int i = padded_width_bytes, j = 0; i < image_size_bytes - padded_width_bytes; i++)
	{
		// Ignore all pixels that are a part of the side padding
		if (i % padded_width_bytes == 0 || i % padded_width_bytes == (padded_width_bytes - 3))
		{
			i += 2;
			continue;
		}

		// Get sum of values of a single color within mask
		int color_sum = 0;

		color_sum += image[i - padded_width_bytes - 3] * mask[0]; // Top left pixel
		color_sum += image[i - padded_width_bytes] * mask[1]; // Top middle pixel
		color_sum += image[i - padded_width_bytes + 3] * mask[2]; // Top right pixel

		color_sum += image[i - 3] * mask[3]; // Middle left pixel
		color_sum += image[i] * mask[4]; // Center pixel
		color_sum += image[i + 3] * mask[5]; // Middle right pixel

		color_sum += image[i + padded_width_bytes - 3] * mask[6]; // Bottom left pixel
		color_sum += image[i + padded_width_bytes] * mask[7]; // Bottom middle pixel
		color_sum += image[i + padded_width_bytes + 3] * mask[8]; // Bottom right pixel

		// Divide sum of color values by sum of mask weights to find the average
		color_sum = color_sum / mask_sum;

		// Place the byte in the next spot of the result image
		result_image[j] = color_sum;
		j++;
	}
	
}
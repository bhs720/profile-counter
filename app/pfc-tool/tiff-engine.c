#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <stdarg.h>

#define WIN32_LEAN_AND_MEAN
#include <windows.h>

#include "tiffio.h"
#include "tiff-engine.h"

/* mupdf's own limits, from source/fitz/image.c. Page sizes have to agree with
 * the mupdf path: the same drawing must land in the same paper-size bucket
 * whichever engine measured it. */
#define SANE_DPI 72.0f
#define INSANE_DPI 4800.0f
#define POINTS_PER_INCH 72.0f

/* Declared rather than pulling in all of mupdf/fitz.h for one function. It
 * converts a UTF-8 path to wide characters and calls _wfopen, which is what is
 * needed here too; libmupdf.lib supplies it. */
void *fz_fopen_utf8(const char *name, const char *mode);

/* libtiff reports problems through these; the tool's own stderr is enough. */
static void tiff_quiet(const char *module, const char *fmt, va_list ap)
{
	(void)module; (void)fmt; (void)ap;
}

static TIFF *tiff_open_utf8(const char *filename_utf8)
{
	wchar_t *wide;
	int chars;
	TIFF *tif;

	/* libtiff's TIFFOpen takes the path in the ANSI code page, which would undo
	 * the UTF-16 command line that wmain() exists to preserve. TIFFOpenW keeps
	 * non-ASCII filenames intact. */
	chars = MultiByteToWideChar(CP_UTF8, 0, filename_utf8, -1, NULL, 0);
	if (chars <= 0)
		return NULL;

	wide = (wchar_t *)malloc((size_t)chars * sizeof(wchar_t));
	if (wide == NULL)
		return NULL;

	if (MultiByteToWideChar(CP_UTF8, 0, filename_utf8, -1, wide, chars) <= 0)
	{
		free(wide);
		return NULL;
	}

	tif = TIFFOpenW(wide, "r");
	free(wide);
	return tif;
}

int tiff_is_tiff(const char *filename_utf8)
{
	unsigned char magic[4];
	size_t n;
	FILE *f;

	f = (FILE *)fz_fopen_utf8(filename_utf8, "rb");
	if (f == NULL)
		return 0;

	n = fread(magic, 1, sizeof magic, f);
	fclose(f);

	if (n < 4)
		return 0;

	/* II 42 0 little endian, MM 0 42 big endian. BigTIFF uses 43 and is not
	 * claimed here: libtiff reads it, but nothing in the corpus exercises it. */
	if (magic[0] == 'I' && magic[1] == 'I' && magic[2] == 42 && magic[3] == 0)
		return 1;
	if (magic[0] == 'M' && magic[1] == 'M' && magic[2] == 0 && magic[3] == 42)
		return 1;

	return 0;
}

/* Reproduces fz_image_resolution(). A TIFF may carry no resolution, a
 * resolution of zero, or something wildly implausible; mupdf substitutes and
 * rescales rather than believing it, and the two engines have to agree. */
static void sane_resolution(float *xres, float *yres)
{
	if (*xres < 0 || *yres < 0 || (*xres == 0 && *yres == 0))
	{
		*xres = SANE_DPI;
		*yres = SANE_DPI;
	}
	else if (*xres == 0)
		*xres = *yres;
	else if (*yres == 0)
		*yres = *xres;

	if (*xres < SANE_DPI || *yres < SANE_DPI || *xres > INSANE_DPI || *yres > INSANE_DPI)
	{
		if (*xres < *yres)
		{
			*yres = *yres * SANE_DPI / *xres;
			*xres = SANE_DPI;
		}
		else
		{
			*xres = *xres * SANE_DPI / *yres;
			*yres = SANE_DPI;
		}

		if (*xres == *yres || *xres < SANE_DPI || *yres < SANE_DPI || *xres > INSANE_DPI || *yres > INSANE_DPI)
		{
			*xres = SANE_DPI;
			*yres = SANE_DPI;
		}
	}
}

static void page_size_points(TIFF *tif, float *width_pt, float *height_pt)
{
	uint32_t w = 0, h = 0;
	float xres = 0, yres = 0;
	uint16_t unit = RESUNIT_INCH;

	TIFFGetField(tif, TIFFTAG_IMAGEWIDTH, &w);
	TIFFGetField(tif, TIFFTAG_IMAGELENGTH, &h);

	if (!TIFFGetField(tif, TIFFTAG_XRESOLUTION, &xres)) xres = 0;
	if (!TIFFGetField(tif, TIFFTAG_YRESOLUTION, &yres)) yres = 0;
	TIFFGetFieldDefaulted(tif, TIFFTAG_RESOLUTIONUNIT, &unit);

	if (unit == RESUNIT_CENTIMETER)
	{
		xres *= 2.54f;
		yres *= 2.54f;
	}
	else if (unit == RESUNIT_NONE)
	{
		/* The values are an aspect ratio, not a physical density. */
		xres = 0;
		yres = 0;
	}

	sane_resolution(&xres, &yres);

	*width_pt = w * POINTS_PER_INCH / xres;
	*height_pt = h * POINTS_PER_INCH / yres;
}

static int is_colour_u8(int threshold_u8, int r, int g, int b)
{
	return abs(r - g) > threshold_u8 || abs(r - b) > threshold_u8 || abs(g - b) > threshold_u8;
}

/* -1 unknown, 0 black and white, 1 colour. */
static int colour_from_tags(TIFF *tif, int threshold_u8)
{
	uint16_t photometric = PHOTOMETRIC_MINISWHITE, bits = 1;
	uint16_t *red = NULL, *green = NULL, *blue = NULL;

	TIFFGetFieldDefaulted(tif, TIFFTAG_PHOTOMETRIC, &photometric);
	TIFFGetFieldDefaulted(tif, TIFFTAG_BITSPERSAMPLE, &bits);

	switch (photometric)
	{
	case PHOTOMETRIC_MINISWHITE:
	case PHOTOMETRIC_MINISBLACK:
		/* One intensity channel: every pixel converts to r == g == b, so no
		 * threshold can be exceeded. True whatever the compression, which is
		 * why this answer costs nothing and needs no codec. */
		return 0;

	case PHOTOMETRIC_PALETTE:
		/* The palette is a tag. If every entry it could reference is grey then
		 * no arrangement of indices produces colour. */
		if (bits <= 8 && TIFFGetField(tif, TIFFTAG_COLORMAP, &red, &green, &blue))
		{
			int entries = 1 << bits, i;
			for (i = 0; i < entries; i++)
			{
				/* Colour map entries are 16 bit; compare on the same 8 bit
				 * scale as the pixel test. */
				if (is_colour_u8(threshold_u8, red[i] >> 8, green[i] >> 8, blue[i] >> 8))
					return -1;   /* a colour entry exists; which are used needs pixels */
			}
			return 0;
		}
		break;
	}

	return -1;
}

/* Scans pixels through libtiff's RGBA reader, which handles photometric
 * interpretation, palettes, bit depths, planar configuration and byte order so
 * this does not have to. Reads a strip or tile at a time so memory scales with
 * that rather than with the image: the pages where that distinction matters
 * most are bilevel and never reach here. */
static int colour_by_pixels(TIFF *tif, int threshold_u8)
{
	uint32_t w = 0, h = 0;
	uint32_t *raster = NULL;
	int found = 0;

	TIFFGetField(tif, TIFFTAG_IMAGEWIDTH, &w);
	TIFFGetField(tif, TIFFTAG_IMAGELENGTH, &h);
	if (w == 0 || h == 0)
		return -1;

	if (TIFFIsTiled(tif))
	{
		uint32_t tw = 0, th = 0, x, y;

		if (!TIFFGetField(tif, TIFFTAG_TILEWIDTH, &tw) || !TIFFGetField(tif, TIFFTAG_TILELENGTH, &th))
			return -1;
		if (tw == 0 || th == 0)
			return -1;

		raster = (uint32_t *)_TIFFmalloc((tmsize_t)tw * th * sizeof(uint32_t));
		if (raster == NULL)
			return -1;

		for (y = 0; y < h && !found; y += th)
		{
			for (x = 0; x < w && !found; x += tw)
			{
				uint32_t i, count = tw * th;

				if (!TIFFReadRGBATile(tif, x, y, raster))
				{
					/* Say nothing rather than something wrong. */
					_TIFFfree(raster);
					return -1;
				}

				for (i = 0; i < count; i++)
				{
					uint32_t p = raster[i];
					if (TIFFGetA(p) == 0)
						continue;   /* fully transparent pixels are not shown */
					if (is_colour_u8(threshold_u8, TIFFGetR(p), TIFFGetG(p), TIFFGetB(p)))
					{
						found = 1;
						break;
					}
				}
			}
		}
	}
	else
	{
		uint32_t rows_per_strip = 0, row;

		if (!TIFFGetField(tif, TIFFTAG_ROWSPERSTRIP, &rows_per_strip) || rows_per_strip == 0 || rows_per_strip > h)
			rows_per_strip = h;

		raster = (uint32_t *)_TIFFmalloc((tmsize_t)w * rows_per_strip * sizeof(uint32_t));
		if (raster == NULL)
			return -1;

		for (row = 0; row < h && !found; row += rows_per_strip)
		{
			uint32_t nrows = (h - row < rows_per_strip) ? h - row : rows_per_strip;
			uint32_t i, count = w * nrows;

			if (!TIFFReadRGBAStrip(tif, row, raster))
			{
				_TIFFfree(raster);
				return -1;
			}

			for (i = 0; i < count; i++)
			{
				uint32_t p = raster[i];
				if (TIFFGetA(p) == 0)
					continue;
				if (is_colour_u8(threshold_u8, TIFFGetR(p), TIFFGetG(p), TIFFGetB(p)))
				{
					found = 1;
					break;
				}
			}
		}
	}

	_TIFFfree(raster);
	return found;
}

int tiff_analyze(const char *filename_utf8, float color_threshold, int test_color, int test_pixels)
{
	TIFF *tif;
	int pagecount = 0, page, measured = 0;
	int threshold_u8;

	TIFFSetErrorHandler(tiff_quiet);
	TIFFSetWarningHandler(tiff_quiet);

	threshold_u8 = (int)(color_threshold * 255);

	tif = tiff_open_utf8(filename_utf8);
	if (tif == NULL)
	{
		fprintf(stderr, "Failed to open document\n");
		return 1;
	}

	/* Page count is a walk of the directory chain; no pixels are touched. */
	do
	{
		pagecount++;
	} while (TIFFReadDirectory(tif));

	if (pagecount == 0)
	{
		fprintf(stderr, "Could not read page count\n");
		TIFFClose(tif);
		return 1;
	}

	fprintf(stdout, "PageCount=%i BookmarkCount=%i\n", pagecount, 0);
	fflush(stdout);

	TIFFSetDirectory(tif, 0);

	for (page = 0; page < pagecount; page++)
	{
		float width_pt = 0, height_pt = 0;
		int is_color = -1;

		if (page > 0 && !TIFFSetDirectory(tif, (uint16_t)page))
		{
			fprintf(stderr, "Page %i failed\n", page + 1);
			fprintf(stdout, "Page=%i Size=%f,%f Color=%i\n", page + 1, 0.0, 0.0, -1);
			fflush(stdout);
			continue;
		}

		page_size_points(tif, &width_pt, &height_pt);

		if (test_color)
		{
			is_color = colour_from_tags(tif, threshold_u8);
			if (is_color < 0 && test_pixels)
				is_color = colour_by_pixels(tif, threshold_u8);
		}

		/* Same rule as the mupdf path: only report a size that was measured. */
		if (!(width_pt > 0) || !(height_pt > 0))
		{
			width_pt = 0;
			height_pt = 0;
		}
		else
			measured++;

		fprintf(stdout, "Page=%i Size=%f,%f Color=%i\n", page + 1, width_pt, height_pt, is_color);
		fflush(stdout);
	}

	TIFFClose(tif);

	if (measured == 0)
	{
		fprintf(stderr, "No page could be measured\n");
		return 1;
	}

	return 0;
}

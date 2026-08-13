#ifndef PFC_TIFF_ENGINE_H
#define PFC_TIFF_ENGINE_H

/*
	Analyse a TIFF with libtiff rather than mupdf.

	mupdf decodes every page of a multi-page image format into a full pixmap
	just to hand back a page, so a 21600x14400 bilevel scan costs 296 MB and
	several seconds before anything is asked of it. Page geometry is in the
	tags, and a greyscale or bilevel photometric cannot hold colour whatever
	its compression, so most pages need no pixels at all.

	Emits the same stdout protocol as the mupdf path and returns 0 on success.
	Pages whose colour cannot be determined report Color=-1 rather than
	guessing black-and-white.
*/
int tiff_analyze(const char *filename_utf8, float color_threshold, int test_color, int test_pixels);

/*
	True if the file starts with a TIFF byte-order marker. Recognition is by
	content rather than extension, matching archive_kind() in main.c.
*/
int tiff_is_tiff(const char *filename_utf8);

#endif

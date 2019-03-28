#include <stdio.h>
#include <stdlib.h>
#include "mupdf\fitz.h"

int main(int argc, char **argv)
{
	char *filename = argv[1];
	float color_threshold = argc >= 3 ? atof(argv[2]) : -1;
	int test_pixels = argc >= 4 ? atoi(argv[3]) : 1;
	int test_color = color_threshold < 0 ? 0 : 1;
	int test_options = test_pixels ? FZ_TEST_OPT_IMAGES | FZ_TEST_OPT_SHADINGS : 0;

	fz_context *ctx = fz_new_context(NULL, NULL, FZ_STORE_DEFAULT);
	fz_register_document_handlers(ctx);
	fz_document *doc = fz_open_document(ctx, filename);

	int pagecount = fz_count_pages(ctx, doc);
	if (pagecount == 0)
	{
		fprintf(stderr, "Could not read page count");
		exit(-1);
	}

	fprintf(stdout, "PageCount=%i\n", pagecount);
	fflush(stdout);

	for (int i = 0; i < pagecount; i++)
	{
		fz_device *dev;
		fz_page *page;
		fz_rect bounds;
		int is_color = -1;

		page = fz_load_page(ctx, doc, i);
		fz_bound_page(ctx, page, &bounds);

		if (test_color)
		{
			fz_try(ctx)
			{
				dev = fz_new_test_device(ctx, &is_color, color_threshold, test_options, NULL);
				fz_enable_device_hints(ctx, dev, FZ_NO_CACHE | FZ_DONT_INTERPOLATE_IMAGES);
				fz_run_page(ctx, page, dev, &fz_identity, NULL);
			}
			fz_always(ctx)
			{
				fz_drop_device(ctx, dev);
			}
			fz_catch(ctx)
			{
				is_color = -1;
			}
		}

		fz_drop_page(ctx, page);
		fprintf(stdout, "Page=%i Size=%f,%f Color=%i\n", (i + 1), (bounds.x1 - bounds.x0), (bounds.y1 - bounds.y0), is_color);
		fflush(stdout);
	}
	fz_drop_document(ctx, doc);
	fz_drop_context(ctx);

	return 0;
}
#include <stdio.h>
#include <stdlib.h>
#include "mupdf\fitz.h"

int main(int argc, char **argv)
{
	char *filename = argv[1];
	float color_threshold = argc == 3 ? atof(argv[2]) : 0;
	fz_context *ctx = fz_new_context(NULL, NULL, FZ_STORE_DEFAULT);
	fz_register_document_handlers(ctx);
	fz_document *doc = fz_open_document(ctx, filename);

	int pagecount = fz_count_pages(ctx, doc);
	if (pagecount == 0)
	{
		fprintf(stderr, "closing: page count was zero. bad file.");
		exit(-1);
	}
	fprintf(stdout, "PageCount=%i\n", pagecount);

	int i;
	for (i = 0; i < pagecount; i++)
	{
		fprintf(stdout, "Page=%i ", i);
		fz_page *page = fz_load_page(ctx, doc, i);
		fz_rect bounds;
		fz_bound_page(ctx, page, &bounds);
		fprintf(stdout, "Size=%f,%f ", bounds.x1 - bounds.x0, bounds.y1 - bounds.y0);

		if (argc == 3)
		{
			int is_color = -1;
			fz_device *dev = fz_new_test_device(ctx, &is_color, color_threshold, FZ_TEST_OPT_IMAGES | FZ_TEST_OPT_SHADINGS, NULL);
			fz_enable_device_hints(ctx, dev, FZ_NO_CACHE | FZ_DONT_INTERPOLATE_IMAGES);
			fz_run_page(ctx, page, dev, &fz_identity, NULL);
			fz_drop_device(ctx, dev);
			fz_drop_page(ctx, page);
			fprintf(stdout, "Color=%i", is_color);
		}
		fprintf(stdout, "\n");
		fflush(stdout);
	}
	fz_drop_document(ctx, doc);
	fz_drop_context(ctx);

	return 0;
}
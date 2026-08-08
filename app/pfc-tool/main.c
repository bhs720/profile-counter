#include <stdio.h>
#include <stdlib.h>
#include "mupdf/fitz.h"

/* Corrupt or hostile documents can nest outlines arbitrarily deeply; cap the
 * recursion since a stack overflow can't be caught via fz_try. */
#define MAX_OUTLINE_DEPTH 512

static int count_bookmarks(fz_outline* outline, int depth)
{
	int count = 0;
	if (depth >= MAX_OUTLINE_DEPTH)
		return 0;
	while (outline)
	{
		count++;
		if (outline->down)
		{
			count += count_bookmarks(outline->down, depth + 1);
		}
		outline = outline->next;
	}
	return count;
}

/* Parse a C-locale number, rejecting trailing garbage. A culture-formatted
 * value such as "0,25" fails here rather than silently decoding as 0, which
 * would leave color analysis enabled at a zero threshold. */
static int parse_number(const char *s, double *out)
{
	char *end = NULL;
	double value;

	if (s == NULL || *s == '\0')
		return 0;

	value = strtod(s, &end);
	if (end == s || *end != '\0')
		return 0;

	*out = value;
	return 1;
}

int main(int argc, char **argv)
{
	const char *filename;
	fz_context *ctx = NULL;
	fz_document *doc = NULL;
	double parsed;
	float color_threshold = -1;
	int test_pixels = 1;
	int test_color;
	int test_options;
	int pagecount = 0;
	int bookmarkcount = 0;
	int i;

	if (argc < 2)
	{
		fprintf(stderr, "usage: mupdf.exe \"<filename>\" [<colorThreshold 0-1|-1>] [<checkPixels 0|1>]\n");
		return 1;
	}
	filename = argv[1];

	if (argc >= 3)
	{
		if (!parse_number(argv[2], &parsed))
		{
			fprintf(stderr, "Invalid colorThreshold '%s'; expected a C-locale number such as 0.25, or -1 to skip color analysis\n", argv[2]);
			return 1;
		}
		color_threshold = (float)parsed;
	}

	if (argc >= 4)
	{
		if (!parse_number(argv[3], &parsed))
		{
			fprintf(stderr, "Invalid checkPixels '%s'; expected 0 or 1\n", argv[3]);
			return 1;
		}
		test_pixels = (parsed != 0);
	}

	test_color = color_threshold < 0 ? 0 : 1;

	/* fz_new_test_device treats the threshold as a 0-1 fraction of full scale.
	 * Anything above 1 silently disables detection, so reject it up front
	 * rather than reporting every page as black and white. */
	if (test_color && color_threshold > 1)
	{
		fprintf(stderr, "Invalid colorThreshold %f; expected 0-1, or -1 to skip color analysis\n", color_threshold);
		return 1;
	}

	test_options = test_pixels ? FZ_TEST_OPT_IMAGES | FZ_TEST_OPT_SHADINGS : 0;

	ctx = fz_new_context(NULL, NULL, FZ_STORE_DEFAULT);
	if (ctx == NULL)
	{
		fprintf(stderr, "Failed to create mupdf context\n");
		return 1;
	}

	fz_var(doc);

	fz_try(ctx)
	{
		fz_register_document_handlers(ctx);
		doc = fz_open_document(ctx, filename);

		if (fz_needs_password(ctx, doc))
			fz_throw(ctx, FZ_ERROR_GENERIC, "document is password protected");

		pagecount = fz_count_pages(ctx, doc);
	}
	fz_catch(ctx)
	{
		fprintf(stderr, "Failed to open document: %s\n", fz_caught_message(ctx));
		fz_drop_document(ctx, doc);
		fz_drop_context(ctx);
		return 1;
	}

	if (pagecount == 0)
	{
		fprintf(stderr, "Could not read page count\n");
		fz_drop_document(ctx, doc);
		fz_drop_context(ctx);
		return 1;
	}

	/* Bookmarks are best-effort; a broken outline must not fail the file. */
	{
		fz_outline* outline = NULL;
		fz_var(outline);
		fz_try(ctx)
		{
			outline = fz_load_outline(ctx, doc);
			if (outline)
				bookmarkcount = count_bookmarks(outline, 0);
		}
		fz_always(ctx)
		{
			fz_drop_outline(ctx, outline);
		}
		fz_catch(ctx)
		{
			fprintf(stderr, "Failed to load document outline: %s\n", fz_caught_message(ctx));
			bookmarkcount = 0;
		}
	}

	fprintf(stdout, "PageCount=%i BookmarkCount=%i\n", pagecount, bookmarkcount);
	fflush(stdout);

	for (i = 0; i < pagecount; i++)
	{
		fz_device *dev = NULL;
		fz_page *page = NULL;
		fz_rect bounds = fz_empty_rect;
		int is_color = -1;

		fz_var(dev);
		fz_var(page);
		fz_var(bounds);
		fz_var(is_color);

		/* A single bad page must not abort the run: every page still gets a
		 * line so the caller's page count reconciliation holds. */
		fz_try(ctx)
		{
			page = fz_load_page(ctx, doc, i);
			bounds = fz_bound_page(ctx, page);

			if (test_color)
			{
				dev = fz_new_test_device(ctx, &is_color, color_threshold, test_options, NULL);
				fz_enable_device_hints(ctx, dev, FZ_NO_CACHE | FZ_DONT_INTERPOLATE_IMAGES);
				fz_run_page(ctx, page, dev, fz_identity, NULL);
				fz_close_device(ctx, dev);
			}
		}
		fz_always(ctx)
		{
			fz_drop_device(ctx, dev);
			fz_drop_page(ctx, page);
		}
		fz_catch(ctx)
		{
			fprintf(stderr, "Page %i failed: %s\n", (i + 1), fz_caught_message(ctx));
			is_color = -1;
		}

		fprintf(stdout, "Page=%i Size=%f,%f Color=%i\n", (i + 1), (bounds.x1 - bounds.x0), (bounds.y1 - bounds.y0), is_color);
		fflush(stdout);
	}

	fz_drop_document(ctx, doc);
	fz_drop_context(ctx);

	return 0;
}

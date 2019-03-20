#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include "mupdf\fitz.h"

#define OK 0
#define NO_INPUT 1
#define INPUT_TOO_LONG 2
#define CTX_FAILED 3
#define HANDLERS_FAILED 4
#define DOC_FAILED 5
#define PAGE_FAILED 6

static int get_line(char* buff, size_t sz)
{
	int ch, extra;

	if (fgets(buff, sz, stdin) == NULL)
		return NO_INPUT;

	if (buff[strlen(buff) - 1] != '\n') {
		extra = 0;
		while (((ch = getchar()) != '\n') && (ch != EOF))
			extra = 1;
		return (extra == 1) ? INPUT_TOO_LONG : OK;
	}

	buff[strlen(buff) - 1] = '\0';
	return OK;
}

static void do_color_test(fz_context* ctx, fz_page* page, int test_pixels, int color_threshold, int* is_color)
{
	fz_device* dev = NULL;
	int test_options = 0;	

	if (test_pixels)
		test_options = FZ_TEST_OPT_IMAGES | FZ_TEST_OPT_SHADINGS;
	else
		test_options = 0;

	fz_var(dev);
	fz_try(ctx)
	{
		dev = fz_new_test_device(ctx, is_color, color_threshold, test_options, NULL);
		fz_enable_device_hints(ctx, dev, FZ_NO_CACHE | FZ_DONT_INTERPOLATE_IMAGES);
		fz_run_page(ctx, page, dev, &fz_identity, NULL);
	}
	fz_always(ctx)
	{
		fz_drop_device(ctx, dev);
	}
	fz_catch(ctx)
		fz_rethrow(ctx);
}

static void process_page(fz_context* ctx, fz_document* doc, int number, int test_pixels, int color_threshold)
{
	fz_page* page = NULL;
	fz_rect mediabox;
	float width = 0;
	float height = 0;
	int is_color = -1;
	int test_color = 1;

	if (color_threshold < 0)
		test_color = 0;
	else
		test_color = 1;

	fz_var(page);
	fz_try(ctx)
	{
		page = fz_load_page(ctx, doc, number);
		fz_bound_page(ctx, page, &mediabox);
		width = mediabox.x1 - mediabox.x0;
		height = mediabox.y1 - mediabox.y0;

		if (test_color)
			do_color_test(ctx, page, test_pixels, color_threshold, &is_color);
		
		fprintf(stdout, "Page=%d Width=%f Height=%f Color=%d\n", number + 1, width, height, is_color);
		fflush(stdout);
	}
	fz_always(ctx)
	{
		fz_drop_page(ctx, page);
	}
	fz_catch(ctx)
		fz_rethrow(ctx);
}

static int process_file(char* filename, int test_pixels, int color_threshold)
{
	fz_context* ctx;
	fz_document* doc = NULL;
	int page_count;

	ctx = fz_new_context(NULL, NULL, FZ_STORE_DEFAULT);
	if (!ctx)
	{
		fprintf(stderr, "Failed to create context\n");
		return CTX_FAILED;
	}
	
	fz_try(ctx)
	{
		fz_register_document_handlers(ctx);
	}
	fz_catch(ctx)
	{
		fprintf(stderr, "Cannot initialize mupdf: %s\n", fz_caught_message(ctx));
		fz_drop_context(ctx);
		return HANDLERS_FAILED;
	}

	fz_var(doc);
	fz_try(ctx)
	{
		doc = fz_open_document(ctx, filename);
		page_count = fz_count_pages(ctx, doc);
		fprintf(stdout, "PageCount=%d\n", page_count);
		fflush(stdout);
		for (int i = 0; i < page_count; i++)
		{
			process_page(ctx, doc, i, test_pixels, color_threshold);
		}
		fz_drop_document(ctx, doc);
	}
	fz_catch(ctx)
	{
		fprintf(stderr, "Failed to open document: %s\n", fz_caught_message(ctx));
		fz_drop_document(ctx, doc);
		fz_drop_context(ctx);
		return DOC_FAILED;
	}

	fz_drop_context(ctx);
	return OK;
}

int main(int argc, char** argv)
{
	while (1)
	{
		char buff[1024];
		char filename[5] = "";
		int test_pixels, rc;
		float color_threshold = 0;

		rc = get_line(buff, sizeof(buff));
		if (rc == NO_INPUT)
		{
			fprintf(stderr, "No input\n");
		}
		else if (rc == INPUT_TOO_LONG)
		{
			fprintf(stderr, "Input too long [%s]\n", buff);
		}
		else if ((rc = sscanf(buff, " \"%[^\"]\" %d %f", filename, &test_pixels, &color_threshold)) != 3)
		{
			fprintf(stderr, "Failed to parse input [%s]\n", buff);
		}
		else
		{
			fprintf(stdout, "rc=%d filename=%s test_pixels=%d color_threshold=%f\n", rc, filename, test_pixels, color_threshold);
			rc = process_file(filename, test_pixels, color_threshold);
		}

		fprintf(stdout, "RC=%d\n", rc);
	}

	return 0;
}

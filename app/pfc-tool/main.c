#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include "mupdf/fitz.h"

/* Registered individually instead of via fz_register_document_handlers().
 * ProFile Counter only reports counts for formats where page count and page
 * dimensions are properties of the file itself.
 *
 * The reflowable formats (txt, html, xhtml, md, epub, mobi, fb2) and the Office
 * formats carry no intrinsic pagination: mupdf converts them to HTML and flows
 * the result onto FZ_DEFAULT_LAYOUT_W/H, a 420x595pt A5 canvas. A spreadsheet
 * reports A5 pages, and so does a landscape PowerPoint deck -- the numbers
 * describe mupdf's default layout, not the document, and they would land in the
 * page-size buckets as a size nobody printed. cbz turns the image entries of an
 * archive into "pages" for the same reason.
 *
 * img covers TIFF/JPEG/PNG/BMP and must stay: those are analysed deliberately. */
extern fz_document_handler pdf_document_handler;
extern fz_document_handler img_document_handler;

/* Corrupt or hostile documents can nest outlines arbitrarily deeply; cap the
 * recursion since a stack overflow can't be caught via fz_try. */
#define MAX_OUTLINE_DEPTH 512

/* Enough to reach TAR's "ustar" magic, which sits at offset 257. */
#define ARCHIVE_PROBE_BYTES 262

static int magic_at(const unsigned char *buf, size_t n, size_t off, const unsigned char *sig, size_t len)
{
	return n >= off + len && memcmp(buf + off, sig, len) == 0;
}

/* Identify archive containers by magic number, returning a display name or NULL.
 *
 * Unregistering the archive handlers is not enough on its own: mupdf's PDF
 * repair scans a file for a %PDF marker and rebuilds a document from whatever
 * it finds, so a .zip holding PDFs is reported as a document with the page count
 * of the first one inside. The PDF handler cannot be dropped, so the container
 * has to be recognised before fz_open_document ever sees the file.
 *
 * Deliberately a blocklist rather than an allowlist of known-good headers:
 * genuinely damaged PDFs may carry garbage before their %PDF marker and mupdf
 * repairs them successfully, which is behaviour worth keeping. */
static const char *archive_kind(const char *filename)
{
	static const unsigned char zip_local[]  = { 0x50, 0x4B, 0x03, 0x04 };
	static const unsigned char zip_empty[]  = { 0x50, 0x4B, 0x05, 0x06 };
	static const unsigned char zip_spare[]  = { 0x50, 0x4B, 0x07, 0x08 };
	static const unsigned char sevenzip[]   = { 0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C };
	static const unsigned char rar[]        = { 0x52, 0x61, 0x72, 0x21, 0x1A, 0x07 };
	static const unsigned char gzip[]       = { 0x1F, 0x8B };
	static const unsigned char bzip2[]      = { 0x42, 0x5A, 0x68 };
	static const unsigned char xz[]         = { 0xFD, 0x37, 0x7A, 0x58, 0x5A, 0x00 };
	static const unsigned char tar_ustar[]  = { 0x75, 0x73, 0x74, 0x61, 0x72 };

	unsigned char buf[ARCHIVE_PROBE_BYTES];
	size_t n;
	FILE *f;

	f = fopen(filename, "rb");
	if (f == NULL)
		return NULL; /* Let fz_open_document report the real problem. */

	n = fread(buf, 1, sizeof buf, f);
	fclose(f);

	if (magic_at(buf, n, 0, zip_local, sizeof zip_local) ||
		magic_at(buf, n, 0, zip_empty, sizeof zip_empty) ||
		magic_at(buf, n, 0, zip_spare, sizeof zip_spare))
		return "ZIP"; /* also catches docx/xlsx/pptx/epub, which are ZIP containers */
	if (magic_at(buf, n, 0, sevenzip, sizeof sevenzip))
		return "7-Zip";
	if (magic_at(buf, n, 0, rar, sizeof rar))
		return "RAR";
	if (magic_at(buf, n, 0, gzip, sizeof gzip))
		return "gzip";
	if (magic_at(buf, n, 0, bzip2, sizeof bzip2))
		return "bzip2";
	if (magic_at(buf, n, 0, xz, sizeof xz))
		return "xz";
	if (magic_at(buf, n, 257, tar_ustar, sizeof tar_ustar))
		return "TAR";

	return NULL;
}

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
	int measured = 0;
	int i;

	if (argc < 2)
	{
		fprintf(stderr, "usage: pfc-tool.exe \"<filename>\" [<colorThreshold 0-1|-1>] [<checkPixels 0|1>]\n");
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

	/* Refuse archives before mupdf can mine them for embedded PDF data. */
	{
		const char *kind = archive_kind(filename);
		if (kind != NULL)
		{
			fprintf(stderr, "Refusing %s archive: not a document\n", kind);
			return 1;
		}
	}

	ctx = fz_new_context(NULL, NULL, FZ_STORE_DEFAULT);
	if (ctx == NULL)
	{
		fprintf(stderr, "Failed to create mupdf context\n");
		return 1;
	}

	fz_var(doc);

	fz_try(ctx)
	{
		fz_register_document_handler(ctx, &pdf_document_handler);
		fz_register_document_handler(ctx, &img_document_handler);
		doc = fz_open_document(ctx, filename);

		if (fz_needs_password(ctx, doc))
			fz_throw(ctx, FZ_ERROR_GENERIC, "document is password protected");

		pagecount = fz_count_pages(ctx, doc);
	}
	fz_catch(ctx)
	{
		/* fz_catch does not clear the error state; the handler must resolve it
		 * with fz_report_error, fz_ignore_error or fz_rethrow. Reading the
		 * message alone leaves errcode set, and fz_drop_context would then
		 * report a spurious "UNHANDLED EXCEPTION!". */
		fz_report_error(ctx);
		fprintf(stderr, "Failed to open document\n");
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
			/* Must clear the error state here too: this path continues on to
			 * the page loop, and a lingering errcode makes every later throw
			 * inside mupdf emit "UNHANDLED EXCEPTION!" as well. */
			fz_report_error(ctx);
			fprintf(stderr, "Failed to load document outline\n");
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
		float width, height;
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
			/* Same again: the loop continues to the next page, so the error
			 * state must not be left set behind us. */
			fz_report_error(ctx);
			fprintf(stderr, "Page %i failed\n", (i + 1));
			is_color = -1;
		}

		/* Only report a size that was actually measured. fz_empty_rect is an
		 * inverted sentinel (x0 = FZ_MAX_INF_RECT, x1 = FZ_MIN_INF_RECT), so a
		 * page that never loaded would otherwise print a width of about -2^32 --
		 * a negative number that the caller's Size= grammar does not accept, so
		 * the whole file is rejected as garbled. The >0 tests are written so
		 * that NaN fails them too. */
		width = bounds.x1 - bounds.x0;
		height = bounds.y1 - bounds.y0;
		if (!(width > 0) || !(height > 0))
			width = height = 0;
		else
			measured++;

		fprintf(stdout, "Page=%i Size=%f,%f Color=%i\n", (i + 1), width, height, is_color);
		fflush(stdout);
	}

	fz_drop_document(ctx, doc);
	fz_drop_context(ctx);

	/* A document that opened but yielded no measurable page is not something the
	 * caller can count. Report it as a failure rather than as pages of unknown
	 * size, which would silently inflate the totals. A document with some good
	 * pages still succeeds; those that failed carry a 0x0 size and Color=-1. */
	if (measured == 0)
	{
		fprintf(stderr, "No page could be measured\n");
		return 1;
	}

	return 0;
}

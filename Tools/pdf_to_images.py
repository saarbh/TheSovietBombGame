"""Render a PDF's pages to PNG images.

The design docs for this project (notably GMTKGJ2026ChatGPT.pdf) are image-only
exports - text extraction returns nothing, so the pages have to be rasterised and
read visually. This script exists so that never has to be rediscovered.

Lives outside Assets/ deliberately: anything under Assets/ gets imported by Unity.

Usage:
    python Tools/pdf_to_images.py <pdf> [-o OUTDIR] [--dpi 110] [--pages 1-6,9]

Requires: pip install pymupdf
"""

import argparse
import sys
from pathlib import Path


def parse_page_spec(spec, page_count):
    """Turn "1-6,9" into a sorted list of zero-based page indices."""
    if not spec:
        return list(range(page_count))

    wanted = set()

    for chunk in spec.split(","):
        chunk = chunk.strip()

        if not chunk:
            continue

        if "-" in chunk:
            first, last = chunk.split("-", 1)
            wanted.update(range(int(first) - 1, int(last)))
        else:
            wanted.add(int(chunk) - 1)

    return sorted(i for i in wanted if 0 <= i < page_count)


def render(pdf_path, out_dir, dpi, page_spec):
    try:
        import pymupdf
    except ImportError:
        sys.exit("pymupdf is not installed. Run: pip install pymupdf")

    pdf_path = Path(pdf_path)
    out_dir = Path(out_dir)
    out_dir.mkdir(parents=True, exist_ok=True)

    document = pymupdf.open(pdf_path)
    indices = parse_page_spec(page_spec, len(document))
    written = []

    for index in indices:
        target = out_dir / f"{pdf_path.stem}_pg{index + 1:02d}.png"
        document[index].get_pixmap(dpi=dpi).save(target)
        written.append(target)

    document.close()

    for path in written:
        print(path)

    print(f"{len(written)} page(s) -> {out_dir}", file=sys.stderr)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("pdf", help="Path to the source PDF.")
    parser.add_argument("-o", "--out", default="pdf_pages", help="Output directory.")
    parser.add_argument("--dpi", type=int, default=110, help="Render resolution.")
    parser.add_argument("--pages", default="", help='Page ranges, e.g. "1-6,9". Default: all.')

    args = parser.parse_args()
    render(args.pdf, args.out, args.dpi, args.pages)


if __name__ == "__main__":
    main()

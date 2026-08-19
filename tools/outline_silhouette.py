"""Trace a white outline around a cut-out's alpha channel.

The loadout mannequin was a solid purple body on a near-black panel: no edge,
no read, and the eye had nothing to hang the equipment slots off. A silhouette
wants a LINE, not a fill -- the line says "a body is here" while staying out of
the way of the item art hung around it, which a filled shape cannot do.

Produced as a separate PNG rather than by tinting the source, because the two
layers want different opacities: a very dim fill for mass, a crisp line for
definition. One image cannot be both.

    python tools/outline_silhouette.py <source.png> <out.png> [--radius 4]

The band is (dilate - erode) over the alpha mask, then blurred a little so the
edge is anti-aliased. Straight thresholding gives a line that crawls with
stair-steps once it is scaled down into the panel.
"""
import argparse
import sys

from PIL import Image, ImageFilter


def outline(source: Image.Image, radius: int, feather: float) -> Image.Image:
    alpha = source.split()[-1].point(lambda v: 255 if v > 128 else 0)

    # MaxFilter/MinFilter want an odd window; the band ends up about 2*radius
    # wide, straddling the true edge so the line sits ON the silhouette rather
    # than outside it and growing the figure.
    window = radius * 2 + 1
    grown = alpha.filter(ImageFilter.MaxFilter(window))
    shrunk = alpha.filter(ImageFilter.MinFilter(window))

    from PIL import ImageChops
    band = ImageChops.subtract(grown, shrunk)
    if feather > 0:
        band = band.filter(ImageFilter.GaussianBlur(feather))

    white = Image.new("RGBA", source.size, (255, 255, 255, 0))
    white.putalpha(band)
    return white


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("source")
    parser.add_argument("out")
    parser.add_argument("--radius", type=int, default=4,
                        help="half-width of the line in SOURCE pixels; the "
                             "mannequin is shown at about a quarter size, so 4 "
                             "reads as a ~2px line on screen")
    parser.add_argument("--feather", type=float, default=1.0)
    args = parser.parse_args()

    source = Image.open(args.source).convert("RGBA")
    if source.split()[-1].getextrema() == (255, 255):
        print(f"{args.source} has no transparency - there is no silhouette to "
              f"trace, only a rectangle.", file=sys.stderr)
        return 1

    outline(source, args.radius, args.feather).save(args.out)
    print(f"wrote {args.out}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

#!/usr/bin/env python3
"""Prepare transparent RX-9 lighting fixtures and a visual QA sheet."""

from __future__ import annotations

import argparse
from pathlib import Path

from PIL import Image, ImageDraw


CEILING_SIZE = (512, 160)
BEACON_SIZE = (256, 256)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--ceiling-source", type=Path, required=True)
    parser.add_argument("--beacon-source", type=Path, required=True)
    parser.add_argument("--output-directory", type=Path, required=True)
    parser.add_argument("--preview", type=Path, required=True)
    parser.add_argument("--floor-tile", type=Path)
    return parser.parse_args()


def alpha_bbox(image: Image.Image) -> tuple[int, int, int, int]:
    alpha = image.getchannel("A")
    mask = alpha.point(lambda value: 255 if value >= 8 else 0)
    bbox = mask.getbbox()
    if bbox is None:
        raise ValueError("Source contains no visible pixels.")
    return bbox


def fit_sprite(
    source_path: Path,
    target_size: tuple[int, int],
    padding: tuple[int, int],
) -> Image.Image:
    with Image.open(source_path) as source:
        rgba = source.convert("RGBA")
        subject = rgba.crop(alpha_bbox(rgba))

    maximum = (
        target_size[0] - padding[0] * 2,
        target_size[1] - padding[1] * 2,
    )
    scale = min(maximum[0] / subject.width, maximum[1] / subject.height)
    resized = subject.resize(
        (
            max(1, round(subject.width * scale)),
            max(1, round(subject.height * scale)),
        ),
        Image.Resampling.LANCZOS,
    )
    result = Image.new("RGBA", target_size, (0, 0, 0, 0))
    result.alpha_composite(
        resized,
        ((target_size[0] - resized.width) // 2, (target_size[1] - resized.height) // 2),
    )
    return result


def build_background(tile_path: Path | None) -> Image.Image:
    canvas = Image.new("RGBA", (1536, 900), (24, 33, 41, 255))
    if tile_path is None or not tile_path.is_file():
        return canvas

    with Image.open(tile_path) as source:
        tile = source.convert("RGB").resize((256, 256), Image.Resampling.LANCZOS)
    for y in range(0, canvas.height, tile.height):
        for x in range(0, canvas.width, tile.width):
            canvas.paste(tile, (x, y))

    veil = Image.new("RGBA", canvas.size, (6, 14, 20, 150))
    canvas.alpha_composite(veil)
    return canvas


def make_preview(
    ceiling: Image.Image,
    beacon: Image.Image,
    output_path: Path,
    floor_tile: Path | None,
) -> None:
    canvas = build_background(floor_tile)
    glow_layer = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    glow = ImageDraw.Draw(glow_layer)
    glow.rounded_rectangle((120, 125, 760, 355), radius=90, fill=(52, 198, 200, 26))
    glow.rounded_rectangle((780, 450, 1420, 680), radius=90, fill=(52, 198, 200, 26))
    glow.ellipse((610, 475, 1010, 875), fill=(232, 84, 40, 22))
    canvas.alpha_composite(glow_layer)

    ceiling_large = ceiling.resize((640, 200), Image.Resampling.LANCZOS)
    canvas.alpha_composite(ceiling_large, (120, 140))
    canvas.alpha_composite(ceiling_large, (780, 465))
    beacon_large = beacon.resize((280, 280), Image.Resampling.LANCZOS)
    canvas.alpha_composite(beacon_large, (670, 560))

    output_path.parent.mkdir(parents=True, exist_ok=True)
    canvas.convert("RGB").save(output_path, format="PNG", optimize=True)


def main() -> None:
    args = parse_args()
    ceiling = fit_sprite(args.ceiling_source, CEILING_SIZE, (18, 14))
    beacon = fit_sprite(args.beacon_source, BEACON_SIZE, (18, 18))
    args.output_directory.mkdir(parents=True, exist_ok=True)
    ceiling.save(
        args.output_directory / "S_CeilingLightPanel.png",
        format="PNG",
        optimize=True,
    )
    beacon.save(
        args.output_directory / "S_EmergencyBeacon.png",
        format="PNG",
        optimize=True,
    )
    make_preview(ceiling, beacon, args.preview, args.floor_tile)


if __name__ == "__main__":
    main()

#!/usr/bin/env python3
"""Prepare transparent RX-9 quarantine VFX and a visual QA sheet."""

from __future__ import annotations

import argparse
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


ASSET_SPECS = {
    "S_QuarantineWarningBeacon.png": ((256, 256), (14, 14)),
    "S_ContainmentFloorGrid.png": ((1024, 560), (20, 18)),
    "S_BrokenGlass_A.png": ((768, 432), (18, 16)),
    "S_ContainmentFloorNumbers.png": ((1024, 224), (18, 12)),
}


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--beacon-source", type=Path, required=True)
    parser.add_argument("--grid-source", type=Path, required=True)
    parser.add_argument("--glass-source", type=Path, required=True)
    parser.add_argument("--numbers-source", type=Path, required=True)
    parser.add_argument("--font", type=Path, required=True)
    parser.add_argument("--output-directory", type=Path, required=True)
    parser.add_argument("--preview", type=Path, required=True)
    parser.add_argument("--floor-tile", type=Path)
    return parser.parse_args()


def alpha_bbox(image: Image.Image) -> tuple[int, int, int, int]:
    bbox = image.getchannel("A").point(
        lambda value: 255 if value >= 8 else 0
    ).getbbox()
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
        (
            (target_size[0] - resized.width) // 2,
            (target_size[1] - resized.height) // 2,
        ),
    )
    return result


def add_containment_numbers(
    base: Image.Image,
    font_path: Path,
) -> Image.Image:
    result = base.copy()
    draw = ImageDraw.Draw(result)
    font = ImageFont.truetype(str(font_path), 96)
    for center_x, label in zip((190, 512, 834), ("01", "02", "03")):
        text_box = draw.textbbox((0, 0), label, font=font, stroke_width=4)
        text_width = text_box[2] - text_box[0]
        text_height = text_box[3] - text_box[1]
        draw.text(
            (
                center_x - text_width / 2,
                112 - text_height / 2 - text_box[1],
            ),
            label,
            font=font,
            fill=(232, 248, 247, 245),
            stroke_width=4,
            stroke_fill=(7, 20, 25, 240),
        )
    return result


def build_floor(floor_tile_path: Path | None) -> Image.Image:
    canvas = Image.new("RGBA", (1600, 950), (41, 49, 55, 255))
    if floor_tile_path is not None and floor_tile_path.is_file():
        with Image.open(floor_tile_path) as source:
            tile = source.convert("RGB").resize(
                (256, 256), Image.Resampling.LANCZOS
            )
        for y in range(0, canvas.height, tile.height):
            for x in range(0, canvas.width, tile.width):
                canvas.paste(tile, (x, y))
    canvas.alpha_composite(Image.new("RGBA", canvas.size, (5, 12, 18, 52)))
    return canvas


def make_preview(
    sprites: dict[str, Image.Image],
    output_path: Path,
    floor_tile_path: Path | None,
) -> None:
    canvas = build_floor(floor_tile_path)
    placements = {
        "S_QuarantineWarningBeacon.png": ((100, 75), (280, 280)),
        "S_BrokenGlass_A.png": ((820, 50), (680, 382)),
        "S_ContainmentFloorGrid.png": ((60, 430), (940, 514)),
        "S_ContainmentFloorNumbers.png": ((1040, 660), (500, 109)),
    }
    for name, (position, target_size) in placements.items():
        sprite = sprites[name].resize(target_size, Image.Resampling.LANCZOS)
        canvas.alpha_composite(sprite, position)

    output_path.parent.mkdir(parents=True, exist_ok=True)
    canvas.convert("RGB").save(output_path, format="PNG", optimize=True)


def validate_sprite(name: str, image: Image.Image) -> None:
    if image.mode != "RGBA":
        raise ValueError(f"{name} must be RGBA.")
    corners = (
        image.getpixel((0, 0))[3],
        image.getpixel((image.width - 1, 0))[3],
        image.getpixel((0, image.height - 1))[3],
        image.getpixel((image.width - 1, image.height - 1))[3],
    )
    if any(corners):
        raise ValueError(f"{name} must have transparent corners: {corners}")
    if image.getchannel("A").getbbox() is None:
        raise ValueError(f"{name} contains no visible pixels.")


def main() -> None:
    args = parse_args()
    sources = {
        "S_QuarantineWarningBeacon.png": args.beacon_source,
        "S_ContainmentFloorGrid.png": args.grid_source,
        "S_BrokenGlass_A.png": args.glass_source,
        "S_ContainmentFloorNumbers.png": args.numbers_source,
    }
    sprites = {
        name: fit_sprite(sources[name], *ASSET_SPECS[name])
        for name in ASSET_SPECS
    }
    sprites["S_ContainmentFloorNumbers.png"] = add_containment_numbers(
        sprites["S_ContainmentFloorNumbers.png"],
        args.font,
    )

    args.output_directory.mkdir(parents=True, exist_ok=True)
    for name, sprite in sprites.items():
        validate_sprite(name, sprite)
        sprite.save(args.output_directory / name, format="PNG", optimize=True)

    make_preview(sprites, args.preview, args.floor_tile)


if __name__ == "__main__":
    main()

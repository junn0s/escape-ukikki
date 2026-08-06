#!/usr/bin/env python3
"""Prepare ward blood decals, deterministic triage numbers, and a QA sheet."""

from __future__ import annotations

import argparse
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


BLOOD_SPECS = {
    "S_BloodStain_A.png": ((512, 256), (16, 14)),
    "S_BloodStain_B.png": ((512, 256), (16, 14)),
}
TRIAGE_SIZE = (1024, 256)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--blood-a-source", type=Path, required=True)
    parser.add_argument("--blood-b-source", type=Path, required=True)
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


def create_triage_numbers(font_path: Path) -> Image.Image:
    image = Image.new("RGBA", TRIAGE_SIZE, (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    font = ImageFont.truetype(str(font_path), 118)
    centers = (190, 512, 834)
    for index, center_x in enumerate(centers, start=1):
        box = (center_x - 108, 30, center_x + 108, 226)
        draw.rounded_rectangle(
            box,
            radius=44,
            fill=(16, 31, 38, 178),
            outline=(86, 221, 225, 232),
            width=12,
        )
        draw.rounded_rectangle(
            (box[0] + 16, box[1] + 16, box[2] - 16, box[3] - 16),
            radius=32,
            outline=(230, 245, 244, 96),
            width=4,
        )
        label = str(index)
        text_box = draw.textbbox((0, 0), label, font=font, stroke_width=3)
        text_width = text_box[2] - text_box[0]
        text_height = text_box[3] - text_box[1]
        draw.text(
            (
                center_x - text_width / 2,
                128 - text_height / 2 - text_box[1],
            ),
            label,
            font=font,
            fill=(235, 247, 246, 238),
            stroke_width=3,
            stroke_fill=(9, 18, 23, 230),
        )

    for center_x in (351, 673):
        draw.polygon(
            (
                (center_x - 20, 94),
                (center_x + 20, 128),
                (center_x - 20, 162),
                (center_x - 20, 143),
                (center_x + 1, 128),
                (center_x - 20, 113),
            ),
            fill=(237, 145, 49, 224),
        )
    return image


def build_floor(floor_tile_path: Path | None) -> Image.Image:
    canvas = Image.new("RGBA", (1536, 900), (41, 49, 55, 255))
    if floor_tile_path is not None and floor_tile_path.is_file():
        with Image.open(floor_tile_path) as source:
            tile = source.convert("RGB").resize(
                (256, 256), Image.Resampling.LANCZOS
            )
        for y in range(0, canvas.height, tile.height):
            for x in range(0, canvas.width, tile.width):
                canvas.paste(tile, (x, y))
    canvas.alpha_composite(Image.new("RGBA", canvas.size, (5, 12, 18, 48)))
    return canvas


def make_preview(
    sprites: dict[str, Image.Image],
    output_path: Path,
    floor_tile_path: Path | None,
) -> None:
    canvas = build_floor(floor_tile_path)
    placements = {
        "S_BloodStain_A.png": ((65, 110), (690, 345)),
        "S_BloodStain_B.png": ((875, 125), (560, 280)),
        "S_TriageFloorNumbers.png": ((260, 585), (1024, 256)),
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
        "S_BloodStain_A.png": args.blood_a_source,
        "S_BloodStain_B.png": args.blood_b_source,
    }
    sprites = {
        name: fit_sprite(sources[name], *BLOOD_SPECS[name])
        for name in BLOOD_SPECS
    }
    sprites["S_TriageFloorNumbers.png"] = create_triage_numbers(args.font)

    args.output_directory.mkdir(parents=True, exist_ok=True)
    for name, sprite in sprites.items():
        validate_sprite(name, sprite)
        sprite.save(args.output_directory / name, format="PNG", optimize=True)

    make_preview(sprites, args.preview, args.floor_tile)


if __name__ == "__main__":
    main()
